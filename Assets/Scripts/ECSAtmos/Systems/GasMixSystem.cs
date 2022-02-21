using ECSAtmos.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ECSAtmos.Systems
{
    [BurstCompile]
    public class GasMixSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref GasMixComponent gasMixComponent, in DynamicBuffer<GasDataBuffer> gasDataBuffer) =>
            {
                gasMixComponent.ReCalculate(in gasDataBuffer);
                
            }).ScheduleParallel();
        }
    }
}
