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
    //TODO run after matrix child move and after neighbours set up
    [BurstCompile]
    [UpdateAfter(typeof(TileNeighbourSystem))]
    [UpdateAfter(typeof(MatrixEntityPositionSystem))]
    [DisableAutoCreation]
    public class OldGasExchangeSystem : SystemBase
    {
        private int3 offset = new int3(0, 0, 0);
        
        private EntityQuery query;
 
        protected override void OnCreate() {
	        this.query = GetEntityQuery(typeof(GasMixComponent), 
		        typeof(LocalToParent), 
		        typeof(MetaDataTileComponent), 
		        typeof(MetaDataTileBuffer), 
		        typeof(GasDataBuffer));
        }
        
        protected override void OnUpdate()
        {
	         BufferFromEntity<GasDataBuffer> neighborGasDataBuffer
		         = this.GetBufferFromEntity<GasDataBuffer>(true);
	        
            //Local var the offset so it can go into job
            var cOffset = offset;
            var entityManager = EntityManager;
            Entities.ForEach((
                ref GasMixComponent gasMixComponent, 
                ref DynamicBuffer<GasDataBuffer> gasDataBuffer, 
                in LocalToParent localToParent,
                in MetaDataTileComponent metaDataTileComponent,
                in DynamicBuffer<MetaDataTileBuffer> metaDataTileBuffers) =>
            {
	            int3 localPos = new int3(localToParent.Position);
	            localPos += cOffset;
	            
	            //This basically means that every forth tile is allowed to do an update
	            //But we alternate which ones every step (I would draw a shitty paint diagram to explain but i cant draw)
	            //Basically means every forth update every tile will have done an update
	            if (localPos.x % 3 != 0 || localPos.y % 3 != 0) return;

	            //Do stuff
	            CalculatePressure(ref gasMixComponent, ref gasDataBuffer, 
		            in metaDataTileComponent, in metaDataTileBuffers, in neighborGasDataBuffer,
		            in entityManager, in localToParent);

            }).WithReadOnly(neighborGasDataBuffer).WithReadOnly(entityManager)
	            .WithNativeDisableContainerSafetyRestriction(neighborGasDataBuffer)
	            .WithNativeDisableParallelForRestriction(entityManager)
	            .ScheduleParallel();

            //Increase offset for next update
            offset += new int3(1, 1, 0);
            if (offset.x > 3)
            {
                offset = new int3(0, 0, 0);
            }
            
            //Force these jobs to finish before main thread continues
            this.Dependency.Complete();
        }
        
        

        private static void CalculatePressure(ref GasMixComponent gasMixComponent,
	        ref DynamicBuffer<GasDataBuffer> gasDataBuffer, 
	        in MetaDataTileComponent metaDataTileComponent,
	        in DynamicBuffer<MetaDataTileBuffer> metaDataTileBuffers,
	        in BufferFromEntity<GasDataBuffer> neighborGasDataBuffer,
	        in EntityManager entityManager,
	        in LocalToParent localToParent)
        {
            if(IsPressureChanged(in metaDataTileComponent, in metaDataTileBuffers, 
	            in entityManager, in localToParent, ref gasDataBuffer, in neighborGasDataBuffer,
	            out var windDirection, out var windForce) == false) return;
            
            
        }
        
        public static bool IsPressureChanged(in MetaDataTileComponent metaDataTileComponent,
	        in DynamicBuffer<MetaDataTileBuffer> metaDataTileBuffers, in EntityManager entityManager,
	        in LocalToParent localToParent, ref DynamicBuffer<GasDataBuffer> gasDataBuffer, 
	        in BufferFromEntity<GasDataBuffer> neighborGasDataBuffer,
	        out int2 windDirection, out float windForce)
		{
			var neighbors = metaDataTileBuffers;
			windDirection = int2.zero;
			var clampVector = int3.zero;
			windForce = 0L;
			int3 localPos = new int3(localToParent.Position);
			bool result = false;

			for (var i = 0; i < neighbors.Length; i++)
			{
				var neighborEntity = neighbors[i].DataTile;
				var neighborGasMix = entityManager.GetComponentData<GasMixComponent>(neighborEntity);
				var neighborTileData = entityManager.GetComponentData<MetaDataTileComponent>(neighborEntity);
				var neighborLocalPos = new int3(entityManager.GetComponentData<LocalToParent>(neighborEntity).Position);

				//We only need to check open tiles
				if (neighborTileData.IsOccupied) continue;

				float pressureDifference = neighborGasMix.Pressure - neighborGasMix.Pressure;
				float absoluteDifference = math.abs(pressureDifference);

				//Check to see if theres a large pressure difference
				if (absoluteDifference > AtmosConstants.MinPressureDifference)
				{
					result = true;

					if (absoluteDifference > windForce)
					{
						windForce = absoluteDifference;
					}

					int neighborOffsetX = (neighborLocalPos.x - localPos.x);
					int neighborOffsetY = (neighborLocalPos.y - localPos.y);

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

				var neighborBuffer = neighborGasDataBuffer[neighborEntity];

				//Current node
				//Only need to check if false
				if (result == false)
				{
					for (int j = gasDataBuffer.Length - 1; j >= 0; j--)
					{
						var gas = gasDataBuffer[j].GasData;
						var moles = gas.Moles;
						float molesNeighbor = neighborBuffer.GetGasMoles(gas.GasSO);

						if (math.abs(moles - molesNeighbor) > AtmosConstants.MinPressureDifference)
						{
							result = true;

							//We break not return here so we can still work out wind direction
							break;
						}
					}
				}

				//Neighbor node
				//Only need to check if false
				if (result == false)
				{
					for (int j = 0; j < neighborBuffer.Length; j++)
					{
						var gas = gasDataBuffer[j].GasData;
						float molesNeighbor = gas.Moles;
						float moles = gasDataBuffer.GetGasMoles(gas.GasSO);

						if (math.abs(moles - molesNeighbor) > AtmosConstants.MinPressureDifference)
						{
							result = true;

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

			return result;
		}
    }
}
