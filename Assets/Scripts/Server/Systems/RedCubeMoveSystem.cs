// using Server.Components;
// using Unity.Entities;
// using Unity.Mathematics;
// using Unity.Transforms;
//
// namespace Server.Systems
// {
//     public class RedCubeMoveSystem : SystemBase
//     {
//         protected override void OnUpdate()
//         {
//             Entities.WithAll<RedCubeComponent>().ForEach((ref Translation translation) =>
//             {
//                 var random = new Random(1);
//                 translation.Value += new float3(random.NextFloat(0, 1), random.NextFloat(0, 1), 0);
//             }).ScheduleParallel();
//         }
//     }
// }
