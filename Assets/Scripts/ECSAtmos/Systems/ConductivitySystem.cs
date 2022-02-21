using Systems.Atmospherics;
using ECSAtmos.Components;
using ECSAtmos.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace ECSAtmos.Systems
{
    [BurstCompile]
    [UpdateAfter(typeof(TileGasExchangeSystem))]
    public class ConductivitySystem : JobSystemBase
    {
        private OffsetLogic offset;
        
        private EntityQuery query;
        
        private float timer;
        
        protected override void OnCreate() {
            this.query = GetEntityQuery(typeof(GasMixComponent), 
                typeof(Translation), 
                typeof(ConductivityComponent),
                typeof(GasDataBuffer));
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            timer += Time.DeltaTime;
            if (timer < 0.05f) return inputDeps;
            timer = 0;

            ConductivityJob job = new ConductivityJob() 
            {
                metaDataTileNeighbor = GetBufferTypeHandle<MetaDataTileBuffer>(),
                entityTypeHandle = GetEntityTypeHandle(),
		              
                offset = offset.Offset,
		              
                allGasMix = GetComponentDataFromEntity<GasMixComponent>(),
                allConductivity = GetComponentDataFromEntity<ConductivityComponent>(),
                allMetaDataTile = GetComponentDataFromEntity<MetaDataTileComponent>(),
                allTranslation = GetComponentDataFromEntity<Translation>(),
            };

            //Increase offset for next update
            offset.DoStep();
     
            return job.ScheduleParallel(this.query, 1, inputDeps);
        }

        [BurstCompile]
        public struct ConductivityJob : IJobEntityBatch
        {
            [ReadOnly] 
            public int3 offset;

            [ReadOnly] 
            public EntityTypeHandle entityTypeHandle;

            //Writing and Reading from these, this is thread safe as tiles are only access once in an area so no conflicts
            [NativeDisableParallelForRestriction] 
            public ComponentDataFromEntity<GasMixComponent> allGasMix;
            [NativeDisableParallelForRestriction] 
            public ComponentDataFromEntity<ConductivityComponent> allConductivity;
            [NativeDisableParallelForRestriction] 
            public ComponentDataFromEntity<MetaDataTileComponent> allMetaDataTile;

            [ReadOnly] 
            public ComponentDataFromEntity<Translation> allTranslation;
            
            [ReadOnly]
            public BufferTypeHandle<MetaDataTileBuffer> metaDataTileNeighbor;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                BufferAccessor<MetaDataTileBuffer> metaDataTileNeighbors = batchInChunk.GetBufferAccessor(this.metaDataTileNeighbor);
                
                for (int i = 0; i < batchInChunk.Count; ++i)
                {
                    var entity = batchInChunk.GetNativeArray(entityTypeHandle)[i];
                    int3 worldPos = new int3(allTranslation[entity].Value);
                    var pos = worldPos + offset;

                    //This basically means that every forth tile is allowed to do an update but the offset changes to allow
                    //all tiles to update after the system has been called multiple times
                    if (pos.x % 3 != 0 || pos.y % 3 != 0) continue;
                    
                    var conductivity = allConductivity[entity];
                    
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
                        
                        allConductivity[entity] = conductivity;
                        return;
                    }

                    if (conductivity.HeatCapacity < AtmosDefines.M_CELL_WITH_RATIO) return;

                    conductivity.AllowedToSuperConduct = true;
                    var gasMix = allGasMix[entity];
                    var neighbors = metaDataTileNeighbors[i];
                    
                    //Don't bother when no neighbors
                    if (neighbors.Length != 0)
                    {
                        //Solid conductivity is done by meta data node variables, as walls dont have functioning gas mix
                        SolidConductivity(ref conductivity, in neighbors, ref allMetaDataTile, ref allConductivity);
                    }

                    //Check to see whether we should disable the node
                    if (conductivity.ConductivityTemperature < AtmosDefines.MINIMUM_TEMPERATURE_FOR_SUPERCONDUCTION)
                    {
                        //Disable node if it fails temperature check
                        conductivity.AllowedToSuperConduct = false;
                        conductivity.StartingSuperConduct = false;
                    }
                    
                    allConductivity[entity] = conductivity;
                }
            }
            
            private static void SolidConductivity(ref ConductivityComponent currentConductivity, 
                in DynamicBuffer<MetaDataTileBuffer> neighbors, 
                ref ComponentDataFromEntity<MetaDataTileComponent> allMetaDataTile,
                ref ComponentDataFromEntity<ConductivityComponent> allConductivity)
            {
                for (var i = 0; i < neighbors.Length; i++)
                {
                    var neighborEntity = neighbors[i].DataTile;
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
                        ConductFromSolidToSolid(ref currentConductivity, ref neighborConductivity, ref neighborMetaDataTile, tempDelta);
                        
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
            private static void ConductFromSolidToSolid(ref ConductivityComponent currentConductivity, 
                ref ConductivityComponent neighborConductivity, ref MetaDataTileComponent neighborMetaDataTile, 
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
                neighborMetaDataTile.Sleeping = false;
            }

            #endregion
        }
    }
}
