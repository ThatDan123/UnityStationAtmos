// using ECSAtmos.Components;
// using ECSAtmos.Util;
// using Unity.Burst;
// using Unity.Collections;
// using Unity.Entities;
// using Unity.Mathematics;
// using Unity.Transforms;
//
// namespace ECSAtmos.Systems
// {
//     [BurstCompile]
//     [UpdateInGroup(typeof(AtmosSystemGroup))]
//     [UpdateAfter(typeof(ArchetypeSystem))]
//     public partial class TileNeighbourSystem : SystemBase
//     {
//         private EntityQuery query;
//
//         private OffsetLogic offset;
//
//         private float timer;
//         protected override void OnUpdate()
//         {
//             return;
//
//             timer += Time.DeltaTime;
//             if (timer < 0.2f) return;
//             timer = 0;
//
//             var entitiesInQuery = query.CalculateEntityCount();
//
//             NativeArray<Data> floatArray = new NativeArray<Data>(entitiesInQuery, Allocator.TempJob);
//
//             Entities.ForEach((Entity entity,
//                     int entityInQueryIndex,
//                     in LocalToParent localToParent,
//                     in LocalToWorld localToWorld,
//                     in Parent parent) =>
//                 {
// 	                floatArray[entityInQueryIndex] = new Data()
//                     {
// 	                    LocalPos = new int3(localToParent.Position),
// 	                    WorldPos = new int3(localToWorld.Position),
// 	                    Parent = parent.Value,
// 	                    Entity = entity
//                     };
//
//                 }).WithAll<NeighbourBuffer>()
//                 .WithStoreEntityQueryInField(ref query)
//                 .ScheduleParallel();
//
//             Dependency.Complete();
//
//             var offSet = offset;
//
//             Entities.ForEach((Entity entity,
//                     ref DynamicBuffer<NeighbourBuffer> metaDataTileBuffers,
//                     in LocalToWorld localToWorld,
//                     in LocalToParent localToParent,
//                     in Parent parent) =>
//             {
//                 var worldPos = new int3(localToWorld.Position);
//
//                 //TODO redo this if needed
//                 // var pos = worldPos + offSet;
//                 //
//                 // //This basically means that every forth tile is allowed to do an update
//                 // if (pos.x % 3 != 0 || pos.y % 3 != 0) return;
//
//                 var localPos = new int3(localToParent.Position);
//
//                 var directions = new NativeArray<int3>(4, Allocator.Temp)
//                 {
//                     [0] = new int3(0, 1, 0),
//                     [1] = new int3(0, -1, 0),
//                     [2] = new int3(-1, 0, 0),
//                     [3] = new int3(1, 0, 0)
//                 };
//
//                 var tested = new NativeArray<bool>(4, Allocator.Temp)
//                 {
//                     [0] = false,
//                     [1] = false,
//                     [2] = false,
//                     [3] = false
//                 };
//
//                 BufferCheck(ref metaDataTileBuffers, ref directions, ref tested, worldPos, localPos, parent.Value);
//
//                 for (int i = 0; i < tested.Length; i++)
//                 {
//                     if(tested[i]) continue;
//
//                     Check(in floatArray, directions[i], ref metaDataTileBuffers, worldPos, localPos, parent.Value);
//                 }
//
//                 directions.Dispose();
//                 tested.Dispose();
//
//             }).WithReadOnly(floatArray).WithNativeDisableParallelForRestriction(floatArray)
//                 .WithDisposeOnCompletion(floatArray)//.WithoutBurst()
//                 .ScheduleParallel();
//
//             Dependency.Complete();
//
//             //Increase offset for next update
//             offset.DoStep();
//         }
//
//         private static bool Check(in NativeArray<Data> floatArray, int3 direction,
// 	        ref DynamicBuffer<NeighbourBuffer> metaDataTileBuffers
// 	        , int3 worldPos, int3 localPos, Entity parent)
//         {
// 	        var localPosDir = localPos + direction;
// 	        var worldPosDir = worldPos + direction;
//
//             for (int i = 0; i < floatArray.Length; i++)
//             {
// 	            var sameParent = parent == floatArray[i].Parent;
//
// 	            var coordToTest = sameParent
// 		            ? floatArray[i].LocalPos
// 		            : floatArray[i].WorldPos;
//
// 	            var currentCoord = sameParent ? localPosDir : worldPosDir;
//
//                 //Cus apparently cant do int3 == int3 as a bool in If statement
//                 if (coordToTest.x != currentCoord.x || coordToTest.y != currentCoord.y) continue;
//
//                 metaDataTileBuffers.Add(new NeighbourBuffer{ NeighbourData = new Data()
//                 {
//                     Entity = floatArray[i].Entity,
//                     Parent = floatArray[i].Parent,
//
//                     //We can set these the same as only the correct one will be checked
//                     WorldPos = coordToTest,
//                     LocalPos = coordToTest
//                 }});
//
//                 //This does assume matrixes can't be on top of each other
//                 return true;
//             }
//
//             return false;
//         }
//
//         private static void BufferCheck(ref DynamicBuffer<NeighbourBuffer> metaDataTileBuffers,
//             ref NativeArray<int3> directions, ref NativeArray<bool> tested, int3 worldPos, int3 localPos, Entity parent)
//         {
//             for (int i = metaDataTileBuffers.Length - 1; i >= 0; i--)
//             {
//                 var found = false;
//                 for (int j = 0; j < directions.Length; j++)
//                 {
//
// 	                var sameParent = metaDataTileBuffers[i].NeighbourData.Parent == parent;
//
// 	                var coordToTest = sameParent
// 		                ? metaDataTileBuffers[i].NeighbourData.LocalPos
// 		                : metaDataTileBuffers[i].NeighbourData.WorldPos;
//
// 	                var currentCoord = sameParent ? localPos : worldPos;
//
// 	                //Basically if we have the same parent check to see if our local coords match
// 	                //Otherwise we'll be looking for cross matrix therefore check world
//                     if (coordToTest.x == currentCoord.x && coordToTest.y == currentCoord.y)
//                     {
//                         tested[j] = true;
//                         found = true;
//                         break;
//                     }
//                 }
//
//                 if(found) continue;
//
//                 metaDataTileBuffers.RemoveAt(i);
//             }
//         }
//
//         public struct Data
//         {
// 	        public Entity Entity;
//         }
//     }
// }
