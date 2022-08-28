using Systems.Atmospherics;
using ECSAtmos.DataTypes;
using Unity.Burst;
using Unity.Entities;

namespace ECSAtmos.Components
{
    public struct GasMixComponent : IComponentData
    {
        //Note: GasDataBuffer holds the gas data for this component as we can't use native arrays inside components

        /// <summary>In moles. CALL RECALCULATE BEFORE USE</summary>
        public float Moles{ get; private set; }

        /// <summary>In kPa.</summary>
        public float Pressure;

        /// <summary>In cubic metres.</summary>
        public float Volume;

        /// <summary>In Kelvin.</summary>
        public float Temperature;

        /// <summary>In Joules/Kelvin. CALL RECALCULATE BEFORE USE</summary>
        public float WholeHeatCapacity{ get; private set; }

        /// <summary>In Joules?. CALL RECALCULATE BEFORE USE</summary>
        public float InternalEnergy { get; private set; }

        public GasMixComponent(in DynamicBuffer<GasDataBuffer> buffer,
            float pressure = 0, float volume = AtmosConstants.TileVolume, float temperature = AtmosConstants.KOffsetC + 20)
        {
            Pressure = pressure;
            Volume = volume;
            Temperature = temperature;

            Moles = 0;
            InternalEnergy = 0;
            WholeHeatCapacity = 0;

            SetValues(in buffer);
        }

        public void SetValues(in DynamicBuffer<GasDataBuffer> gasDataBuffer)
        {
	        Moles = 0;
	        WholeHeatCapacity = 0;
	        InternalEnergy = 0;

	        for (int i = 0; i < gasDataBuffer.Length; i++)
	        {
		        var gas = gasDataBuffer[i];
		        Moles += gas.GasData.Moles;
		        WholeHeatCapacity += gas.GasData.GasInfo.MolarHeatCapacity * gas.GasData.Moles;
	        }

	        if (float.IsNaN(Moles))
	        {
		        Moles = 0;
	        }

	        InternalEnergy = WholeHeatCapacity * Temperature;
        }
    }

    [BurstCompile]
    public static class GasMixUtil
    {
        public static void Recalculate(this ref GasMixComponent gasMixComponent, in DynamicBuffer<GasDataBuffer> gasDataBuffer)
        {
	        SetValues(ref gasMixComponent, in gasDataBuffer);
        }

        private static void SetValues(ref GasMixComponent gasMixComponent, in DynamicBuffer<GasDataBuffer> gasDataBuffer)
        {
	        gasMixComponent.SetValues(in gasDataBuffer);
        }

        public static void CalcPressure(this ref GasMixComponent gasMixComponent, in DynamicBuffer<GasDataBuffer> gasDataBuffer)
        {
	        gasMixComponent.Recalculate(in gasDataBuffer);
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

        public static void CalcVolume(this ref GasMixComponent gasMixComponent, in DynamicBuffer<GasDataBuffer> gasDataBuffer)
        {
	        gasMixComponent.Recalculate(in gasDataBuffer);
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

        // public static void CalcMoles(this ref GasMixComponent gasMixComponent, in DynamicBuffer<GasDataBuffer> gasDataBuffer)
        // {
        // gasMixComponent.Recalculate(in gasDataBuffer);
        //     gasMixComponent.Moles = CalcMoles(gasMixComponent.Pressure, gasMixComponent.Volume, gasMixComponent.Temperature);
        // }

        public static float CalcMoles(float pressure, float volume, float temperature)
        {
            if (temperature > 0 && pressure > 0 && volume > 0)
            {
                return pressure * volume / (AtmosConstants.R * temperature) * 1000;
            }

            return 0;
        }

        public static void CalcTemperature(this ref GasMixComponent gasMixComponent, in DynamicBuffer<GasDataBuffer> gasDataBuffer)
        {
	        gasMixComponent.Recalculate(in gasDataBuffer);
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

        public static void SetTemperature(this ref GasMixComponent gasMixComponent, in DynamicBuffer<GasDataBuffer> gasDataBuffer, float newTemperature)
        {
	        if (newTemperature < AtmosDefines.SPACE_TEMPERATURE)
	        {
		        gasMixComponent.Temperature = AtmosDefines.SPACE_TEMPERATURE;
	        }
	        else
	        {
		        gasMixComponent.Temperature = newTemperature;
	        }

            gasMixComponent.CalcPressure(in gasDataBuffer);
        }

        public static void SetPressure(this ref GasMixComponent gasMixComponent, in DynamicBuffer<GasDataBuffer> gasDataBuffer, float newPressure)
        {
            gasMixComponent.Pressure = newPressure;
            gasMixComponent.CalcTemperature(in gasDataBuffer);
        }
    }

    public struct GasDataBuffer : IBufferElementData
    {
        public GasData GasData;
    }
}
