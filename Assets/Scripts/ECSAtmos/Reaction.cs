using System.Collections.Generic;
using ECSAtmos.Components;
using ECSAtmos.DataTypes;
using Unity.Collections;
using Unity.Entities;

namespace Systems.ECSAtmos
{
	public interface Reaction
	{
		void React(in NativeParallelHashMap<byte, BlobAssetReference<GasInfo>> gases,
			ref MetaDataTileComponent tileComponent, ref GasMixComponent gasMix, ref DynamicBuffer<GasDataBuffer> gasBuffer);
	}

	public struct GasReactions
	{
		private static List<GasReactions> gasReactions = new List<GasReactions>();

		//List of reactions which will be used to reset the gasReactions list so that custom reactions will be removed
		private static List<GasReactions> baseGasReactions = new List<GasReactions>();

		public static void Clear()
		{
			gasReactions.Clear();
		}

		//Gas and minimum moles to react
		public Dictionary<byte, GasReactionData> GasReactionData;

		public Reaction Reaction;

		public float MinimumTileTemperature;
		public float MaximumTileTemperature;

		public float MinimumTilePressure;
		public float MaximumTilePressure;

		public float MinimumTileMoles;
		public float MaximumTileMoles;

		public readonly int Index;

		public GasReactions(Dictionary<byte, GasReactionData> gasReactionData, Reaction reaction, float minimumTileTemperature, float maximumTileTemperature, float minimumTilePressure, float maximumTilePressure, float minimumTileMoles, float maximumTileMoles, bool addToBaseReactions = false)
		{
			GasReactionData = gasReactionData;

			Reaction = reaction;

			MinimumTileTemperature = minimumTileTemperature;
			MaximumTileTemperature = maximumTileTemperature;
			MinimumTilePressure = minimumTilePressure;
			MaximumTilePressure = maximumTilePressure;
			MinimumTileMoles = minimumTileMoles;
			MaximumTileMoles = maximumTileMoles;

			Index = gasReactions.Count;

			gasReactions.Add(this);

			if (addToBaseReactions)
			{
				baseGasReactions.Add(this);
			}

			SetAllToNull();
			numberOfGasReactions = 0;
		}

		public static GasReactions Get(int i)
		{
			return gasReactions[i];
		}

		public static GasReactions[] All
		{
			get
			{
				if (all == null)
				{
					all = gasReactions.ToArray();
				}
				return all;
			}
		}


		private static GasReactions[] all;


		public static int Count
		{
			get
			{
				if (numberOfGasReactions == 0)
				{
					numberOfGasReactions = gasReactions.Count;
				}
				return numberOfGasReactions;
			}
		}

		private static int numberOfGasReactions = 0;

		public static implicit operator int(GasReactions gasReaction)
		{
			return gasReaction.Index;
		}

		public static void RemoveReaction(GasReactions gasReaction)
		{
			gasReactions.Remove(gasReaction);
			SetAllToNull();
		}

		/// <summary>
		/// Removes all custom reactions which are added at runtime, only the reactions in this class will stay
		/// </summary>
		public static void ResetReactionList()
		{
			gasReactions = baseGasReactions;
			SetAllToNull();
		}

		private static void SetAllToNull()
		{
			if(all == null) return;

			lock (all)
			{
				all = null;
			}
		}
	}

	public struct GasReactionData
	{
		public float minimumMolesToReact;

		//unused
		public float ratio;
	}
}
