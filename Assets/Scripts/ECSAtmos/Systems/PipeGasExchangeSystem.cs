using ECSAtmos.Components;
using Systems.ECSAtmos;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECSAtmos.Systems
{
	[BurstCompile]
	[UpdateInGroup(typeof(AtmosSystemGroup))]
	[UpdateAfter(typeof(TileConductivitySystem))]
	public partial struct PipeGasExchangeSystem : ISystem
	{
		private EntityQuery query;
		
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
	        query = SystemAPI.QueryBuilder()
		        .WithAll<AtmosUpdateDataComponent, AtmosTileOffsetShared, GasMixComponent, NeighbourBuffer, 
			        GasDataBuffer, PipeAtmosTag>()
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
			
	        var job = new PipeGasExchangeJob
	        {
		        PipeNeighbors = SystemAPI.GetBufferLookup<NeighbourBuffer>(true),

		        AllGasMix = SystemAPI.GetComponentLookup<GasMixComponent>(),
		        AllUpdateData = SystemAPI.GetComponentLookup<AtmosUpdateDataComponent>(),
		        AllGasData = SystemAPI.GetBufferLookup<GasDataBuffer>(),
		        Ecb = ecb.AsParallelWriter()
	        }.ScheduleParallel(query, state.Dependency);

	        state.Dependency = job;
        }

        [BurstCompile]
        private partial struct PipeGasExchangeJob : IJobEntity
        {
	        [ReadOnly]
	        public BufferLookup<NeighbourBuffer> PipeNeighbors;

	        //Writing and Reading from these, this is thread safe as tiles are only access once in an area so no conflicts
	        [NativeDisableParallelForRestriction]
	        public ComponentLookup<GasMixComponent> AllGasMix;

	        [NativeDisableParallelForRestriction]
	        public BufferLookup<GasDataBuffer> AllGasData;

	        [NativeDisableParallelForRestriction]
	        public ComponentLookup<AtmosUpdateDataComponent> AllUpdateData;
	        
	        public EntityCommandBuffer.ParallelWriter Ecb;

	        private void Execute([ChunkIndexInQuery] int index, in Entity entity)
	        {
		        var currentUpdateData = AllUpdateData[entity];

		        currentUpdateData.TriedToUpdate = true;
		        AllUpdateData[entity] = currentUpdateData;

		        var neighbors = PipeNeighbors[entity];

		        //No neighbors so don't bother
		        if(neighbors.Length == 0) return;

		        var currentGasMix = AllGasMix[entity];
		        var currentGasDataBuffer = AllGasData[entity];

		        var isPressureChanged = PressureCheck(in neighbors, ref AllGasMix, ref AllGasData, ref currentGasMix,
			        ref currentGasDataBuffer);

		        if (isPressureChanged == false)
		        {
			        //If no pressure change is required as all neighbors are equal sleep pipe
			        Ecb.AddComponent<DeactivatedTag>(index, entity);
			        return;
		        }

		        //Now to equalise the gases
		        Equalise(in neighbors, ref AllGasMix, ref AllUpdateData, ref AllGasData,
			        ref currentGasMix, ref currentGasDataBuffer, in entity, ref currentUpdateData, ref Ecb, in index);
	        }

	        private static bool PressureCheck(
		        in DynamicBuffer<NeighbourBuffer> neighbors,
		        ref ComponentLookup<GasMixComponent> allGasMix,
		        ref BufferLookup<GasDataBuffer> allGasData,
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
		        ref ComponentLookup<GasMixComponent> allGasMix,
		        ref ComponentLookup<AtmosUpdateDataComponent> allUpdateData,
		        ref BufferLookup<GasDataBuffer> allGasData,
		        ref GasMixComponent currentGasMix,
		        ref DynamicBuffer<GasDataBuffer> currentGasDataBuffer,
		        in Entity entity,
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