using Unity.Entities;

namespace ECSAtmos.Components
{
    public struct ConductivityComponent : IComponentData
    {
        //Temperature of the solid node
        public float ConductivityTemperature;
        //How easily the node conducts 0-1
        public float ThermalConductivity;
        //Heat capacity of the node, also effects conducting speed
        public float HeatCapacity;

        //If this node started the conductivity
        public bool StartingSuperConduct;
        //If this node is allowed to share temperature to surrounding nodes
        public bool AllowedToSuperConduct;
    }
}