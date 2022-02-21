using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace ECSAtmos.Util
{
    public abstract class JobSystemBase : SystemBase
    {
        protected override void OnUpdate() 
        {
            this.Dependency = OnUpdate(this.Dependency);
        }
 
        protected abstract JobHandle OnUpdate(JobHandle inputDeps);
    }
}
