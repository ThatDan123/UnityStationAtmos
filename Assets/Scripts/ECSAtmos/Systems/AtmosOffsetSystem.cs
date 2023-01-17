using ECSAtmos.Util;
using Unity.Burst;
using Unity.Entities;

namespace ECSAtmos.Systems
{
    public struct AtmosOffsetSingleton : IComponentData
    {
        public OffsetLogic Offset;
    }
    
    [BurstCompile]
    [UpdateInGroup(typeof(AtmosSystemGroup))]
    [UpdateAfter(typeof(AtmosBeginningEntityCommandBufferSystem))]
    public partial struct AtmosOffsetSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.CreateSingleton(new AtmosOffsetSingleton());
            
            state.RequireForUpdate<AtmosOffsetSingleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteDependencyBeforeRW<AtmosOffsetSingleton>();

            ref var offsetSingleton = ref SystemAPI.GetSingletonRW<AtmosOffsetSingleton>().ValueRW;
            offsetSingleton.Offset.DoStep();
        }
    }
}