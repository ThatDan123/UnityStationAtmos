using System;

namespace ECSAtmos.DataTypes
{
    [Serializable]
    public struct GasData
    {
        public byte GasSO;

        //Moles of this gas type
        public float Moles;
        
        public float MolarHeatCapacity;
    }
}
