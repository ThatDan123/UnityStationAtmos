// using Shared.Components.RPC;
// using Unity.Entities;
// using Unity.NetCode;
//
// namespace Client.Systems
// {
//     // When client has a connection with network id, go in game and tell server to also go in game
//     [UpdateInGroup(typeof(ClientSimulationSystemGroup))]
//     public class GoInGameClientSystem : ComponentSystem
//     {
//         protected override void OnCreate()
//         {
//         }
//
//         protected override void OnUpdate()
//         {
//             Entities.WithNone<NetworkStreamInGame>().ForEach((Entity ent, ref NetworkIdComponent id) =>
//             {
//                 PostUpdateCommands.AddComponent<NetworkStreamInGame>(ent);
//                 var req = PostUpdateCommands.CreateEntity();
//                 PostUpdateCommands.AddComponent<GoInGameRequest>(req);
//                 PostUpdateCommands.AddComponent(req, new SendRpcCommandRequestComponent { TargetConnection = ent });
//             });
//         }
//     }
// } 
