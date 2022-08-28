using ECSAtmos.Components;
using ECSAtmos.Util;
using Systems.Atmospherics;
using Systems.ECSAtmos;
using Systems.ECSAtmos.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace ECSAtmos.Systems
{
	[BurstCompile]
	[UpdateInGroup(typeof(AtmosSystemGroup))]
    [UpdateAfter(typeof(PipeGasExchangeSystem))]
    public partial class TileGasExchangeSystem : AtmosSystemBase
    {
	    private EntityQuery query;

        private AtmosEndEntityCommandBufferSystem commandBufferSystem;

        protected override void OnCreate()
        {
	        commandBufferSystem = World.GetOrCreateSystem<AtmosEndEntityCommandBufferSystem>();

	        var queryDesc = new EntityQueryDesc
	        {
				All = new ComponentType[]
				{
					typeof(AtmosUpdateDataComponent),
					typeof(MetaDataTileComponent),
					typeof(NeighbourBuffer),
					typeof(GasMixComponent),
					typeof(GasDataBuffer),
					typeof(TileAtmosTag)
				},

				None = new ComponentType[]
				{
					typeof(DeactivatedTag)
				}
	        };

	        query = GetEntityQuery(queryDesc);
        }

        protected override JobHandle Update(JobHandle inputDeps, OffsetLogic offset)
        {
	        TileGasExchangeJob job = new TileGasExchangeJob
	        {
		        metaDataTileNeighbor = GetBufferTypeHandle<NeighbourBuffer>(true),

		        entityTypeHandle = GetEntityTypeHandle(),

		        offset = offset,

		        allGasMix = GetComponentDataFromEntity<GasMixComponent>(),

		        allMetaDataTile = GetComponentDataFromEntity<MetaDataTileComponent>(true),

		        allGasData = GetBufferFromEntity<GasDataBuffer>(),
		        allUpdateData = GetComponentDataFromEntity<AtmosUpdateDataComponent>(),
		        ecb = commandBufferSystem.CreateCommandBuffer().AsParallelWriter()
	        };

	        inputDeps = job.ScheduleParallel(query, inputDeps);

	        commandBufferSystem.AddJobHandleForProducer(inputDeps);

	        return inputDeps;
        }

        [BurstCompile]
        private struct TileGasExchangeJob : IJobEntityBatch
        {
	        [ReadOnly]
	        public BufferTypeHandle<NeighbourBuffer> metaDataTileNeighbor;

	        [ReadOnly]
	        public EntityTypeHandle entityTypeHandle;

	        [ReadOnly]
	        public OffsetLogic offset;

	        //Writing and Reading from these, this is thread safe as tiles are only access once in an area so no conflicts
	        [NativeDisableParallelForRestriction]
	        public ComponentDataFromEntity<GasMixComponent> allGasMix;

	        [NativeDisableParallelForRestriction]
	        public BufferFromEntity<GasDataBuffer> allGasData;

	        [ReadOnly]
	        [NativeDisableParallelForRestriction]
	        public ComponentDataFromEntity<MetaDataTileComponent> allMetaDataTile;

	        [NativeDisableParallelForRestriction]
	        public ComponentDataFromEntity<AtmosUpdateDataComponent> allUpdateData;

	        [NativeDisableParallelForRestriction]
	        public EntityCommandBuffer.ParallelWriter ecb;

	        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
	        {
		        BufferAccessor<NeighbourBuffer> metaDataTileNeighbors = batchInChunk.GetBufferAccessor(this.metaDataTileNeighbor);

		        for (int i = 0; i < batchInChunk.Count; ++i)
		        {
			        var entity = batchInChunk.GetNativeArray(entityTypeHandle)[i];

			        var currentUpdateData = allUpdateData[entity];

			        //This basically means that every forth tile is allowed to do an update
			        //But we alternate which ones every step (I would draw a shitty paint diagram to explain but i cant draw)
			        //Basically means every forth update every tile will have done an update
			        //NOTE: because of this we don't update two tiles in the same frame therefore can disable writing safeties in the job
			        if (currentUpdateData.XUpdateID != offset.XUpdateID || currentUpdateData.YUpdateID != offset.YUpdateID) continue;

			        currentUpdateData.TriedToUpdate = true;

			        allUpdateData[entity] = currentUpdateData;

			        var currentMetaDataTile = allMetaDataTile[entity];

			        //We only need to check open tiles
			        if (currentMetaDataTile.IsIsolatedNode) continue;

			        //We only need to check non-solid tiles
			        if (currentMetaDataTile.IsSolid) continue;

			        var neighbors = metaDataTileNeighbors[i];

			        //No neighbors so don't bother
			        if(neighbors.Length == 0) continue;

			        var neighborsEqualise = new NativeArray<bool>(neighbors.Length, Allocator.Temp);
			        var boolShouldEqualise = false;

			        for (int j = neighbors.Length - 1; j >= 0; j--)
			        {
				        var neighbour = neighbors[j];

				        //TODO might need to check neighbour still exists?
				        var neighbourMetaDataTile = allMetaDataTile[neighbour.NeighbourEntity];

				        //Bool means to block gas equalise, e.g for when closed windoor/directional passable
				        //Have to do IsOccupiedBlocked from both tiles perspective
				        var equalise = neighbourMetaDataTile.IsIsolatedNode == false &&
				                       neighbourMetaDataTile.IsSolid == false &&
				                       AtmosUtils.IsOccupiedBlocked(in currentMetaDataTile, in neighbourMetaDataTile) == false &&
				                       AtmosUtils.IsOccupiedBlocked(in neighbourMetaDataTile, in currentMetaDataTile) == false;

				        neighborsEqualise[j] = equalise;

				        if(equalise == false) continue;

				        boolShouldEqualise = true;
			        }

			        //No neighbors to equalise to so don't bother
			        if(boolShouldEqualise == false) continue;

			        var currentGasMix = allGasMix[entity];
			        var currentGasDataBuffer = allGasData[entity];

			        //TODO out the wind stuff
			        var isPressureChanged = PressureCheck(in neighbors, in neighborsEqualise, ref allGasMix,
				        in allMetaDataTile, ref allGasData,
				        ref currentGasMix, ref currentGasDataBuffer, in currentMetaDataTile.TileLocalPos);

					if (isPressureChanged == false)
					{
						//TODO queue wind event, use ECB?
						continue;
					}

					//Now to equalise the gases
					Equalise(in neighbors, in neighborsEqualise, ref allGasMix, ref allUpdateData, in allMetaDataTile, ref allGasData,
						ref currentGasMix, ref currentGasDataBuffer, in entity, in currentMetaDataTile, ref currentUpdateData, ref ecb, in batchIndex);
		        }
	        }

	        private static bool PressureCheck(
		        in DynamicBuffer<NeighbourBuffer> neighbors,
		        in NativeArray<bool> neighborsEqualise,
		        ref ComponentDataFromEntity<GasMixComponent> allGasMix,
		        in ComponentDataFromEntity<MetaDataTileComponent> allMetaDataTile,
		        ref BufferFromEntity<GasDataBuffer> allGasData,
		        ref GasMixComponent currentGasMix,
		        ref DynamicBuffer<GasDataBuffer> currentGasDataBuffer,
		        in int3 localPos)
	        {
				var windDirection = int2.zero;
				var clampVector = int3.zero;
				float windForce = 0L;

				bool isPressureChanged = false;

				for (var k = 0; k < neighbors.Length; k++)
				{
					//We only need to check neighbours which can equalise
					if (neighborsEqualise[k] == false) continue;

					var neighborEntity = neighbors[k].NeighbourEntity;
					var neighborGasMix = allGasMix[neighborEntity];
					var neighborTileData = allMetaDataTile[neighborEntity];

					float pressureDifference = currentGasMix.Pressure - neighborGasMix.Pressure;
					float absoluteDifference = math.abs(pressureDifference);

					//Check to see if theres a large pressure difference
					if (absoluteDifference > AtmosConstants.MinPressureDifference)
					{
						isPressureChanged = true;

						if (absoluteDifference > windForce)
						{
							windForce = absoluteDifference;
						}

						int neighborOffsetX = (neighborTileData.TileLocalPos.x - localPos.x);
						int neighborOffsetY = (neighborTileData.TileLocalPos.y - localPos.y);

						if (pressureDifference > 0)
						{
							windDirection.x += neighborOffsetX;
							windDirection.y += neighborOffsetY;
						}
						else if (pressureDifference < 0)
						{
							windDirection.x -= neighborOffsetX;
							windDirection.y -= neighborOffsetY;
						}

						clampVector.x -= neighborOffsetX;
						clampVector.y -= neighborOffsetY;

						//We continue here so we can calculate the whole wind direction from all possible nodes
						continue;
					}

					//Check if the moles are different. (e.g. CO2 is different from breathing)
					//Check current node then check neighbor so we dont miss a gas if its only on one of the nodes

					var neighborBuffer = allGasData[neighborEntity];

					//Current node
					//Only need to check if false
					if (isPressureChanged == false)
					{
						for (int j = currentGasDataBuffer.Length - 1; j >= 0; j--)
						{
							var gas = currentGasDataBuffer[j].GasData;
							var moles = gas.Moles;
							float molesNeighbor = neighborBuffer.GetMoles(gas.GasSO);

							if (math.abs(moles - molesNeighbor) > AtmosConstants.MinPressureDifference)
							{
								isPressureChanged = true;

								//We break not return here so we can still work out wind direction
								break;
							}
						}
					}

					//Neighbor node
					//Only need to check if false
					if (isPressureChanged == false)
					{
						for (int j = 0; j < neighborBuffer.Length; j++)
						{
							var gas = neighborBuffer[j].GasData;
							float molesNeighbor = gas.Moles;
							float moles = currentGasDataBuffer.GetMoles(gas.GasSO);

							if (math.abs(moles - molesNeighbor) > AtmosConstants.MinPressureDifference)
							{
								isPressureChanged = true;

								//We break not return here so we can still work out wind direction
								break;
							}
						}
					}
		        }

				//not blowing in direction of tiles that aren't atmos passable
				windDirection.y = math.clamp(windDirection.y, clampVector.y < 0 ? 0 : -1,
					clampVector.y > 0 ? 0 : 1);
				windDirection.x = math.clamp(windDirection.x, clampVector.x < 0 ? 0 : -1,
					clampVector.x > 0 ? 0 : 1);

				return isPressureChanged;
	        }

	        private static void Equalise(
		        in DynamicBuffer<NeighbourBuffer> neighbors,
		        in NativeArray<bool> neighborsEqualise,
		        ref ComponentDataFromEntity<GasMixComponent> allGasMix,
		        ref ComponentDataFromEntity<AtmosUpdateDataComponent> allUpdateData,
		        in ComponentDataFromEntity<MetaDataTileComponent> allMetaDataTile,
		        ref BufferFromEntity<GasDataBuffer> allGasData,
		        ref GasMixComponent currentGasMix,
		        ref DynamicBuffer<GasDataBuffer> currentGasDataBuffer,
		        in Entity entity,
		        in MetaDataTileComponent currentMetaData,
		        ref AtmosUpdateDataComponent currentUpdateData,
		        ref EntityCommandBuffer.ParallelWriter ecb,
		        in int batchIndex)
	        {
					var dividingCount = 1;

					//Add all the gases
					for (int j = 0; j < neighbors.Length; j++)
					{
						//We only need to check neighbours which can equalise
						if (neighborsEqualise[j] == false) continue;

						var neighborEntity = neighbors[j].NeighbourEntity;

						dividingCount++;

						var neighborBuffer = allGasData[neighborEntity];
						var neighborGasMix = allGasMix[neighborEntity];

						currentGasMix.Volume += neighborGasMix.Volume;

						//TransferAllGas from neighbor to current, can do this as this happens after main thread update
						//so there wont be conflicts (doesnt clear old mix, will get overriden later)
						AtmosUtils.TransferAllGas(ref neighborBuffer, ref neighborGasMix,
							ref currentGasDataBuffer, ref currentGasMix);
					}

					if (dividingCount == 1) return;

					//Note: this assumes the volume of all tiles are the same
					currentGasMix.Volume /= dividingCount;

					//Divide for neighbors
					currentGasDataBuffer.DivideAllGases(dividingCount);

					currentGasMix.Recalculate(currentGasDataBuffer);

					//Set neighbor mixes
					for (int j = 0; j < neighbors.Length; j++)
					{
						//We only need to check neighbours which can equalise
						if (neighborsEqualise[j] == false) continue;

						var neighborEntity = neighbors[j].NeighbourEntity;
						var neighborTileData = allUpdateData[neighborEntity];

						neighborTileData.Updated = true;

						//Wake the neighbor tile up
						ecb.RemoveComponent<DeactivatedTag>(batchIndex, neighborEntity);

						var neighbourMetaTile = allMetaDataTile[neighborEntity];

						//If we are space then we don't copy, instead empty
						if (neighbourMetaTile.IsSpace)
						{
							var buffer = allGasData[neighborEntity];
							buffer.Clear();
							allGasMix[neighborEntity] = new GasMixComponent(in buffer, 0, 0, AtmosDefines.SPACE_TEMPERATURE);
						}
						else
						{
							//They are structs so copy to old reference for neighbors
							allGasData[neighborEntity].CopyFrom(currentGasDataBuffer);
							allGasMix[neighborEntity] = currentGasMix;
						}

						allUpdateData[neighborEntity] = neighborTileData;
					}

					currentUpdateData.Updated = true;

					if (currentMetaData.IsSpace)
					{
						//If we are space then we don't copy, instead empty
						var buffer = allGasData[entity];
						buffer.Clear();
						allGasMix[entity] = new GasMixComponent(in buffer, 0, 0, AtmosDefines.SPACE_TEMPERATURE);
					}
					else
					{
						//They are structs so copy to old reference for neighbors
						allGasData[entity].CopyFrom(currentGasDataBuffer);
						allGasMix[entity] = currentGasMix;
					}

					allUpdateData[entity] = currentUpdateData;
	        }
        }
    }
}
