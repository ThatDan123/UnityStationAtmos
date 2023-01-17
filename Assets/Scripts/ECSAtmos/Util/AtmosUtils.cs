using System.Text;
using ECSAtmos.Components;
using ECSAtmos.DataTypes;
using ECSAtmos.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Systems.ECSAtmos
{
	[BurstCompile]
	public static unsafe class AtmosUtils
	{
		/// <summary>
		/// Checks to see if the gas mix contains a specific gas
		/// </summary>
		public static bool HasGasType(this in DynamicBuffer<GasDataBuffer> data, byte gasType)
		{
			foreach (var gas in data)
			{
				if(gas.GasData.GasSO != gasType) continue;

				return true;
			}

			return false;
		}

		/// <summary>
		/// Gets the gas info of a specific gas from the gas array, check before if theres actually moles of this gas,
		/// or this wont work (or check for GasSO = 0 == invalid). Use the ArchetypeSystem store instead if you really need GasInfo
		/// </summary>
		public static BlobAssetReference<GasInfo> GetInfo(this in DynamicBuffer<GasDataBuffer> data, byte gasType)
		{
			return GetGasType(in data, gasType)?.GasInfoReference ?? new BlobAssetReference<GasInfo>();
		}

		/// <summary>
		/// Gets moles of a specific gas from the gas array, returns 0 if gas isn't in mix
		/// </summary>
		public static float GetMoles(this in DynamicBuffer<GasDataBuffer> data, byte gasType)
		{
			return GetGasType(in data, gasType)?.Moles ?? 0;
		}

		/// <summary>
		/// Gets moles of a specific gas from the gas array, returns 0 if gas isn't in mix
		/// </summary>
		public static void GetMoles(this in DynamicBuffer<GasDataBuffer> data, byte gasType, out float gasMoles)
		{
			gasMoles = GetMoles(in data, gasType);
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
		/// Removes moles for a specific gas in the gas data (doesnt accept negatives)
		/// </summary>
		public static void RemoveMoles(this ref DynamicBuffer<GasDataBuffer> data,
			in BlobAssetReference<GasInfo> gasInfo, in byte gasType, in float moles)
		{
			if(moles < 0) return;

			ChangeMoles(ref data, in gasInfo, in gasType, -moles);
		}

		/// <summary>
		/// Adds moles for a specific gas in the gas data (doesnt accept negatives)
		/// </summary>
		public static void AddMoles(this ref DynamicBuffer<GasDataBuffer> data,
			in BlobAssetReference<GasInfo> gasInfo, in byte gasType, in float moles)
		{
			if(moles < 0) return;

			ChangeMoles(ref data, in gasInfo, in gasType, in moles);
		}

		/// <summary>
		/// Multiply moles for a specific gas in the gas data
		/// </summary>
		public static void MultiplyMoles(this ref DynamicBuffer<GasDataBuffer> data,
			in BlobAssetReference<GasInfo> gasInfo, in byte gasType, in float factor)
		{
			var moles = data.GetMoles(gasType);

			moles *= factor;

			SetMoles(ref data, in gasInfo, in gasType, in moles);
		}

		/// <summary>
		/// Divide moles for a specific gas in the gas data
		/// </summary>
		public static void DivideMoles(this ref DynamicBuffer<GasDataBuffer> data,
			in BlobAssetReference<GasInfo> gasInfo, in byte gasType, in float factor)
		{
			if(factor.Approx(0)) return;

			var moles = data.GetMoles(gasType);

			moles /= factor;

			SetMoles(ref data, in gasInfo, in gasType, in moles);
		}

		/// <summary>
		/// Adds/Removes moles for a specific gas in the gas data
		/// </summary>
		public static void ChangeMoles(this ref DynamicBuffer<GasDataBuffer> data,
			in BlobAssetReference<GasInfo> gasInfo, in byte gasType, in float moles)
		{
			InternalSetMoles(ref data, in gasInfo, in gasType, in moles, true);
		}

		/// <summary>
		/// Sets moles for a specific gas to a specific value in the gas data
		/// </summary>
		public static void SetMoles(this ref DynamicBuffer<GasDataBuffer> data,
			in BlobAssetReference<GasInfo> gasInfo, in byte gasType, in float moles)
		{
			InternalSetMoles(ref data, in gasInfo, in gasType, in moles, false);
		}

		private static void InternalSetMoles(ref DynamicBuffer<GasDataBuffer> data, in BlobAssetReference<GasInfo> gasInfo,
			in byte gasType, in float moles, in bool isChange)
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
					dy[i] = new GasData
					{
						Moles = tempGasData.Moles,
						GasInfoReference = tempGasData.GasInfoReference
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
			data.Add(new GasDataBuffer
			{
				GasData = new GasData
				{
					Moles = moles,
					GasInfoReference = gasInfo
				}
			});
		}

		/// <summary>
		/// Copies all gases from one and adds to another, changes temp and pressure if needed
		/// Does not remove from old mix!
		/// </summary>
		public static void TransferAllGas(
			ref DynamicBuffer<GasDataBuffer> oldGasData, ref GasMixComponent oldGasMix,
			ref DynamicBuffer<GasDataBuffer> newGasData, ref GasMixComponent newGasMix)
		{
			oldGasData.AddTo(ref newGasData);
			newGasMix.Recalculate(in newGasData);

			if (Approx(oldGasMix.Temperature, newGasMix.Temperature))
			{
				newGasMix.CalcPressure(in newGasData);
			}
			else
			{
				var energyTarget = newGasMix.Moles * newGasMix.Temperature;
				var energyTransfer = newGasMix.Moles * oldGasMix.Temperature;
				var targetTempFinal = (energyTransfer + energyTarget) / (newGasMix.Moles + newGasMix.Moles);
				newGasMix.SetTemperature(in newGasData, targetTempFinal);
			}
		}

		/// <summary>
		/// Transfers moles from one gas to another
		/// </summary>
		public static void TransferGases(in Entity sourceEntity, in Entity targetEntity,
			ref DynamicBuffer<GasDataBuffer> sourceGasData, ref GasMixComponent sourceGasMix,
			ref DynamicBuffer<GasDataBuffer> targetGasData, ref GasMixComponent targetGasMix,
			float molesToTransfer,
			bool doNotTouchOriginalMix = false)
		{
			if (targetEntity == sourceEntity) return;

			var sourceStartMoles = sourceGasMix.Moles;

			molesToTransfer = math.clamp(molesToTransfer, 0, sourceStartMoles);

			if (molesToTransfer.Approx(0) || sourceStartMoles.Approx(0)) return;

			var ratio = molesToTransfer / sourceStartMoles;
			var targetStartMoles = targetGasMix.Moles;

			var length = sourceGasData.Length;
			for (int i = length - 1; i >= 0; i--)
			{
				var gas = sourceGasData[i].GasData;
				if (gas.GasSO == 0) continue;

				var sourceMoles = sourceGasData.GetMoles(gas.GasSO);
				if (sourceMoles.Approx(0)) continue;

				var sourceGasInfo = sourceGasData.GetInfo(gas.GasSO);

				var transfer = sourceMoles * ratio;

				//Add to target
				targetGasData.ChangeMoles(in sourceGasInfo, gas.GasSO, in transfer);

				if (doNotTouchOriginalMix == false)
				{
					//Remove from source
					sourceGasData.ChangeMoles(in sourceGasInfo, gas.GasSO, -transfer);
				}
			}

			if (targetGasMix.Temperature.Approx(sourceGasMix.Temperature))
			{
				targetGasMix.CalcPressure(in targetGasData);
			}
			else
			{
				var energyTarget = targetStartMoles * targetGasMix.Temperature;
				var energyTransfer = molesToTransfer * sourceGasMix.Temperature;
				var targetTempFinal = (energyTransfer + energyTarget) / (targetStartMoles + molesToTransfer);
				targetGasMix.SetTemperature(in targetGasData, targetTempFinal);
			}

			if (doNotTouchOriginalMix == false)
			{
				if (ratio.Approx(1)) //transferred everything, source is empty
				{
					sourceGasMix.SetPressure(in sourceGasData, 0);
				}
				else
				{
					sourceGasMix.CalcPressure(in sourceGasData);
				}
			}

			var em = World.DefaultGameObjectInjectionWorld.EntityManager;

			em.SetComponentData(sourceEntity, sourceGasMix);
			em.SetComponentData(targetEntity, targetGasMix);
		}

		[BurstDiscard]
		public static void TransferSpecifiedTo(in Entity sourceEntity, in Entity targetEntity,
			ref DynamicBuffer<GasDataBuffer> sourceGasData, ref GasMixComponent sourceGasMix,
			ref DynamicBuffer<GasDataBuffer> targetGasData, ref GasMixComponent targetGasMix,
			byte gas, float molesToTransfer = -1)
		{
			if(sourceEntity == targetEntity) return;

			//We can grab the GasInfo from the source itself since it needs to exits to transfer, don't need to ask ArchetypeSystem
			var info = sourceGasData.GetInfo(gas);

			//Invalid info, gas wasn't in source, dont even bother transferring
			if(info.Value.GasSO == 0) return;

			var targetStartMoles = targetGasMix.Moles;

			float toRemoveGas = 0;
			if (molesToTransfer > 0)
			{
				toRemoveGas = molesToTransfer;
			}
			else
			{
				toRemoveGas = sourceGasData.GetMoles(gas);
			}

			sourceGasData.RemoveMoles(in info, in gas, in toRemoveGas);
			targetGasData.AddMoles(in info, in gas, in toRemoveGas);

			if (targetGasMix.Temperature.Approx(sourceGasMix.Temperature))
			{
				targetGasMix.CalcPressure(in targetGasData);
			}
			else
			{
				var energyTarget = targetStartMoles * targetGasMix.Temperature;
				var energyTransfer = molesToTransfer * sourceGasMix.Temperature;
				var targetTempFinal = (energyTransfer + energyTarget) / (targetStartMoles + molesToTransfer);
				targetGasMix.SetTemperature(in targetGasData, targetTempFinal);
			}

			sourceGasMix.Recalculate(in sourceGasData);

			if (sourceGasMix.Moles.Approx(0))
			{
				//Transferred everything, source is empty
				sourceGasMix.SetPressure(in sourceGasData, 0);
			}
			else
			{
				sourceGasMix.CalcPressure(in sourceGasData);
			}
		}

		/// <summary>
		/// Copies gases from one and adds to another (USE TransferMoles AT RUNTIME, THIS HAS NO CHECKS)
		/// </summary>
		public static void AddTo(this ref DynamicBuffer<GasDataBuffer> oldData, ref DynamicBuffer<GasDataBuffer> copyTo)
		{
			for (int i = oldData.Length - 1; i >= 0; i--)
			{
				copyTo.ChangeMoles(oldData[i].GasData.GasInfoReference, oldData[i].GasData.GasSO, oldData[i].GasData.Moles);
			}
		}

		/// <summary>
		/// Returns the gas as a percentage of the gas in the mix
		/// </summary>
		public static float GasRatio(this in DynamicBuffer<GasDataBuffer> buffer, in GasMixComponent gasMix, byte gasIndex)
		{
			return buffer.GetMoles(gasIndex) / gasMix.Moles;
		}

		/// <summary>
		/// Divide all gas moles
		/// </summary>
		public static void DivideAllGases(this ref DynamicBuffer<GasDataBuffer> data, float division)
		{
			if(division.Approx(0f)) return;

			foreach (var value in data)
			{
				data.SetMoles(value.GasData.GasInfoReference, value.GasData.GasSO, value.GasData.Moles / division);
			}
		}

		public static bool Approx(this float thisValue, float value)
		{
			return (double) math.abs(value - thisValue) < (double) math.max(1E-06f * math.max(math.abs(thisValue), math.abs(value)), math.EPSILON * 8f);
		}

		public static bool TryGetComponent<T>(this Entity entity, out T componentData) where T : unmanaged, IComponentData
		{
			if (World.DefaultGameObjectInjectionWorld.EntityManager.HasComponent<T>(entity) == false)
			{
				componentData = default;
				return false;
			}

			componentData = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<T>(entity);
			return true;
		}

		private static readonly int3 Up = new int3(0, 1, 0);
		private static readonly int3 Down = new int3(0, -1, 0);
		private static readonly int3 Left = new int3(-1, 0, 0);
		private static readonly int3 Right = new int3(1, 0, 0);

		public static bool IsOccupiedBlocked(in MetaDataTileComponent thisNode, in MetaDataTileComponent neighbourNode)
		{
			if (thisNode.OccupiedType == NodeOccupiedType.None) return false;
			if (thisNode.OccupiedType == NodeOccupiedType.Full) return true;

			var direction = (neighbourNode.TileLocalPos - thisNode.TileLocalPos);

			if (direction.Equals(Up))
			{
				return (thisNode.OccupiedType & NodeOccupiedType.Up) != 0;
			}

			if (direction.Equals(Down))
			{
				return (thisNode.OccupiedType & NodeOccupiedType.Down) != 0;
			}

			if (direction.Equals(Left))
			{
				return (thisNode.OccupiedType & NodeOccupiedType.Left) != 0;
			}

			if (direction.Equals(Right))
			{
				return (thisNode.OccupiedType & NodeOccupiedType.Right) != 0;
			}

			return false;
		}

		public static string ToStringFull(this NodeOccupiedType type)
		{
			if (type == NodeOccupiedType.None) return type.ToString();
			if (type == NodeOccupiedType.Full) return type.ToString();

			var stringBuilder = new StringBuilder();

			if ((type & NodeOccupiedType.Up) != 0)
			{
				stringBuilder.AppendLine(NodeOccupiedType.Up.ToString());
			}

			if ((type & NodeOccupiedType.Down) != 0)
			{
				stringBuilder.AppendLine(NodeOccupiedType.Down.ToString());
			}

			if ((type & NodeOccupiedType.Right) != 0)
			{
				stringBuilder.AppendLine(NodeOccupiedType.Left.ToString());
			}

			if ((type & NodeOccupiedType.Up) != 0)
			{
				stringBuilder.AppendLine(NodeOccupiedType.Right.ToString());
			}

			return stringBuilder.ToString();
		}

		[BurstDiscard]
		public static BlobAssetReference<GasInfo> CreateGasInfoBlob(byte newIndex, GasInfo gasInfoToUse)
		{
			// Create a new builder that will use temporary memory to construct the blob asset
			var builder = new BlobBuilder(Allocator.Temp);

			// Construct the root object for the blob asset. Notice the use of `ref`.
			ref GasInfo gasInfo = ref builder.ConstructRoot<GasInfo>();

			gasInfo.MolarHeatCapacity = gasInfoToUse.MolarHeatCapacity;
			gasInfo.MolarMass = gasInfoToUse.MolarMass;
			gasInfo.FusionPower = gasInfoToUse.FusionPower;
			gasInfo.GasSO = newIndex;

			// Now copy the data from the builder into its final place, which will
			// use the persistent allocator
			var result = builder.CreateBlobAssetReference<GasInfo>(Allocator.Persistent);

			var archetypeSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ArchetypeSystem>();
			archetypeSystem.GasReferences.Add(newIndex, result);

			builder.Dispose();
			return result;
		}
	}
}