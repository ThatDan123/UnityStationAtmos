using Systems.Atmospherics;
using ECSAtmos.DataTypes;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ECSAtmos.Components
{
    public struct GasMixComponent : IComponentData
    {
        //Note: GasDataBuffer holds the gas data for this component as we can't use native arrays inside components
        
        /// <summary>In moles.</summary>
        public float Moles;
        
        /// <summary>In kPa.</summary>
        public float Pressure;

        /// <summary>In cubic metres.</summary>
        public float Volume;

        /// <summary>In Kelvin.</summary>
        public float Temperature;
        
        /// <summary>In Joules/Kelvin.</summary>
        public float WholeHeatCapacity;
        
        /// <summary>In Joules?.</summary>
        internal float InternalEnergy;

        public GasMixComponent(ref DynamicBuffer<GasDataBuffer> buffer, 
            float pressure = 0, float volume = AtmosConstants.TileVolume, float temperature = AtmosConstants.KOffsetC + 20)
        {
            Pressure = pressure;
            Volume = volume;
            Temperature = temperature;

            Moles = 0;
            InternalEnergy = 0;
            WholeHeatCapacity = 0;
            this.ReCalculate(in buffer);
        }
    }

    [BurstCompile]
    public static class GasMixUtil
    {
        public static void ReCalculate(this ref GasMixComponent gasMixComponent, in DynamicBuffer<GasDataBuffer> gasDataBuffer)
        {
            ResetValues(ref gasMixComponent);
            SetValues(ref gasMixComponent, in gasDataBuffer);
        }
        
        private static void ResetValues(ref GasMixComponent gasMixComponent)
        {
            gasMixComponent.Moles = 0;
            gasMixComponent.WholeHeatCapacity = 0;
            gasMixComponent.InternalEnergy = 0;
        }

        private static void SetValues(ref GasMixComponent gasMixComponent, in DynamicBuffer<GasDataBuffer> gasDataBuffer)
        {
            for (int i = 0; i < gasDataBuffer.Length; i++)
            {
                var gas = gasDataBuffer[i];
                gasMixComponent.Moles += gas.GasData.Moles;
                gasMixComponent.WholeHeatCapacity += gas.GasData.MolarHeatCapacity * gas.GasData.Moles;
            }
            
            if (float.IsNaN(gasMixComponent.Moles))
            {
                gasMixComponent.Moles = 0;
            }
            
            gasMixComponent.InternalEnergy = gasMixComponent.WholeHeatCapacity * gasMixComponent.Temperature;
        }

        public static void CalcPressure(this ref GasMixComponent gasMixComponent)
        {
            gasMixComponent.Pressure = CalcPressure(gasMixComponent.Volume, gasMixComponent.Moles, gasMixComponent.Temperature);
        }
        
        public static float CalcPressure(float volume, float moles, float temperature)
        {
            if (temperature > 0 && moles > 0 && volume > 0)
            {
                return moles * AtmosConstants.R * temperature / volume / 1000;
            }

            return 0;
        }
        
        public static void CalcVolume(this ref GasMixComponent gasMixComponent)
        {
            gasMixComponent.Volume = CalcVolume(gasMixComponent.Pressure, gasMixComponent.Moles, gasMixComponent.Temperature);
        }

        public static float CalcVolume(float pressure, float moles, float temperature)
        {
            if (temperature > 0 && pressure > 0 && moles > 0)
            {
                return moles * AtmosConstants.R * temperature / pressure;
            }

            return 0;
        }
        
        public static void CalcMoles(this ref GasMixComponent gasMixComponent)
        {
            gasMixComponent.Moles = CalcMoles(gasMixComponent.Pressure, gasMixComponent.Volume, gasMixComponent.Temperature);
        }

        public static float CalcMoles(float pressure, float volume, float temperature)
        {
            if (temperature > 0 && pressure > 0 && volume > 0)
            {
                return pressure * volume / (AtmosConstants.R * temperature) * 1000;
            }

            return 0;
        }
        
        public static void CalcTemperature(this ref GasMixComponent gasMixComponent)
        {
            gasMixComponent.Temperature = CalcTemperature(gasMixComponent.Pressure, gasMixComponent.Volume, gasMixComponent.Moles);
        }

        public static float CalcTemperature(float pressure, float volume, float moles)
        {
            if (volume > 0 && pressure > 0 && moles > 0)
            {
                return pressure * volume / (AtmosConstants.R * moles) * 1000;
            }

            return AtmosDefines.SPACE_TEMPERATURE; //space radiation
        }
        
        public static void SetTemperature(this ref GasMixComponent gasMixComponent, float newTemperature)
        {
            gasMixComponent.Temperature = newTemperature;
            gasMixComponent.CalcPressure();
        }

        public static void SetPressure(this ref GasMixComponent gasMixComponent, float newPressure)
        {
            gasMixComponent.Pressure = newPressure;
            gasMixComponent.CalcTemperature();
        }
    }
    
    public struct GasDataBuffer : IBufferElementData
    {
        public GasData GasData;
    }
}
