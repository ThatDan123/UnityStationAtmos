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
	[UpdateAfter(typeof(TileGasExchangeSystem))]
	public class DeactivateSystem : AtmosSystemBase
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
					typeof(ConductivityComponent)
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
			DeactivateJob job = new DeactivateJob
			{
				entityTypeHandle = GetEntityTypeHandle(),

				offset = offset,

				allUpdateData = GetComponentDataFromEntity<AtmosUpdateDataComponent>(true),
				allConductivity = GetComponentDataFromEntity<ConductivityComponent>(true),
				ecb = commandBufferSystem.CreateCommandBuffer().AsParallelWriter()
			};

			inputDeps = job.ScheduleParallel(query, inputDeps);

			commandBufferSystem.AddJobHandleForProducer(inputDeps);

			return inputDeps;
		}

		[BurstCompile]
		private struct DeactivateJob : IJobEntityBatch
		{
			[ReadOnly]
			public EntityTypeHandle entityTypeHandle;

			[ReadOnly]
			public OffsetLogic offset;

			[ReadOnly]
			[NativeDisableParallelForRestriction]
			public ComponentDataFromEntity<AtmosUpdateDataComponent> allUpdateData;

			[ReadOnly]
			[NativeDisableParallelForRestriction]
			public ComponentDataFromEntity<ConductivityComponent> allConductivity;

			[NativeDisableParallelForRestriction]
			public EntityCommandBuffer.ParallelWriter ecb;

			public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
			{
				for (int i = 0; i < batchInChunk.Count; ++i)
				{
					var entity = batchInChunk.GetNativeArray(entityTypeHandle)[i];

					var currentUpdateData = allUpdateData[entity];

					//This basically means that every forth tile is allowed to do an update
					//But we alternate which ones every step (I would draw a shitty paint diagram to explain but i cant draw)
					//Basically means every forth update every tile will have done an update
					//NOTE: because of this we don't update two tiles in the same frame therefore can disable writing safeties in the job
					if (currentUpdateData.XUpdateID != offset.XUpdateID || currentUpdateData.YUpdateID != offset.YUpdateID) continue;

					var currentConductivity = allConductivity[entity];

					//If atmos tile updated then don't deactivate
					if(currentUpdateData.Updated) continue;

					//If heat conducting then don't deactivate
					if(currentConductivity.StartingSuperConduct || currentConductivity.AllowedToSuperConduct) continue;

					ecb.AddComponent<DeactivatedTag>(batchIndex, entity);
				}
			}
		}
	}
}