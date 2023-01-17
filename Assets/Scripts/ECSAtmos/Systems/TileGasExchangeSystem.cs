using ECSAtmos.Components;
using Systems.Atmospherics;
using Systems.ECSAtmos;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECSAtmos.Systems
{
	[BurstCompile]
	[UpdateInGroup(typeof(AtmosSystemGroup))]
    [UpdateAfter(typeof(PipeGasExchangeSystem))]
    public partial struct TileGasExchangeSystem : ISystem
    {
	    private EntityQuery query;
	    
	    [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
	        query = SystemAPI.QueryBuilder()
		        .WithAll<AtmosUpdateDataComponent, AtmosTileOffsetShared, MetaDataTileComponent, NeighbourBuffer, 
			        GasMixComponent, GasDataBuffer, TileAtmosTag>()
		        .WithNone<DeactivatedTag>().Build();
	        
	        state.RequireForUpdate(query);
	        
	        //This is fine and won't block the system running as it is Added after the RequireForUpdate
	        query.AddSharedComponentFilter(new AtmosTileOffsetShared());
	        
	        state.RequireForUpdate<AtmosOffsetSingleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
	        state.EntityManager.CompleteDependencyBeforeRO<AtmosOffsetSingleton>();
	        var offsetSingleton = SystemAPI.GetSingleton<AtmosOffsetSingleton>();
	        
	        //This basically means that every forth tile is allowed to do an update
	        //But we alternate which ones every step (I would draw a shitty paint diagram to explain but i cant draw)
	        //Basically means every forth update every tile will have done an update
	        //NOTE: because of this we don't update two tiles in the same frame therefore can disable writing safeties in the job
	        query.SetSharedComponentFilter(new AtmosTileOffsetShared(offsetSingleton.Offset));

	        var ecbSystem = SystemAPI.GetSingleton<AtmosEndEntityCommandBufferSystem.Singleton>();
	        var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
	        
	        var jobHandle = new TileGasExchangeJob
	        {
		        MetaDataTileNeighbor = SystemAPI.GetBufferLookup<NeighbourBuffer>(true),
		        
		        AllGasMix = SystemAPI.GetComponentLookup<GasMixComponent>(),

		        AllMetaDataTile = SystemAPI.GetComponentLookup<MetaDataTileComponent>(true),

		        AllGasData = SystemAPI.GetBufferLookup<GasDataBuffer>(),
		        AllUpdateData = SystemAPI.GetComponentLookup<AtmosUpdateDataComponent>(),
		        
		        Ecb = ecb.AsParallelWriter()
	        }.ScheduleParallel(query, state.Dependency);

	        state.Dependency = jobHandle;
        }

        [BurstCompile]
        private partial struct TileGasExchangeJob : IJobEntity
        {
	        [ReadOnly]
	        public BufferLookup<NeighbourBuffer> MetaDataTileNeighbor;

	        //Writing and Reading from these, this is thread safe as tiles are only access once in an area so no conflicts
	        [NativeDisableParallelForRestriction]
	        public ComponentLookup<GasMixComponent> AllGasMix;

	        [NativeDisableParallelForRestriction]
	        public BufferLookup<GasDataBuffer> AllGasData;

	        [ReadOnly]
	        [NativeDisableParallelForRestriction]
	        public ComponentLookup<MetaDataTileComponent> AllMetaDataTile;

	        [NativeDisableParallelForRestriction]
	        public ComponentLookup<AtmosUpdateDataComponent> AllUpdateData;
	        
	        public EntityCommandBuffer.ParallelWriter Ecb;

	        private void Execute([ChunkIndexInQuery] int index, in Entity entity)
	        {
				var currentUpdateData = AllUpdateData[entity];

		        currentUpdateData.TriedToUpdate = true;

		        AllUpdateData[entity] = currentUpdateData;

		        var currentMetaDataTile = AllMetaDataTile[entity];

		        //We only need to check open tiles
		        if (currentMetaDataTile.IsIsolatedNode) return;

		        //We only need to check non-solid tiles
		        if (currentMetaDataTile.IsSolid) return;

		        var neighbors = MetaDataTileNeighbor[entity];

		        //No neighbors so don't bother
		        if(neighbors.Length == 0) return;

		        var neighborsEqualise = new NativeArray<bool>(neighbors.Length, Allocator.Temp);
		        var boolShouldEqualise = false;

		        for (int j = neighbors.Length - 1; j >= 0; j--)
		        {
			        var neighbour = neighbors[j];

			        //TODO might need to check neighbour still exists?
			        var neighbourMetaDataTile = AllMetaDataTile[neighbour.NeighbourEntity];

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
		        if(boolShouldEqualise == false) return;

		        var currentGasMix = AllGasMix[entity];
		        var currentGasDataBuffer = AllGasData[entity];

		        //TODO out the wind stuff
		        var isPressureChanged = PressureCheck(in neighbors, in neighborsEqualise, ref AllGasMix,
			        in AllMetaDataTile, ref AllGasData,
			        ref currentGasMix, ref currentGasDataBuffer, in currentMetaDataTile.TileLocalPos);

				if (isPressureChanged == false)
				{
					//TODO queue wind event, use ECB?
					return;
				}

				//Now to equalise the gases
				Equalise(in neighbors, in neighborsEqualise, ref AllGasMix, ref AllUpdateData, in AllMetaDataTile, ref AllGasData,
					ref currentGasMix, ref currentGasDataBuffer, in entity, in currentMetaDataTile, ref currentUpdateData, ref Ecb, in index);
	        }

	        private static bool PressureCheck(
		        in DynamicBuffer<NeighbourBuffer> neighbors,
		        in NativeArray<bool> neighborsEqualise,
		        ref ComponentLookup<GasMixComponent> allGasMix,
		        in ComponentLookup<MetaDataTileComponent> allMetaDataTile,
		        ref BufferLookup<GasDataBuffer> allGasData,
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
		        ref ComponentLookup<GasMixComponent> allGasMix,
		        ref ComponentLookup<AtmosUpdateDataComponent> allUpdateData,
		        in ComponentLookup<MetaDataTileComponent> allMetaDataTile,
		        ref BufferLookup<GasDataBuffer> allGasData,
		        ref GasMixComponent currentGasMix,
		        ref DynamicBuffer<GasDataBuffer> currentGasDataBuffer,
		        in Entity entity,
		        in MetaDataTileComponent currentMetaData,
		        ref AtmosUpdateDataComponent currentUpdateData,
		        ref EntityCommandBuffer.ParallelWriter ecb,
		        in int index)
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
						ecb.RemoveComponent<DeactivatedTag>(index, neighborEntity);

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
