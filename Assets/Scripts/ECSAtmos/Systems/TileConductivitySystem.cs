using Systems.Atmospherics;
using ECSAtmos.Components;
using ECSAtmos.Util;
using Systems.ECSAtmos.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace ECSAtmos.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(AtmosSystemGroup))]
    [UpdateAfter(typeof(ConductivitySystem))]
    public partial class TileConductivitySystem : AtmosSystemBase
    {
	    private EntityQuery query;

        protected override void OnCreate()
        {
	        var queryDesc = new EntityQueryDesc
	        {
		        All = new ComponentType[]
		        {
			        typeof(AtmosUpdateDataComponent),
			        typeof(MetaDataTileComponent),
			        typeof(GasMixComponent),
			        typeof(ConductivityComponent),
			        typeof(GasDataBuffer),
			        typeof(TileAtmosTag)
		        },

		        None = new ComponentType[]
		        {
			        typeof(DeactivatedTag)
		        }
	        };

	        query = GetEntityQuery(queryDesc);
        }

        protected override JobHandle Update(JobHandle inputDeps, OffsetLogic offset)
        {
	        TileOpenToSolidConductivityJob job = new TileOpenToSolidConductivityJob
            {
                entityTypeHandle = GetEntityTypeHandle(),

                offset = offset,

                allGasMix = GetComponentDataFromEntity<GasMixComponent>(),
                allGasData = GetBufferFromEntity<GasDataBuffer>(true),
                allConductivity = GetComponentDataFromEntity<ConductivityComponent>(),

                allUpdateData = GetComponentDataFromEntity<AtmosUpdateDataComponent>(true),
                allMetaData = GetComponentDataFromEntity<MetaDataTileComponent>(true)
            };

            return job.ScheduleParallel(query, inputDeps);
        }
    }

    [BurstCompile]
    public struct TileOpenToSolidConductivityJob : IJobEntityBatch
    {
        [ReadOnly]
        public OffsetLogic offset;

        [ReadOnly]
        public EntityTypeHandle entityTypeHandle;

        //Writing and Reading from these, this is thread safe as tiles are only access once in an area so no conflicts
        [NativeDisableParallelForRestriction]
        public ComponentDataFromEntity<GasMixComponent> allGasMix;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        public BufferFromEntity<GasDataBuffer> allGasData;

        [NativeDisableParallelForRestriction]
        public ComponentDataFromEntity<ConductivityComponent> allConductivity;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        public ComponentDataFromEntity<MetaDataTileComponent> allMetaData;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        public ComponentDataFromEntity<AtmosUpdateDataComponent> allUpdateData;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            for (int i = 0; i < batchInChunk.Count; ++i)
            {
                var entity = batchInChunk.GetNativeArray(entityTypeHandle)[i];

                var currentUpdateData = allUpdateData[entity];

                //This basically means that every forth tile is allowed to do an update
                //But we alternate which ones every step (I would draw a shitty paint diagram to explain but i cant draw)
                //Basically means every forth update every tile will have done an update
                //NOTE: because of this we don't update two tiles in the same frame therefore can disable writing safeties in the job
                if (currentUpdateData.XUpdateID != offset.XUpdateID ||
                    currentUpdateData.YUpdateID != offset.YUpdateID) continue;

                var currentMetaData = allMetaData[entity];

                if (currentMetaData.IsSolid && currentMetaData.IsIsolatedNode == false) continue;

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

                var buffer = allGasData[entity];

                gasMix.SetTemperature(in buffer,
	                math.max(gasMix.Temperature + (heat / gasMix.WholeHeatCapacity),
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
            }
        }
    }
}
