using Systems.Atmospherics;
using ECSAtmos.Components;
using ECSAtmos.DataTypes;
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
    //TODO run after matrix child move and after neighbours set up
    [BurstCompile]
    [UpdateAfter(typeof(TileNeighbourSystem))]
    [UpdateAfter(typeof(MatrixEntityPositionSystem))]
    public class TileGasExchangeSystem : JobSystemBase
    {
        private OffsetLogic offset;
        
        private EntityQuery query;

        private float timer;
        protected override void OnCreate() {
	        this.query = GetEntityQuery(typeof(GasMixComponent), 
		        typeof(Translation), 
		        typeof(MetaDataTileComponent), 
		        typeof(MetaDataTileBuffer), 
		        typeof(GasDataBuffer));
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
	        timer += Time.DeltaTime;
	        //if (timer < 0.2f) return inputDeps;
	        timer = 0;

	        Entities.ForEach((ref MetaDataTileComponent dataTileComponent) =>
	        {
		        dataTileComponent.Updated = false;
		        dataTileComponent.TriedToUpdate = false;
	        }).ScheduleParallel();
	        
	        Dependency.Complete();
	        
	        TileGasExchangeJob job = new TileGasExchangeJob() 
	        {
		        metaDataTileNeighbor = GetBufferTypeHandle<MetaDataTileBuffer>(),
		         
		        entityTypeHandle = GetEntityTypeHandle(),
		         
		        offset = offset.Offset,
		         
		        allGasMix = GetComponentDataFromEntity<GasMixComponent>(),
		        allMetaDataTile = GetComponentDataFromEntity<MetaDataTileComponent>(),
		        allGasData = GetBufferFromEntity<GasDataBuffer>(), 
		        allTranslation = GetComponentDataFromEntity<Translation>(),
	        };

	        //Increase offset for next update
	        offset.DoStep();
     
	        return job.ScheduleParallel(this.query, 1, inputDeps);
        }
        
        [BurstCompile]
        private struct TileGasExchangeJob : IJobEntityBatch 
        {
	        [ReadOnly]
	        public BufferTypeHandle<MetaDataTileBuffer> metaDataTileNeighbor;
	        
	        [ReadOnly]
	        public EntityTypeHandle entityTypeHandle;

	        [ReadOnly]
	        public int3 offset;
	        
	        //Writing and Reading from these, this is thread safe as tiles are only access once in an area so no conflicts
	        [NativeDisableParallelForRestriction]
	        public ComponentDataFromEntity<GasMixComponent> allGasMix;
	        [NativeDisableParallelForRestriction]
	        public BufferFromEntity<GasDataBuffer> allGasData;
	        [NativeDisableParallelForRestriction]
	        public ComponentDataFromEntity<MetaDataTileComponent> allMetaDataTile;
	        
	        [ReadOnly]
	        public ComponentDataFromEntity<Translation> allTranslation;
	        
	        public void Execute(ArchetypeChunk batchInChunk, int batchIndex) 
	        {
		        BufferAccessor<MetaDataTileBuffer> metaDataTileNeighbors = batchInChunk.GetBufferAccessor(this.metaDataTileNeighbor);
 
		        for (int i = 0; i < batchInChunk.Count; ++i)
		        {
			        var entity = batchInChunk.GetNativeArray(entityTypeHandle)[i];
					var worldPos = new int3(allTranslation[entity].Value);
			        var pos = worldPos + offset;
                
			        //This basically means that every forth tile is allowed to do an update
			        //But we alternate which ones every step (I would draw a shitty paint diagram to explain but i cant draw)
			        //Basically means every forth update every tile will have done an update
			        //NOTE: because of this we don't update two tiles in the same frame therefore can disable writing safeties in the job
			        if (pos.x % 3 != 0 || pos.y % 3 != 0) continue;
			        
			        //TODO more restriction on updates?, ie have them alternate every 2nd too?

			        var currentMetaDataTile = allMetaDataTile[entity];
			        
			        currentMetaDataTile.TriedToUpdate = true;
			        allMetaDataTile[entity] = currentMetaDataTile;
			        
			        //If the tile is sleeping don't bother
			        if(currentMetaDataTile.Sleeping) continue;
			        
			        //We only need to check open tiles
			        if (currentMetaDataTile.TileAllowed() == false) continue;

			        var neighbors = metaDataTileNeighbors[i];
			        
			        //No neighbors so don't bother
			        if(neighbors.Length == 0) continue;
			        
			        var currentGasMix = allGasMix[entity];
			        var currentGasDataBuffer = allGasData[entity];

			        //TODO out the wind stuff
			        var isPressureChanged = PressureCheck(in neighbors, ref allGasMix, 
				        ref allMetaDataTile, in allTranslation, ref allGasData, 
				        ref currentGasMix, ref currentGasDataBuffer, worldPos);
					
					//TODO queue wind event, use ECB?
					
					if (isPressureChanged == false)
					{
						//If no pressure change is required as all neighbors are equal sleep tile
						currentMetaDataTile.Sleeping = true;
						allMetaDataTile[entity] = currentMetaDataTile;
						continue;
					}
					
					//Now to equalise the gases
					Equalise(in neighbors, ref allGasMix, ref allMetaDataTile, ref allGasData, 
						ref currentGasMix, ref currentGasDataBuffer, ref entity, ref currentMetaDataTile);
		        }
	        }

	        private static bool PressureCheck(in DynamicBuffer<MetaDataTileBuffer> neighbors, 
		        ref ComponentDataFromEntity<GasMixComponent> allGasMix,
		        ref ComponentDataFromEntity<MetaDataTileComponent> allMetaDataTile,
		        in ComponentDataFromEntity<Translation> allTranslation,
		        ref BufferFromEntity<GasDataBuffer> allGasData,
		        ref GasMixComponent currentGasMix, ref DynamicBuffer<GasDataBuffer> currentGasDataBuffer,
		        int3 worldPos)
	        {
				var windDirection = int2.zero;
				var clampVector = int3.zero;
				float windForce = 0L;
				
				bool isPressureChanged = false;

				for (var k = 0; k < neighbors.Length; k++)
				{ 
					var neighborEntity = neighbors[k].DataTile;
					var neighborGasMix = allGasMix[neighborEntity];
					var neighborTileData = allMetaDataTile[neighborEntity];
					var neighborLocalPos = new int3(allTranslation[neighborEntity].Value);

					//We only need to check open tiles
					if (neighborTileData.TileAllowed() == false) continue;

					float pressureDifference = currentGasMix.Pressure - neighborGasMix.Pressure;
					float absoluteDifference = math.abs(pressureDifference);

					//Check to see if theres a large pressure difference
					if (absoluteDifference > AtmosConstants.MinPressureDifference)
					{
						isPressureChanged = true;

						if (absoluteDifference > windForce)
						{
							windForce = absoluteDifference;
						}

						int neighborOffsetX = (neighborLocalPos.x - worldPos.x);
						int neighborOffsetY = (neighborLocalPos.y - worldPos.y);

						if (pressureDifference > 0)
						{
							windDirection.x += neighborOffsetX;
							windDirection.y += neighborOffsetY;
						}
						else if (pressureDifference < 0)
						{
							windDirection.x -= neighborOffsetX;
							windDirection.y -= neighborOffsetY;
						}

						clampVector.x -= neighborOffsetX;
						clampVector.y -= neighborOffsetY;

						//We continue here so we can calculate the whole wind direction from all possible nodes
						continue;
					}

					//Check if the moles are different. (e.g. CO2 is different from breathing)
					//Check current node then check neighbor so we dont miss a gas if its only on one of the nodes

					var neighborBuffer = allGasData[neighborEntity];

					//Current node
					//Only need to check if false
					if (isPressureChanged == false)
					{
						for (int j = currentGasDataBuffer.Length - 1; j >= 0; j--)
						{
							var gas = currentGasDataBuffer[j].GasData;
							var moles = gas.Moles;
							float molesNeighbor = neighborBuffer.GetGasMoles(gas.GasSO);

							if (math.abs(moles - molesNeighbor) > AtmosConstants.MinPressureDifference)
							{
								isPressureChanged = true;

								//We break not return here so we can still work out wind direction
								break;
							}
						}
					}

					//Neighbor node
					//Only need to check if false
					if (isPressureChanged == false)
					{
						for (int j = 0; j < neighborBuffer.Length; j++)
						{
							var gas = neighborBuffer[j].GasData;
							float molesNeighbor = gas.Moles;
							float moles = currentGasDataBuffer.GetGasMoles(gas.GasSO);

							if (math.abs(moles - molesNeighbor) > AtmosConstants.MinPressureDifference)
							{
								isPressureChanged = true;

								//We break not return here so we can still work out wind direction
								break;
							}
						}
					}
		        }

				//not blowing in direction of tiles that aren't atmos passable
				windDirection.y = math.clamp(windDirection.y, clampVector.y < 0 ? 0 : -1,
					clampVector.y > 0 ? 0 : 1);
				windDirection.x = math.clamp(windDirection.x, clampVector.x < 0 ? 0 : -1,
					clampVector.x > 0 ? 0 : 1);

				return isPressureChanged;
	        }

	        private static void Equalise(in DynamicBuffer<MetaDataTileBuffer> neighbors, 
		        ref ComponentDataFromEntity<GasMixComponent> allGasMix,
		        ref ComponentDataFromEntity<MetaDataTileComponent> allMetaDataTile,
		        ref BufferFromEntity<GasDataBuffer> allGasData,
		        ref GasMixComponent currentGasMix, ref DynamicBuffer<GasDataBuffer> currentGasDataBuffer,
		        ref Entity entity, ref MetaDataTileComponent currentMetaDataTile)
	        {
					var dividingCount = 1;

					//Add all the gases
					for (int j = 0; j < neighbors.Length; j++)
					{
						var neighborEntity = neighbors[j].DataTile;
						var neighborTileData = allMetaDataTile[neighborEntity];
						
						//We only need to check open tiles
						if (neighborTileData.TileAllowed() == false) continue;
						dividingCount++;
						
						var neighborBuffer = allGasData[neighborEntity];
						var neighborGasMix = allGasMix[neighborEntity];
						
						currentGasMix.Volume += neighborGasMix.Volume;
						
						//TransferAllGas from neighbor to current, can do this as this happens after main thread update
						//so there wont be conflicts (doesnt clear old mix, will get overriden later)
						AtmosUtils.TransferAllGas(ref neighborBuffer, ref neighborGasMix, 
							ref currentGasDataBuffer, ref currentGasMix);
					}
					
					if (dividingCount == 1) return;

					//Note: this assumes the volume of all tiles are the same
					currentGasMix.Volume /= dividingCount;

					//Divide for neighbors
					currentGasDataBuffer.DivideAllGases(dividingCount);
					
					currentGasMix.ReCalculate(currentGasDataBuffer);

					//Set neighbor mixes
					for (int j = 0; j < neighbors.Length; j++)
					{
						var neighborEntity = neighbors[j].DataTile;
						var neighborTileData = allMetaDataTile[neighborEntity];
						
						//We only need to check open tiles
						if (neighborTileData.TileAllowed() == false) continue;
						
						neighborTileData.Updated = true;
						
						//Wake the neighbor tile up
						neighborTileData.Sleeping = false;

						//They are structs so copy to old reference for neighbors
						allGasData[neighborEntity].CopyFrom(currentGasDataBuffer);
						allGasMix[neighborEntity] = currentGasMix;
						allMetaDataTile[neighborEntity] = neighborTileData;
					}
					
					currentMetaDataTile.Updated = true;
					
					//Literally impossible to get here if it is true but I feel safer adding it :p
					currentMetaDataTile.Sleeping = false;
					
					//They are structs so copy to old reference for this tile
					allGasData[entity].CopyFrom(currentGasDataBuffer);
					allGasMix[entity] = currentGasMix;
					allMetaDataTile[entity] = currentMetaDataTile;
	        }
        }
    }
}
