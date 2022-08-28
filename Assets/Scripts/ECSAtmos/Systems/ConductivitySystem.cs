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
    [UpdateAfter(typeof(UpdateResetSystem))]
    public partial class ConductivitySystem : AtmosSystemBase
    {
	    private EntityQuery query;

        private AtmosEndEntityCommandBufferSystem commandBufferSystem;

        protected override void OnCreate()
        {
	        commandBufferSystem = World.GetOrCreateSystem<AtmosEndEntityCommandBufferSystem>();

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
	        ConductivityJob job = new ConductivityJob
            {
                metaDataTileNeighbor = GetBufferTypeHandle<NeighbourBuffer>(true),
                entityTypeHandle = GetEntityTypeHandle(),

                offset = offset,

                allConductivity = GetComponentDataFromEntity<ConductivityComponent>(),
                allMetaDataTile = GetComponentDataFromEntity<MetaDataTileComponent>(),
                allUpdateData = GetComponentDataFromEntity<AtmosUpdateDataComponent>(true),
                ecb = commandBufferSystem.CreateCommandBuffer().AsParallelWriter()
            };

            inputDeps = job.ScheduleParallel(query, inputDeps);

            commandBufferSystem.AddJobHandleForProducer(inputDeps);

            return inputDeps;
        }

        [BurstCompile]
        public struct ConductivityJob : IJobEntityBatch
        {
            [ReadOnly]
            public OffsetLogic offset;

            [ReadOnly]
            public EntityTypeHandle entityTypeHandle;

            //Writing and Reading from these, this is thread safe as tiles are only access once in an area so no conflicts
            [NativeDisableParallelForRestriction]
            public ComponentDataFromEntity<ConductivityComponent> allConductivity;

            [NativeDisableParallelForRestriction]
            public ComponentDataFromEntity<MetaDataTileComponent> allMetaDataTile;

            [ReadOnly]
            [NativeDisableParallelForRestriction]
            public ComponentDataFromEntity<AtmosUpdateDataComponent> allUpdateData;

            [NativeDisableParallelForRestriction]
            public EntityCommandBuffer.ParallelWriter ecb;

            [ReadOnly]
            public BufferTypeHandle<NeighbourBuffer> metaDataTileNeighbor;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                BufferAccessor<NeighbourBuffer> metaDataTileNeighbors = batchInChunk.GetBufferAccessor(metaDataTileNeighbor);

                for (int i = 0; i < batchInChunk.Count; ++i)
                {
                    var entity = batchInChunk.GetNativeArray(entityTypeHandle)[i];

                    var currentUpdateData = allUpdateData[entity];

                    //This basically means that every forth tile is allowed to do an update
                    //But we alternate which ones every step (I would draw a shitty paint diagram to explain but i cant draw)
                    //Basically means every forth update every tile will have done an update
                    //NOTE: because of this we don't update two tiles in the same frame therefore can disable writing safeties in the job
                    if (currentUpdateData.XUpdateID != offset.XUpdateID || currentUpdateData.YUpdateID != offset.YUpdateID) continue;

                    var conductivity = allConductivity[entity];

                    //Only allow conducting if we are the starting node or we are allowed to
                    if(conductivity.StartingSuperConduct == false && conductivity.AllowedToSuperConduct == false) continue;

                    //Starting node must have higher temperature
                    if (conductivity.ConductivityTemperature < (conductivity.StartingSuperConduct
                        ? AtmosDefines.MINIMUM_TEMPERATURE_START_SUPERCONDUCTION
                        : AtmosDefines.MINIMUM_TEMPERATURE_FOR_SUPERCONDUCTION))
                    {

                        //Disable node if it fails temperature check
                        conductivity.AllowedToSuperConduct = false;
                        conductivity.StartingSuperConduct = false;

                        allConductivity[entity] = conductivity;
                        continue;
                    }

                    if (conductivity.HeatCapacity < AtmosDefines.M_CELL_WITH_RATIO) continue;

                    conductivity.AllowedToSuperConduct = true;
                    var neighbors = metaDataTileNeighbors[i];

                    //Don't bother when no neighbors
                    if (neighbors.Length != 0)
                    {
                        //Solid conductivity is done by meta data node variables, as walls dont have functioning gas mix
                        SolidConductivity(ref conductivity, in neighbors,ref ecb, in batchIndex, ref allMetaDataTile, ref allConductivity);
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
                in DynamicBuffer<NeighbourBuffer> neighbors,
                ref EntityCommandBuffer.ParallelWriter ecb,
                in int batchIndex,
                ref ComponentDataFromEntity<MetaDataTileComponent> allMetaDataTile,
                ref ComponentDataFromEntity<ConductivityComponent> allConductivity)
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
