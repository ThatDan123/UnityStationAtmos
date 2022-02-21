using ECSAtmos.Components;
using ECSAtmos.DataTypes;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Systems.Atmospherics
{
	[BurstCompile]
	public static unsafe class AtmosUtils
	{
		public static readonly Vector2Int MINUS_ONE = new Vector2Int(-1, -1);

		/// <summary>
		/// Checks to see if the gas mix contains a specific gas
		/// </summary>
		public static bool HasGasType(this ref DynamicBuffer<GasDataBuffer> data, byte gasType)
		{
			foreach (var gas in data)
			{
				if(gas.GasData.GasSO != gasType) continue;

				return true;
			}
			
			return false;
		}

		/// <summary>
		/// Gets moles of a specific gas from the gas array, returns 0 if gas isn't in mix
		/// </summary>
		public static float GetGasMoles(this in DynamicBuffer<GasDataBuffer> data, byte gasType)
		{
			return GetGasType(in data, gasType)?.Moles ?? 0;
		}

		/// <summary>
		/// Gets moles of a specific gas from the gas array, returns 0 if gas isn't in mix
		/// </summary>
		public static void GetGasMoles(this in DynamicBuffer<GasDataBuffer> data, byte gasType, out float gasMoles)
		{
			gasMoles = GetGasMoles(in data, gasType);
		}

		/// <summary>
		/// Gets a specific gas from the gas array, returns null if gas isn't in mix
		/// </summary>
		public static void GetGasType(this in DynamicBuffer<GasDataBuffer> data, byte gasType, out GasData? gasData)
		{
			gasData = GetGasType(in data, gasType);
		}

		/// <summary>
		/// Gets a specific gas from the gas array, returns null if gas isn't in mix
		/// </summary>
		private static GasData? GetGasType(this in DynamicBuffer<GasDataBuffer> data, byte gasType)
		{
			for (int i = 0; i < data.Length; i++)
			{
				if(data[i].GasData.GasSO != gasType) continue;
				
				return data[i].GasData;
			}

			return null;
		}

		/// <summary>
		/// Adds/Removes moles for a specific gas in the gas data
		/// </summary>
		public static void ChangeMoles(this ref DynamicBuffer<GasDataBuffer> data, byte gasType, float moles)
		{
			InternalSetMoles(ref data, gasType, moles, true);
		}

		/// <summary>
		/// Sets moles for a specific gas to a specific value in the gas data
		/// </summary>
		public static void SetMoles(this ref DynamicBuffer<GasDataBuffer> data, byte gasType, float moles)
		{
			InternalSetMoles(ref data, gasType, moles, false);
		}

		private static void InternalSetMoles(ref DynamicBuffer<GasDataBuffer> data, byte gasType, float moles, bool isChange)
		{
			//Try to get gas value if already inside mix
			GetGasType(in data, gasType, out var gas);

			if (gas != null)
			{
				var tempGasData = gas.Value;
				
				if (isChange)
				{
					tempGasData.Moles += moles;
				}
				else
				{
					tempGasData.Moles = moles;
				}

				var index = -1;

				for (int i = 0; i < data.Length; i++)
				{
					//Find the gas we changed
					if(data[i].GasData.GasSO != tempGasData.GasSO) continue;

					if (tempGasData.Moles <= AtmosConstants.MinPressureDifference)
					{
						//If too low moles set index for removal
						index = i;
						break;
					}

					//Otherwise update the value
					var dy = data.Reinterpret<GasData>();
					dy[i] = new GasData()
					{
						GasSO = tempGasData.GasSO,
						//TODO look this up in SO?
						MolarHeatCapacity = tempGasData.MolarHeatCapacity,
						Moles = tempGasData.Moles
					};
					
					break;
				}

				//If needed to remove do so now
				//TODO might be able to put this in the loop as we break right after so wont iteration error?
				if (index != -1)
				{
					data.RemoveAt(index);
				}

				return;
			}

			//Gas isn't inside mix so we'll add it

			//Dont add new data for negative moles
			if ((int)math.sign(moles) == -1) return;

			//Dont add if approx 0 or below threshold
			if (moles.Approx(0) || moles <= AtmosConstants.MinPressureDifference) return;

			//Can add directly don't need to do them all again
			data.Add(new GasDataBuffer()
			{
				GasData = new GasData()
				{
					GasSO = gasType,
					//TODO look this up in SO?
					MolarHeatCapacity = 20,
					Moles = moles
				}
			});
		}
		
		/// <summary>
		/// Copies all gases from one and adds to another, changes temp and pressure if needed
		/// </summary>
		public static void TransferAllGas(
			ref DynamicBuffer<GasDataBuffer> oldGasData, ref GasMixComponent oldGasMix,
			ref DynamicBuffer<GasDataBuffer> newGasData, ref GasMixComponent newGasMix)
		{
			oldGasData.CopyTo(ref newGasData);
			newGasMix.ReCalculate(in newGasData);
			
			if (Approx(oldGasMix.Temperature, newGasMix.Temperature))
			{
				newGasMix.CalcPressure();
			}
			else
			{
				var energyTarget = newGasMix.Moles * newGasMix.Temperature;
				var energyTransfer = newGasMix.Moles * oldGasMix.Temperature;
				var targetTempFinal = (energyTransfer + energyTarget) / (newGasMix.Moles + newGasMix.Moles);
				newGasMix.SetTemperature(targetTempFinal);
			}
		}
		
		/// <summary>
		/// Copies gases from one and adds to another
		/// </summary>
		public static void CopyTo(this ref DynamicBuffer<GasDataBuffer> oldData, ref DynamicBuffer<GasDataBuffer> copyTo)
		{
			for (int i = oldData.Length - 1; i >= 0; i--)
			{
				copyTo.ChangeMoles(oldData[i].GasData.GasSO, oldData[i].GasData.Moles);
			}
		}
		
		/// <summary>
		/// Divide all gas moles
		/// </summary>
		public static void DivideAllGases(this ref DynamicBuffer<GasDataBuffer> data, float division)
		{
			if(division.Approx(0f)) return;
			
			foreach (var value in data)
			{
				data.SetMoles(value.GasData.GasSO, value.GasData.Moles / division);
			}
		}
		
		//TODO rotation shit :(
		public static bool TileAllowed(this ref MetaDataTileComponent metaDataTileComponent)
		{
			if (metaDataTileComponent.IsOccupied) return false;
		        
			if (metaDataTileComponent.BlockType == BlockType.All) return false;

			return true;
		}
		
		public static bool Approx(this float thisValue, float value)
		{
			return (double) Mathf.Abs(value - thisValue) < (double) math.max(1E-06f * math.max(math.abs(thisValue), math.abs(value)), math.EPSILON * 8f);
		}
		
		public static bool TryGetComponent<T>(this Entity entity, out T componentData) where T : struct, IComponentData
		{
			if (World.DefaultGameObjectInjectionWorld.EntityManager.HasComponent<T>(entity) == false)
			{
				componentData = default;
				return false;
			}
			
			componentData = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<T>(entity);;
			return true;
		}
	}
}