using ECSAtmos.Util;
using Unity.Jobs;

namespace Systems.ECSAtmos.Util
{
	public abstract class AtmosSystemBase : JobSystemBase
	{
		private OffsetLogic offset;

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var update = Update(inputDeps, offset);

			//Increase offset for next update
			offset.DoStep();

			return update;
		}

		protected abstract JobHandle Update(JobHandle inputDeps, OffsetLogic offset);
	}
}