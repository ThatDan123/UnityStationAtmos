using Unity.Entities;

namespace ECSAtmos.Systems
{
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	public class AtmosSystemGroup : ComponentSystemGroup
	{
		public float TickRate { get; set; }

		private float timer;

		public bool Active { get; set; }

		// AtmosSystemGroup Order:
		// AtmosBeginningEntityCommandBufferSystem
		// UpdateResetSystem
		// ConductivitySystem
		// TileConductivitySystem
		// PipeGasExchangeSystem
		// TileGasExchangeSystem
		// DeactivateSystem
		// AtmosEntityCommandBufferSystem
		// ReactionSystem

		protected override void OnUpdate()
		{
			if (Active == false) return;

			timer += Time.DeltaTime;

			if (timer < TickRate) return;
			timer = 0;

			base.OnUpdate();
		}
	}

	[UpdateInGroup(typeof(InitializationSystemGroup))]
	public class AtmosInitSystemGroup : ComponentSystemGroup
	{
		// AtmosInitSystemGroup Order:
		// ArchetypeSystem
	}
}