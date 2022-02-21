using System.Collections;
using ECSAtmos.Components;
using ECSAtmos.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ECSAtmos.Systems
{
    [BurstCompile]
    public class TileNeighbourSystem : SystemBase
    {
        private EntityQuery query;
        
        private OffsetLogic offset;
        
        private float timer;
        protected override void OnUpdate()
        {
            timer += Time.DeltaTime;
            if (timer < 0.2f) return;
            timer = 0;

            var entitiesInQuery = query.CalculateEntityCount();
            
            NativeArray<Data> floatArray = new NativeArray<Data>(entitiesInQuery, Allocator.TempJob);

            Entities.ForEach((Entity entity,
                    int entityInQueryIndex,
                    ref Translation translation) =>
                {
                    var pos = new int3(translation.Value);

                    floatArray[entityInQueryIndex] = new Data() {Pos = pos, Entity = entity};

                }).WithAll<MetaDataTileBuffer>()
                .WithStoreEntityQueryInField(ref query)
                .ScheduleParallel();
            
            Dependency.Complete();

            var offSet = offset.Offset;
            
            Entities.ForEach((Entity entity,
                    ref DynamicBuffer<MetaDataTileBuffer> metaDataTileBuffers, 
                    in Translation translation) =>
            {
                var centerPos = new int3(translation.Value);
                var pos = centerPos + offSet;
                
                //This basically means that every forth tile is allowed to do an update
                if (pos.x % 3 != 0 || pos.y % 3 != 0) return;

                var directions = new NativeArray<int3>(4, Allocator.Temp)
                {
                    [0] = new int3(0, 1, 0),
                    [1] = new int3(0, -1, 0),
                    [2] = new int3(-1, 0, 0),
                    [3] = new int3(1, 0, 0)
                };
                
                var tested = new NativeArray<bool>(4, Allocator.Temp)
                {
                    [0] = false,
                    [1] = false,
                    [2] = false,
                    [3] = false
                };

                BufferCheck(ref metaDataTileBuffers, ref directions, ref tested, centerPos);

                for (int i = 0; i < tested.Length; i++)
                {
                    if(tested[i]) continue;

                    Check(in floatArray, centerPos + directions[i], ref metaDataTileBuffers);
                }

                directions.Dispose();
                tested.Dispose();

            }).WithReadOnly(floatArray).WithNativeDisableParallelForRestriction(floatArray)
                .WithDisposeOnCompletion(floatArray)//.WithoutBurst()
                .ScheduleParallel();
            
            Dependency.Complete();

            //Increase offset for next update
            offset.DoStep();
        }

        private static void Check(in NativeArray<Data> floatArray, int3 key, ref DynamicBuffer<MetaDataTileBuffer> metaDataTileBuffers)
        {
            for (int i = 0; i < floatArray.Length; i++)
            {
                //Cus apparently cant do int3 == int3 as a bool in If statement
                if (floatArray[i].Pos.x != key.x || floatArray[i].Pos.y != key.y) continue;
                
                metaDataTileBuffers.Add(new MetaDataTileBuffer{ Data = new Data()
                {
                    Entity = floatArray[i].Entity,
                    Pos = key
                }});
                
                //This does assume matrixes can't be on top of each other
                return;
            }
        }

        private static void BufferCheck(ref DynamicBuffer<MetaDataTileBuffer> metaDataTileBuffers, 
            ref NativeArray<int3> directions, ref NativeArray<bool> tested, int3 pos)
        {
            for (int i = metaDataTileBuffers.Length - 1; i >= 0; i--)
            {
                var found = false;
                for (int j = 0; j < directions.Length; j++)
                {
                    if (metaDataTileBuffers[i].Data.Pos.x == pos.x && metaDataTileBuffers[i].Data.Pos.y == pos.y)
                    {
                        tested[j] = true;
                        found = true;
                        break;
                    }
                }
                
                if(found) continue;
                
                metaDataTileBuffers.RemoveAt(i);
            }
        }
        
        public struct Data
        {
            public int3 Pos;
            public Entity Entity;
        }
    }
}
