// using System;
// using Server.Components;
// using Unity.Entities;
// using Unity.NetCode;
//
// namespace Server.Systems
// {
//     [UpdateInGroup(typeof(ServerSimulationSystemGroup))]
//     public class RedCubeSpawnSystem : SystemBase
//     {
//         private int maxCubes = 100;
//         private int currentCubes = 0;
//         
//         private float timer = 0;
//
//         private Entity redCube;
//
//         protected override void OnStartRunning()
//         {
//             var ghostCollection = GetSingletonEntity<GhostPrefabCollectionComponent>();
//             var prefabs = EntityManager.GetBuffer<GhostPrefabBuffer>(ghostCollection);
//             for (int ghostId = 0; ghostId < prefabs.Length; ++ghostId)
//             {
//                 if (EntityManager.HasComponent<RedCubeComponent>(prefabs[ghostId].Value))
//                 {
//                     redCube = prefabs[ghostId].Value;
//                     break;
//                 }
//             }
//         }
//
//         protected override void OnUpdate()
//         {
//             if(currentCubes >= maxCubes) return;
//
//             if (redCube == Entity.Null)
//             {
//                 throw new NullReferenceException();
//             }
//             
//             timer += Time.DeltaTime;
//             
//             if(timer < 5) return;
//             timer = 0;
//             
//             currentCubes++;
//             
//             EntityManager.Instantiate(redCube);
//         }
//     }
// }
