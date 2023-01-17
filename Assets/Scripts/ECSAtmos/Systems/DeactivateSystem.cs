using ECSAtmos.Components;
using Unity.Burst;
using Unity.Entities;

namespace ECSAtmos.Systems
{
	[BurstCompile]
	[UpdateInGroup(typeof(AtmosSystemGroup))]
	[UpdateAfter(typeof(TileGasExchangeSystem))]
	public partial struct DeactivateSystem : ISystem
	{
		private EntityQuery query;
		
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			query = SystemAPI.QueryBuilder().WithAll<AtmosUpdateDataComponent, ConductivityComponent, AtmosTileOffsetShared>().WithNone<DeactivatedTag>().Build();
			
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
			
			var job = new DeactivateJob
			{
				Ecb = ecb.AsParallelWriter()
			}.ScheduleParallel(query, state.Dependency);

			state.Dependency = job;
		}

		[BurstCompile]
		private partial struct DeactivateJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;

			private void Execute([ChunkIndexInQuery] int index, in Entity entity, 
				in AtmosUpdateDataComponent atmosUpdateDataComponent, in ConductivityComponent conductivityComponent)
			{
				//If atmos tile updated then don't deactivate
				if(atmosUpdateDataComponent.Updated) return;

				//If heat conducting then don't deactivate
				if(conductivityComponent.StartingSuperConduct || conductivityComponent.AllowedToSuperConduct) return;

				Ecb.AddComponent<DeactivatedTag>(index, entity);
			}
		}
	}
}