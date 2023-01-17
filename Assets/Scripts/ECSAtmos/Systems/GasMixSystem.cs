using ECSAtmos.Components;
using ECSAtmos.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace ECSAtmos.Systems
{
    // [BurstCompile]
    // [UpdateInGroup(typeof(AtmosSystemGroup))]
    // [UpdateAfter(typeof(UpdateResetSystem))]
    // public partial class GasMixSystem : JobSystemBase
    // {
	   //  private EntityQuery query;
    //
	   //  protected override void OnCreate()
	   //  {
		  //   var queryDesc = new EntityQueryDesc
		  //   {
			 //    All = new ComponentType[]
			 //    {
				//     typeof(GasMixComponent),
				//     typeof(GasDataBuffer)
			 //    },
    //
			 //    None = new ComponentType[]
			 //    {
				//     typeof(DeactivatedTag)
			 //    }
		  //   };
    //
		  //   query = GetEntityQuery(queryDesc);
	   //  }
    //
	   //  protected override JobHandle OnUpdate(JobHandle inputDeps)
	   //  {
		  //   return inputDeps;
    //
		  //   RecalculateJob job = new RecalculateJob
		  //   {
			 //    entityTypeHandle = GetEntityTypeHandle(),
		  //   
			 //    allGasMix = GetComponentDataFromEntity<GasMixComponent>(),
			 //    allGasData = GetBufferFromEntity<GasDataBuffer>()
		  //   };
		  //   
		  //   return job.ScheduleParallel(query, inputDeps);
	   //  }
    //
	   //  [BurstCompile]
	   //  public struct RecalculateJob : IJobEntity
	   //  {
		  //   [ReadOnly]
		  //   public EntityTypeHandle entityTypeHandle;
	   //  
		  //   [NativeDisableParallelForRestriction]
		  //   public ComponentLookup<GasMixComponent> allGasMix;
	   //  
		  //   [NativeDisableParallelForRestriction]
		  //   public BufferLookup<GasDataBuffer> allGasData;
	   //  
		  //   public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
		  //   {
			 //    for (int i = 0; i < batchInChunk.Count; ++i)
			 //    {
				//     var entity = batchInChunk.GetNativeArray(entityTypeHandle)[i];
				//     var currentGasMixComponent = allGasMix[entity];
				//     var currentGasDataBuffer = allGasData[entity];
	   //  
				//     currentGasMixComponent.Recalculate(in currentGasDataBuffer);
				//     allGasMix[entity] = currentGasMixComponent;
			 //    }
		  //   }
	   //  }
    // }
}
