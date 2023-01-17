using Systems.Atmospherics;
using ECSAtmos.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace ECSAtmos.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(AtmosSystemGroup))]
    [UpdateAfter(typeof(ConductivitySystem))]
    public partial struct TileConductivitySystem : ISystem
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
			
	        var job = new TileOpenToSolidConductivityJob().ScheduleParallel(query, state.Dependency);

	        state.Dependency = job;
        }
        
        [BurstCompile]
	    private partial struct TileOpenToSolidConductivityJob : IJobEntity
	    {
		    private void Execute(in Entity entity, 
		        ref GasMixComponent gasMix, ConductivityComponent conductivity, in DynamicBuffer<GasDataBuffer> gasDataBuffer, in MetaDataTileComponent metaDataTile)
	        {
		        if (metaDataTile.IsSolid && metaDataTile.IsIsolatedNode == false) return;

                var tempDelta = conductivity.ConductivityTemperature - gasMix.Temperature;

                if (math.abs(tempDelta) <= AtmosDefines.MINIMUM_TEMPERATURE_DELTA_TO_CONSIDER) return;

                if (gasMix.WholeHeatCapacity <= AtmosConstants.MINIMUM_HEAT_CAPACITY) return;

                if(conductivity.HeatCapacity <= AtmosConstants.MINIMUM_HEAT_CAPACITY) return;

                //The larger the combined capacity the less is shared
                var heat = conductivity.ThermalConductivity * tempDelta *
                           (conductivity.HeatCapacity * gasMix.WholeHeatCapacity /
                            (conductivity.HeatCapacity + gasMix.WholeHeatCapacity));

                conductivity.ConductivityTemperature = math.max(
                    conductivity.ConductivityTemperature - (heat / conductivity.HeatCapacity),
                    AtmosDefines.SPACE_TEMPERATURE);

                gasMix.SetTemperature(in gasDataBuffer,
	                math.max(gasMix.Temperature + (heat / gasMix.WholeHeatCapacity),
                    AtmosDefines.SPACE_TEMPERATURE));

                //Do atmos update for the Solid node if temperature is allowed so it can do conduction
                //This is checking for the start temperature as this is how the cycle will begin
                if (conductivity.ConductivityTemperature < AtmosDefines.MINIMUM_TEMPERATURE_START_SUPERCONDUCTION) return;

                if (conductivity.AllowedToSuperConduct == false)
                {
                    conductivity.AllowedToSuperConduct = true;

                    //Allow this node to trigger other tiles super conduction
                    conductivity.StartingSuperConduct = true;
                }
	        }
	    }
    }
}
