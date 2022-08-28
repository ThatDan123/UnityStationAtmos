using System;
using Unity.Entities;

namespace ECSAtmos.DataTypes
{
    [Serializable]
    public struct GasData
    {
	    //Moles of this gas type
        public float Moles;

        public byte GasSO => GasInfo.GasSO;

        public GasInfo GasInfo => GasInfoReference.Value;

        public BlobAssetReference<GasInfo> GasInfoReference;
    }
}
