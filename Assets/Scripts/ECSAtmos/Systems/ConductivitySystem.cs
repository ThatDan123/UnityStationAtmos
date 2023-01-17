using Systems.Atmospherics;
using ECSAtmos.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECSAtmos.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(AtmosSystemGroup))]
    [UpdateAfter(typeof(UpdateResetSystem))]
    public partial struct ConductivitySystem : ISystem
    {
	    private EntityQuery query;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            query = SystemAPI.QueryBuilder()
                .WithAll<AtmosUpdateDataComponent, AtmosTileOffsetShared, MetaDataTileComponent, GasMixComponent, 
                    ConductivityComponent, GasDataBuffer, TileAtmosTag>()
                .WithNone<DeactivatedTag>().Build();
	        
            state.RequireForUpdate(query);
	        
            //This is fine and won't block the system running as it is Added after the RequireForUpdate
            query.AddSharedComponentFilter(new AtmosTileOffsetShared());
	        
            state.RequireForUpdate<AtmosOffsetSingleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteDependencyBeforeRO<AtmosOffsetSingleton>();
            var offsetSingleton = SystemAPI.GetSingleton<AtmosOffsetSingleton>();
	        
            //This basically means that every forth tile is allowed to do an update
            //But we alternate which ones every step (I would draw a shitty paint diagram to explain but i cant draw)
            //Basically means every forth update every tile will have done an update
            //NOTE: because of this we don't update two tiles in the same frame therefore can disable writing safeties in the job
            query.SetSharedComponentFilter(new AtmosTileOffsetShared(offsetSingleton.Offset));

            var ecbSystem = SystemAPI.GetSingleton<AtmosEndEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            
            var job = new ConductivityJob
            {
                MetaDataTileNeighbor = SystemAPI.GetBufferLookup<NeighbourBuffer>(true),

                AllConductivity = SystemAPI.GetComponentLookup<ConductivityComponent>(),
                AllMetaDataTile = SystemAPI.GetComponentLookup<MetaDataTileComponent>(),
                Ecb = ecb.AsParallelWriter()
            }.ScheduleParallel(query, state.Dependency);

            state.Dependency = job;
        }

        [BurstCompile]
        private partial struct ConductivityJob : IJobEntity
        {
            [ReadOnly]
            public BufferLookup<NeighbourBuffer> MetaDataTileNeighbor;
            
            //Writing and Reading from these, this is thread safe as tiles are only access once in an area so no conflicts
            [NativeDisableParallelForRestriction]
            public ComponentLookup<ConductivityComponent> AllConductivity;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<MetaDataTileComponent> AllMetaDataTile;
            
            public EntityCommandBuffer.ParallelWriter Ecb;

            private void Execute([ChunkIndexInQuery] int index, in Entity entity)
            {
                var conductivity = AllConductivity[entity];

                //Only allow conducting if we are the starting node or we are allowed to
                if(conductivity.StartingSuperConduct == false && conductivity.AllowedToSuperConduct == false) return;

                //Starting node must have higher temperature
                if (conductivity.ConductivityTemperature < (conductivity.StartingSuperConduct
                    ? AtmosDefines.MINIMUM_TEMPERATURE_START_SUPERCONDUCTION
                    : AtmosDefines.MINIMUM_TEMPERATURE_FOR_SUPERCONDUCTION))
                {

                    //Disable node if it fails temperature check
                    conductivity.AllowedToSuperConduct = false;
                    conductivity.StartingSuperConduct = false;

                    AllConductivity[entity] = conductivity;
                    return;
                }

                if (conductivity.HeatCapacity < AtmosDefines.M_CELL_WITH_RATIO) return;

                conductivity.AllowedToSuperConduct = true;
                var neighbors = MetaDataTileNeighbor[entity];

                //Don't bother when no neighbors
                if (neighbors.Length != 0)
                {
                    //Solid conductivity is done by meta data node variables, as walls dont have functioning gas mix
                    SolidConductivity(ref conductivity, in neighbors,ref Ecb, in index, ref AllMetaDataTile, ref AllConductivity);
                }

                //Check to see whether we should disable the node
                if (conductivity.ConductivityTemperature < AtmosDefines.MINIMUM_TEMPERATURE_FOR_SUPERCONDUCTION)
                {
                    //Disable node if it fails temperature check
                    conductivity.AllowedToSuperConduct = false;
                    conductivity.StartingSuperConduct = false;
                }

                AllConductivity[entity] = conductivity;
            }

            private static void SolidConductivity(ref ConductivityComponent currentConductivity,
                in DynamicBuffer<NeighbourBuffer> neighbors,
                ref EntityCommandBuffer.ParallelWriter ecb,
                in int batchIndex,
                ref ComponentLookup<MetaDataTileComponent> allMetaDataTile,
                ref ComponentLookup<ConductivityComponent> allConductivity)
            {
                for (var i = 0; i < neighbors.Length; i++)
                {
                    var neighborEntity = neighbors[i].NeighbourEntity;
                    var neighborMetaDataTile = allMetaDataTile[neighborEntity];
                    var neighborConductivity = allConductivity[neighborEntity];

                    var tempDelta = currentConductivity.ConductivityTemperature;

                    //Radiate temperature between Solid and Space
                    if(neighborMetaDataTile.IsSpace)
                    {
                        if(currentConductivity.ConductivityTemperature <= AtmosConstants.ZERO_CELSIUS_IN_KELVIN) continue;

                        tempDelta -= AtmosDefines.SPACE_TEMPERATURE;
                        RadiateTemperatureToSpace(ref currentConductivity, in neighborConductivity, tempDelta);
                    }
                    //Share temperature between Solid and Solid
                    else
                    {
	                    tempDelta -= neighborConductivity.ThermalConductivity;
                        ConductFromSolidToSolid(ref currentConductivity, ref neighborConductivity, ref neighborEntity,
	                        ref ecb, in batchIndex, tempDelta);

                        allMetaDataTile[neighborEntity] = neighborMetaDataTile;
                        allConductivity[neighborEntity] = neighborConductivity;
                    }
                }
            }

            #region Solid To ...

            /// <summary>
            /// Used to transfer heat between an Solid tile and Space
            /// Uses data from MetaDataNode for solid node and MetaDataNode date for Space node
            /// </summary>
            private static void RadiateTemperatureToSpace(ref ConductivityComponent currentConductivity,
                in ConductivityComponent neighborConductivity, float tempDelta)
            {
                if(currentConductivity.HeatCapacity <= 0) return;

                if(math.abs(tempDelta) <= AtmosDefines.MINIMUM_TEMPERATURE_DELTA_TO_CONSIDER) return;

                //The larger the combined capacity the less is shared
                var heat = neighborConductivity.ThermalConductivity * tempDelta *
                           (currentConductivity.HeatCapacity * AtmosDefines.SPACE_HEAT_CAPACITY /
                            (currentConductivity.HeatCapacity + AtmosDefines.SPACE_HEAT_CAPACITY));

                currentConductivity.ConductivityTemperature -= heat / currentConductivity.HeatCapacity;
            }

            /// <summary>
            /// Used to transfer heat between an Solid tile and Solid tile
            /// Uses data from MetaDataNode for the current solid node and MetaDataNode date for the other Solid node
            /// </summary>
            private static void ConductFromSolidToSolid(
	            ref ConductivityComponent currentConductivity,
                ref ConductivityComponent neighborConductivity,
	            ref Entity neighborEntity,
                ref EntityCommandBuffer.ParallelWriter ecb,
	            in int batchIndex,
                float tempDelta)
            {
                if (math.abs(tempDelta) <= AtmosDefines.MINIMUM_TEMPERATURE_DELTA_TO_CONSIDER) return;

                //The larger the combined capacity the less is shared
                var heat = neighborConductivity.ThermalConductivity * tempDelta *
                           (currentConductivity.HeatCapacity * neighborConductivity.HeatCapacity /
                            (currentConductivity.HeatCapacity + neighborConductivity.HeatCapacity));

                //The higher your own heat cap the less heat you get from this arrangement
                currentConductivity.ConductivityTemperature -= heat / currentConductivity.HeatCapacity;
                neighborConductivity.ConductivityTemperature += heat / neighborConductivity.HeatCapacity;

                //Do atmos update for the next solid node if temperature is allowed so it can do conduction
                if(neighborConductivity.ConductivityTemperature < AtmosDefines.MINIMUM_TEMPERATURE_FOR_SUPERCONDUCTION) return;
                neighborConductivity.AllowedToSuperConduct = true;

                ecb.RemoveComponent<DeactivatedTag>(batchIndex, neighborEntity);
            }

            #endregion
        }
    }
}
