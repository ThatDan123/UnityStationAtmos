using System;

namespace ECSAtmos.DataTypes
{
	[Serializable]
	public struct GasInfo
	{
		//This is how many Joules are needed to raise 1 mole of the gas 1 degree Kelvin: J/K/mol
		public float MolarHeatCapacity;

		//This is the mass, in grams, of 1 mole of the gas
		public float MolarMass;

		//Used for fusion reaction
		public int FusionPower;

		public byte GasSO { get; set; }

		public static implicit operator byte(GasInfo gas)
		{
			return gas.GasSO;
		}
	}
}