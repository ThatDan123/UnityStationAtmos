using ECSAtmos.Components;
using ECSAtmos.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace ECSAtmos.Systems
{
	[BurstCompile]
	[UpdateInGroup(typeof(AtmosSystemGroup))]
	[UpdateAfter(typeof(AtmosOffsetSystem))]
	public partial struct UpdateResetSystem : ISystem
	{
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<AtmosUpdateDataComponent>().WithNone<DeactivatedTag>().Build());
			
			state.RequireForUpdate<AtmosOffsetSingleton>();
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state) { }

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			state.EntityManager.CompleteDependencyBeforeRO<AtmosOffsetSingleton>();
			var offsetSingleton = SystemAPI.GetSingleton<AtmosOffsetSingleton>();
			
			var job = new UpdateResetJob
			{
				offset = offsetSingleton.Offset
			}.ScheduleParallel(state.Dependency);

			state.Dependency = job;
		}

		[BurstCompile]
		[WithNone(typeof(DeactivatedTag))]
		[WithAll(typeof(AtmosUpdateDataComponent))]
		private partial struct UpdateResetJob : IJobEntity
		{
			[ReadOnly]
			public OffsetLogic offset;

			private void Execute(ref AtmosUpdateDataComponent atmosUpdateDataComponent)
			{
				if (atmosUpdateDataComponent.XUpdateID == offset.XUpdateID &&
				    atmosUpdateDataComponent.YUpdateID == offset.YUpdateID)
				{
					atmosUpdateDataComponent.Updated = false;
				}

				atmosUpdateDataComponent.TriedToUpdate = false;
			}
		}
	}
}