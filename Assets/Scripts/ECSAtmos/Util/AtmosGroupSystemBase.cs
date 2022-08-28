using ECSAtmos.Util;
using Unity.Jobs;

namespace Systems.ECSAtmos.Util
{
	public abstract class AtmosGroupSystemBase : JobSystemBase
	{
		public float TickRate { get; set; }

		private float timer;

		public bool Active { get; set; }

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			if (Active == false) return inputDeps;

			timer += Time.DeltaTime;

			if (timer < TickRate) return inputDeps;
			timer = 0;

			return Update(inputDeps);
		}

		protected abstract JobHandle Update(JobHandle inputDeps);
	}
}