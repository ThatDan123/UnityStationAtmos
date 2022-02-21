using Systems.Atmospherics;
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
    [UpdateBefore(typeof(TileGasExchangeSystem))]
    public class TileConductivitySystem : JobSystemBase
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

            TileOpenToSolidConductivityJob job = new TileOpenToSolidConductivityJob() 
            {
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
    }

    [BurstCompile]
    public struct TileOpenToSolidConductivityJob : IJobEntityBatch
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
        
        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            for (int i = 0; i < batchInChunk.Count; ++i)
            {
                var entity = batchInChunk.GetNativeArray(entityTypeHandle)[i];
                int3 worldPos = new int3(allTranslation[entity].Value);
                var pos = worldPos + offset;
                
                //This basically means that every forth tile is allowed to do an update but the offset changes to allow
                //all tiles to update after the system has been called multiple times
                if (pos.x % 3 != 0 || pos.y % 3 != 0) continue;
                
                //TODO current unitystation has this, might not be needed to check, just always conduct?
                //if (node.IsOccupied && node.IsIsolatedNode) continue;
                
                var conductivity = allConductivity[entity];
                var gasMix = allGasMix[entity];
                
                var tempDelta = conductivity.ConductivityTemperature - gasMix.Temperature;

                if (math.abs(tempDelta) <= AtmosDefines.MINIMUM_TEMPERATURE_DELTA_TO_CONSIDER) continue;

                if (gasMix.WholeHeatCapacity <= AtmosConstants.MINIMUM_HEAT_CAPACITY) continue;

                if(conductivity.HeatCapacity <= AtmosConstants.MINIMUM_HEAT_CAPACITY) continue;
                
                //The larger the combined capacity the less is shared
                var heat = conductivity.ThermalConductivity * tempDelta *
                           (conductivity.HeatCapacity * gasMix.WholeHeatCapacity /
                            (conductivity.HeatCapacity + gasMix.WholeHeatCapacity));

                conductivity.ConductivityTemperature = math.max(
                    conductivity.ConductivityTemperature - (heat / conductivity.HeatCapacity),
                    AtmosDefines.SPACE_TEMPERATURE);

                gasMix.SetTemperature(math.max(
                    gasMix.Temperature + (heat / gasMix.WholeHeatCapacity),
                    AtmosDefines.SPACE_TEMPERATURE));
                
                allGasMix[entity] = gasMix;

                //Do atmos update for the Solid node if temperature is allowed so it can do conduction
                //This is checking for the start temperature as this is how the cycle will begin
                if (conductivity.ConductivityTemperature < AtmosDefines.MINIMUM_TEMPERATURE_START_SUPERCONDUCTION)
                {
                    allConductivity[entity] = conductivity;
                    continue;
                }

                if (conductivity.AllowedToSuperConduct == false)
                {
                    conductivity.AllowedToSuperConduct = true;

                    //Allow this node to trigger other tiles super conduction
                    conductivity.StartingSuperConduct = true;
                    allConductivity[entity] = conductivity;
                }

                //Poke the tile to do an update if needed
                var metaDataTileComponent = allMetaDataTile[entity];
                metaDataTileComponent.Sleeping = false;
                allMetaDataTile[entity] = metaDataTileComponent;
            }
        }
    }
}
