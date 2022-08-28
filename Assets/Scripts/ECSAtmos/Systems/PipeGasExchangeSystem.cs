using ECSAtmos.Components;
using ECSAtmos.Util;
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
	[UpdateAfter(typeof(TileConductivitySystem))]
	public partial class PipeGasExchangeSystem : AtmosSystemBase
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
			        typeof(GasMixComponent),
			        typeof(NeighbourBuffer),
			        typeof(GasDataBuffer),
			        typeof(PipeAtmosTag)
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
	        PipeGasExchangeJob job = new PipeGasExchangeJob
	        {
		        pipeNeighborsHandle = GetBufferTypeHandle<NeighbourBuffer>(true),

		        entityTypeHandle = GetEntityTypeHandle(),

		        offset = offset,

		        allGasMix = GetComponentDataFromEntity<GasMixComponent>(),
		        allUpdateData = GetComponentDataFromEntity<AtmosUpdateDataComponent>(),
		        allGasData = GetBufferFromEntity<GasDataBuffer>(),
		        ecb = commandBufferSystem.CreateCommandBuffer().AsParallelWriter()
	        };

	        inputDeps = job.ScheduleParallel(query, inputDeps);

	        commandBufferSystem.AddJobHandleForProducer(inputDeps);

	        return inputDeps;
        }

        [BurstCompile]
        private struct PipeGasExchangeJob : IJobEntityBatch
        {
	        [ReadOnly]
	        public BufferTypeHandle<NeighbourBuffer> pipeNeighborsHandle;

	        [ReadOnly]
	        public EntityTypeHandle entityTypeHandle;

	        [ReadOnly]
	        public OffsetLogic offset;

	        //Writing and Reading from these, this is thread safe as tiles are only access once in an area so no conflicts
	        [NativeDisableParallelForRestriction]
	        public ComponentDataFromEntity<GasMixComponent> allGasMix;

	        [NativeDisableParallelForRestriction]
	        public BufferFromEntity<GasDataBuffer> allGasData;

	        [NativeDisableParallelForRestriction]
	        public ComponentDataFromEntity<AtmosUpdateDataComponent> allUpdateData;

	        [NativeDisableParallelForRestriction]
	        public EntityCommandBuffer.ParallelWriter ecb;

	        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
	        {
		        BufferAccessor<NeighbourBuffer> pipeNeighbors = batchInChunk.GetBufferAccessor(this.pipeNeighborsHandle);

		        for (int i = 0; i < batchInChunk.Count; ++i)
		        {
			        var entity = batchInChunk.GetNativeArray(entityTypeHandle)[i];

			        var currentUpdateData = allUpdateData[entity];

			        //This basically means that every forth pipe is allowed to do an update
			        //But we alternate which ones every step (I would draw a shitty paint diagram to explain but i cant draw)
			        //Basically means every forth update every pipe will have done an update
			        //NOTE: because of this we don't update two pipe in the same frame therefore can disable writing safeties in the job
			        if (currentUpdateData.XUpdateID != offset.XUpdateID || currentUpdateData.YUpdateID != offset.YUpdateID) continue;

			        currentUpdateData.TriedToUpdate = true;
			        allUpdateData[entity] = currentUpdateData;

			        var neighbors = pipeNeighbors[i];

			        //No neighbors so don't bother
			        if(neighbors.Length == 0) continue;

			        var currentGasMix = allGasMix[entity];
			        var currentGasDataBuffer = allGasData[entity];

			        var isPressureChanged = PressureCheck(in neighbors, ref allGasMix, ref allGasData, ref currentGasMix,
				        ref currentGasDataBuffer);

			        if (isPressureChanged == false)
					{
						//If no pressure change is required as all neighbors are equal sleep pipe
						ecb.AddComponent<DeactivatedTag>(batchIndex, entity);
						continue;
					}

					//Now to equalise the gases
					Equalise(in neighbors, ref allGasMix, ref allUpdateData, ref allGasData,
						ref currentGasMix, ref currentGasDataBuffer, ref entity, ref currentUpdateData, ref ecb, in batchIndex);
		        }
	        }

	        private static bool PressureCheck(
		        in DynamicBuffer<NeighbourBuffer> neighbors,
		        ref ComponentDataFromEntity<GasMixComponent> allGasMix,
		        ref BufferFromEntity<GasDataBuffer> allGasData,
		        ref GasMixComponent currentGasMix,
		        ref DynamicBuffer<GasDataBuffer> currentGasDataBuffer)
	        {
		        for (var k = 0; k < neighbors.Length; k++)
				{
					var neighborEntity = neighbors[k].NeighbourEntity;
					var neighborGasMix = allGasMix[neighborEntity];

					float pressureDifference = currentGasMix.Pressure - neighborGasMix.Pressure;
					float absoluteDifference = math.abs(pressureDifference);

					//Check to see if theres a large pressure difference
					if (absoluteDifference > AtmosConstants.MinPressureDifference)
					{
						return true;
					}

					//Check if the moles are different. (e.g. CO2 is different from breathing)
					//Check current node then check neighbor so we dont miss a gas if its only on one of the nodes
					var neighborBuffer = allGasData[neighborEntity];

					//Current node
					//Only need to check if false
					for (int j = currentGasDataBuffer.Length - 1; j >= 0; j--)
					{
						var gas = currentGasDataBuffer[j].GasData;
						var moles = gas.Moles;
						float molesNeighbor = neighborBuffer.GetMoles(gas.GasSO);

						if (math.abs(moles - molesNeighbor) > AtmosConstants.MinPressureDifference)
						{
							return true;
						}
					}

					//Neighbor node
					//Only need to check if false
					for (int j = 0; j < neighborBuffer.Length; j++)
					{
						var gas = neighborBuffer[j].GasData;
						float molesNeighbor = gas.Moles;
						float moles = currentGasDataBuffer.GetMoles(gas.GasSO);

						if (math.abs(moles - molesNeighbor) > AtmosConstants.MinPressureDifference)
						{
							return true;
						}
					}
		        }

				return false;
	        }

	        private static void Equalise(
		        in DynamicBuffer<NeighbourBuffer> neighbors,
		        ref ComponentDataFromEntity<GasMixComponent> allGasMix,
		        ref ComponentDataFromEntity<AtmosUpdateDataComponent> allUpdateData,
		        ref BufferFromEntity<GasDataBuffer> allGasData,
		        ref GasMixComponent currentGasMix,
		        ref DynamicBuffer<GasDataBuffer> currentGasDataBuffer,
		        ref Entity entity,
		        ref AtmosUpdateDataComponent currentUpdateData,
		        ref EntityCommandBuffer.ParallelWriter ecb,
		        in int batchIndex)
	        {
					var dividingCount = 1;

					//Add all the gases
					for (int j = 0; j < neighbors.Length; j++)
					{
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
						var neighborEntity = neighbors[j].NeighbourEntity;
						var neighborTileData = allUpdateData[neighborEntity];

						neighborTileData.Updated = true;

						//Wake the neighbor tile up
						ecb.RemoveComponent<DeactivatedTag>(batchIndex, neighborEntity);

						//They are structs so copy to old reference for neighbors
						allGasData[neighborEntity].CopyFrom(currentGasDataBuffer);
						allGasMix[neighborEntity] = currentGasMix;
						allUpdateData[neighborEntity] = neighborTileData;
					}

					currentUpdateData.Updated = true;

					//They are structs so copy to old reference for this tile
					allGasData[entity].CopyFrom(currentGasDataBuffer);
					allGasMix[entity] = currentGasMix;
					allUpdateData[entity] = currentUpdateData;
	        }
        }
	}
}