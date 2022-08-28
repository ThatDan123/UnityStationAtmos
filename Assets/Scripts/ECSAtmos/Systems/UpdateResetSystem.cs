using ECSAtmos.Components;
using ECSAtmos.Util;
using Systems.ECSAtmos.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace ECSAtmos.Systems
{
	[BurstCompile]
	[UpdateInGroup(typeof(AtmosSystemGroup))]
	[UpdateAfter(typeof(AtmosBeginningEntityCommandBufferSystem))]
	public partial class UpdateResetSystem : AtmosSystemBase
	{
		private EntityQuery query;

		protected override void OnCreate()
		{
			var queryDesc = new EntityQueryDesc
			{
				All = new ComponentType[]
				{
					typeof(AtmosUpdateDataComponent)
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
			UpdateResetJob job = new UpdateResetJob
			{
				offset = offset,

				entityTypeHandle = GetEntityTypeHandle(),

				allUpdateData = GetComponentDataFromEntity<AtmosUpdateDataComponent>()
			};

			return job.ScheduleParallel(query, inputDeps);
		}

		[BurstCompile]
		private struct UpdateResetJob : IJobEntityBatch
		{
			[ReadOnly]
			public OffsetLogic offset;

			[ReadOnly]
			public EntityTypeHandle entityTypeHandle;

			[NativeDisableParallelForRestriction]
			public ComponentDataFromEntity<AtmosUpdateDataComponent> allUpdateData;

			public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
			{
				for (int i = 0; i < batchInChunk.Count; ++i)
				{
					var entity = batchInChunk.GetNativeArray(entityTypeHandle)[i];

					var currentUpdateData = allUpdateData[entity];

					if (currentUpdateData.XUpdateID == offset.XUpdateID &&
					    currentUpdateData.YUpdateID == offset.YUpdateID)
					{
						currentUpdateData.Updated = false;
					}

					currentUpdateData.TriedToUpdate = false;

					allUpdateData[entity] = currentUpdateData;
				}
			}
		}
	}
}