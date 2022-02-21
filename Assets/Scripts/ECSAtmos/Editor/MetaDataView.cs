using System;
using System.Collections.Generic;
using Systems.Atmospherics;
using ECSAtmos.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace ECSAtmos.Editor
{
	public class MetaDataView : BasicView
	{
		public static List<Check> localChecks = new List<Check>();

		static MetaDataView()
		{
			localChecks.Add(new MolesCheck());
			localChecks.Add(new PressureCheck());
			localChecks.Add(new TemperatureCheck());
			
			localChecks.Add(new TileTemperatureCheck());
			localChecks.Add(new ThermalConductivityCheck());
			localChecks.Add(new HeatCapacityCheck());
			localChecks.Add(new StartingSuperConductCheck());
			localChecks.Add(new AllowedToSuperConductCheck());
			
			localChecks.Add(new UpdateCheck());
			localChecks.Add(new TriedUpdateCheck());
		}

		public override void DrawContent()
		{
			for (var i = 0; i < localChecks.Count; i++)
			{
				Check check = localChecks[i];
				check.Active = GUILayout.Toggle(check.Active, check.Label);
			}
		}
		
		[DrawGizmo(GizmoType.Active | GizmoType.NonSelected)]
		private static void DrawGizmoLocal(TestingMono test, GizmoType gizmoType)
		{
			if(TestingMono.isPaused) return;
			
			GizmoUtils.DrawGizmos(localChecks);
		}

		private class MolesCheck : Check
		{
			public override string Label { get; } = "Total Moles";

			public override void DrawLabel(BoundsInt bounds)
			{
				if(World.DefaultGameObjectInjectionWorld?.EntityManager == null) return;
				
				var entities = World.DefaultGameObjectInjectionWorld.EntityManager.GetAllEntities(Allocator.Temp);

				foreach (var entity in entities)
				{
					if(entity.TryGetComponent<Translation>(out var trans) == false) continue;
					if(bounds.Contains(((Vector3)trans.Value).RoundToInt()) == false) continue;
					if(entity.TryGetComponent<GasMixComponent>(out var gas) == false) continue;
					
					GizmoUtils.DrawText($"{gas.Moles:0.###}", trans.Value, Color.cyan, false, 10);
				}

				entities.Dispose();
			}
		}
		
		private class PressureCheck : Check
		{
			public override string Label { get; } = "Pressure Check";

			public override void DrawLabel(BoundsInt bounds)
			{
				if(World.DefaultGameObjectInjectionWorld?.EntityManager == null) return;
				
				var entities = World.DefaultGameObjectInjectionWorld.EntityManager.GetAllEntities(Allocator.Temp);

				foreach (var entity in entities)
				{
					if(entity.TryGetComponent<Translation>(out var trans) == false) continue;
					if(bounds.Contains(((Vector3)trans.Value).RoundToInt()) == false) continue;
					if(entity.TryGetComponent<GasMixComponent>(out var gas) == false) continue;
					
					GizmoUtils.DrawText($"{gas.Pressure:0.###}", trans.Value, Color.cyan, false, 8);
				}

				entities.Dispose();
			}
		}
		
		private class TemperatureCheck : Check
		{
			public override string Label { get; } = "Temperature Check";

			public override void DrawLabel(BoundsInt bounds)
			{
				if(World.DefaultGameObjectInjectionWorld?.EntityManager == null) return;
				
				var entities = World.DefaultGameObjectInjectionWorld.EntityManager.GetAllEntities(Allocator.Temp);

				foreach (var entity in entities)
				{
					if(entity.TryGetComponent<Translation>(out var trans) == false) continue;
					if(bounds.Contains(((Vector3)trans.Value).RoundToInt()) == false) continue;
					if(entity.TryGetComponent<GasMixComponent>(out var gas) == false) continue;
					
					GizmoUtils.DrawText($"{gas.Temperature:0.###}", trans.Value, Color.cyan, false, 8);
				}

				entities.Dispose();
			}
		}
		
		#region Conductivity
		
		private class TileTemperatureCheck : Check
		{
			public override string Label { get; } = "Tile Temperature Check";

			public override void DrawLabel(BoundsInt bounds)
			{
				if(World.DefaultGameObjectInjectionWorld?.EntityManager == null) return;
				
				var entities = World.DefaultGameObjectInjectionWorld.EntityManager.GetAllEntities(Allocator.Temp);

				foreach (var entity in entities)
				{
					if(entity.TryGetComponent<Translation>(out var trans) == false) continue;
					if(bounds.Contains(((Vector3)trans.Value).RoundToInt()) == false) continue;
					if(entity.TryGetComponent<ConductivityComponent>(out var conductivityComponent) == false) continue;
					
					GizmoUtils.DrawText($"{conductivityComponent.ConductivityTemperature:0.###}", trans.Value, Color.cyan, false, 8);
				}

				entities.Dispose();
			}
		}
		
		private class ThermalConductivityCheck : Check
		{
			public override string Label { get; } = "Thermal Conductivity Check";

			public override void DrawLabel(BoundsInt bounds)
			{
				if(World.DefaultGameObjectInjectionWorld?.EntityManager == null) return;
				
				var entities = World.DefaultGameObjectInjectionWorld.EntityManager.GetAllEntities(Allocator.Temp);

				foreach (var entity in entities)
				{
					if(entity.TryGetComponent<Translation>(out var trans) == false) continue;
					if(bounds.Contains(((Vector3)trans.Value).RoundToInt()) == false) continue;
					if(entity.TryGetComponent<ConductivityComponent>(out var conductivityComponent) == false) continue;
					
					GizmoUtils.DrawText($"{conductivityComponent.ThermalConductivity:0.###}", trans.Value, Color.cyan, false, 8);
				}

				entities.Dispose();
			}
		}
		
		private class HeatCapacityCheck : Check
		{
			public override string Label { get; } = "Heat Capacity Check";

			public override void DrawLabel(BoundsInt bounds)
			{
				if(World.DefaultGameObjectInjectionWorld?.EntityManager == null) return;
				
				var entities = World.DefaultGameObjectInjectionWorld.EntityManager.GetAllEntities(Allocator.Temp);

				foreach (var entity in entities)
				{
					if(entity.TryGetComponent<Translation>(out var trans) == false) continue;
					if(bounds.Contains(((Vector3)trans.Value).RoundToInt()) == false) continue;
					if(entity.TryGetComponent<ConductivityComponent>(out var conductivityComponent) == false) continue;
					
					GizmoUtils.DrawText($"{conductivityComponent.HeatCapacity:0.###}", trans.Value, Color.cyan, false, 8);
				}

				entities.Dispose();
			}
		}
		
		private class StartingSuperConductCheck : Check
		{
			public override string Label { get; } = "StartingSuperConduct Check";

			public override void DrawLabel(BoundsInt bounds)
			{
				if(World.DefaultGameObjectInjectionWorld?.EntityManager == null) return;
				
				var entities = World.DefaultGameObjectInjectionWorld.EntityManager.GetAllEntities(Allocator.Temp);

				foreach (var entity in entities)
				{
					if(entity.TryGetComponent<Translation>(out var trans) == false) continue;
					if(bounds.Contains(((Vector3)trans.Value).RoundToInt()) == false) continue;
					if(entity.TryGetComponent<ConductivityComponent>(out var conductivityComponent) == false) continue;
					
					if (conductivityComponent.StartingSuperConduct)
					{
						GizmoUtils.DrawText($"True", trans.Value, Color.green, false, 10);
					}
					else
					{
						GizmoUtils.DrawText($"False", trans.Value, Color.red, false, 10);
					}
				}

				entities.Dispose();
			}
		}
		
		private class AllowedToSuperConductCheck : Check
		{
			public override string Label { get; } = "AllowedToSuperConduct Check";

			public override void DrawLabel(BoundsInt bounds)
			{
				if(World.DefaultGameObjectInjectionWorld?.EntityManager == null) return;
				
				var entities = World.DefaultGameObjectInjectionWorld.EntityManager.GetAllEntities(Allocator.Temp);

				foreach (var entity in entities)
				{
					if(entity.TryGetComponent<Translation>(out var trans) == false) continue;
					if(bounds.Contains(((Vector3)trans.Value).RoundToInt()) == false) continue;
					if(entity.TryGetComponent<ConductivityComponent>(out var conductivityComponent) == false) continue;
					
					if (conductivityComponent.AllowedToSuperConduct)
					{
						GizmoUtils.DrawText($"True", trans.Value, Color.green, false, 10);
					}
					else
					{
						GizmoUtils.DrawText($"False", trans.Value, Color.red, false, 10);
					};
				}

				entities.Dispose();
			}
		}
		
		#endregion

		private class UpdateCheck : Check
		{
			public override string Label { get; } = "Update check";

			public override void DrawLabel(BoundsInt bounds)
			{
				if (World.DefaultGameObjectInjectionWorld?.EntityManager == null) return;

				var entities = World.DefaultGameObjectInjectionWorld.EntityManager.GetAllEntities(Allocator.Temp);

				foreach (var entity in entities)
				{
					if (entity.TryGetComponent<Translation>(out var trans) == false) continue;
					if(bounds.Contains(((Vector3)trans.Value).RoundToInt()) == false) continue;
					if(entity.TryGetComponent<MetaDataTileComponent>(out var tileComponent) == false) continue;

					if (tileComponent.Updated)
					{
						GizmoUtils.DrawText($"True", trans.Value, Color.green, false, 10);
					}
					else
					{
						GizmoUtils.DrawText($"False", trans.Value, Color.red, false, 10);
					}
				}

				entities.Dispose();
			}
		}
		
		private class TriedUpdateCheck : Check
		{
			public override string Label { get; } = "Tried to Update check";

			public override void DrawLabel(BoundsInt bounds)
			{
				if (World.DefaultGameObjectInjectionWorld?.EntityManager == null) return;

				var entities = World.DefaultGameObjectInjectionWorld.EntityManager.GetAllEntities(Allocator.Temp);

				foreach (var entity in entities)
				{
					if (entity.TryGetComponent<Translation>(out var trans) == false) continue;
					if(bounds.Contains(((Vector3)trans.Value).RoundToInt()) == false) continue;
					if(entity.TryGetComponent<MetaDataTileComponent>(out var tileComponent) == false) continue;

					if (tileComponent.TriedToUpdate)
					{
						GizmoUtils.DrawText($"True", trans.Value, Color.green, false, 10);
					}
					else
					{
						GizmoUtils.DrawText($"False", trans.Value, Color.red, false, 10);
					}
				}

				entities.Dispose();
			}
		}
	}
}
