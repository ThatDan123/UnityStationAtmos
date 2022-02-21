// using System;
// using Shared.Components;
// using Shared.Components.RPC;
// using Unity.Entities;
// using Unity.NetCode;
//
// namespace Server.Systems
// {
//     // When server receives go in game request, go in game and delete request
//     [UpdateInGroup(typeof(ServerSimulationSystemGroup))]
//     public class GoInGameServerSystem : ComponentSystem
//     {
//         protected override void OnUpdate()
//         {
//             Entities.WithNone<SendRpcCommandRequestComponent>().ForEach((Entity reqEnt, ref GoInGameRequest req, ref ReceiveRpcCommandRequestComponent reqSrc) =>
//             {
//                 PostUpdateCommands.AddComponent<NetworkStreamInGame>(reqSrc.SourceConnection);
//                 UnityEngine.Debug.Log(String.Format("Server setting connection {0} to in game", EntityManager.GetComponentData<NetworkIdComponent>(reqSrc.SourceConnection).Value));
//                 var ghostCollection = GetSingletonEntity<GhostPrefabCollectionComponent>();
//                 var prefab = Entity.Null;
//                 var prefabs = EntityManager.GetBuffer<GhostPrefabBuffer>(ghostCollection);
//                 for (int ghostId = 0; ghostId < prefabs.Length; ++ghostId)
//                 {
//                     if (EntityManager.HasComponent<MovableCubeComponent>(prefabs[ghostId].Value))
//                         prefab = prefabs[ghostId].Value;
//                 }
//                 
//                 var player = EntityManager.Instantiate(prefab);
//                 EntityManager.SetComponentData(player, new GhostOwnerComponent { NetworkId = EntityManager.GetComponentData<NetworkIdComponent>(reqSrc.SourceConnection).Value});
//                 PostUpdateCommands.AddBuffer<CubeInput>(player);
//
//                 PostUpdateCommands.SetComponent(reqSrc.SourceConnection, new CommandTargetComponent {targetEntity = player});
//
//                 PostUpdateCommands.DestroyEntity(reqEnt);
//             });
//         }
//     }
// }
//
