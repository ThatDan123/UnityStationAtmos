using System.Collections.Generic;
using Systems.Atmospherics;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace ECSAtmos.Editor
{
    public class EntityView : BasicView
    {
        public static List<Check> localChecks = new List<Check>();

        static EntityView()
        {
            localChecks.Add(new EntityId());
        }
        
        private class EntityId : Check
        {
            public override string Label { get; } = "Entity Id";

            public override void DrawLabel(BoundsInt bounds)
            {
                if (World.DefaultGameObjectInjectionWorld?.EntityManager == null) return;

                var entities = World.DefaultGameObjectInjectionWorld.EntityManager.GetAllEntities(Allocator.Temp);

                foreach (var entity in entities)
                {
                    if (entity.TryGetComponent<Translation>(out var trans) == false) continue;
                    if(bounds.Contains(((Vector3)trans.Value).RoundToInt()) == false) continue;

                    GizmoUtils.DrawText($"{entity.Index}, {entity.Version}", trans.Value, Color.red, false, 10);
                }

                entities.Dispose();
            }
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
    }
}