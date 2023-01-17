using ECSAtmos.Components;
using ECSAtmos.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Systems.ECSAtmos.Other
{
	[BurstCompile]
	public class GasVessel
	{
		public float Temperature
		{
			[BurstCompile]
			get
			{
				var gasData = em.GetComponentData<GasMixComponent>(ContainerEntity);
				return gasData.Temperature;
			}

			[BurstCompile]
			set
			{
				var (gasMix, buffer) = GetGasData();
				gasMix.SetTemperature(in buffer, value);
				SetGasData(ref gasMix, in buffer);
			}
		}

		public float InternalEnergy
		{
			[BurstCompile]
			get
			{
				var gasData = em.GetComponentData<GasMixComponent>(ContainerEntity);
				return gasData.InternalEnergy;
			}

			[BurstCompile]
			set
			{
				var inWholeHeatCapacity = WholeHeatCapacity;
				if (inWholeHeatCapacity.Approx(0)) return;

				var temperature = (value / inWholeHeatCapacity);
				Temperature = temperature;
			}
		}

		public float Pressure
		{
			[BurstCompile]
			get
			{
				var gasData = em.GetComponentData<GasMixComponent>(ContainerEntity);
				return gasData.Pressure;
			}
		}

		public float Volume
		{
			[BurstCompile]
			get
			{
				var gasData = em.GetComponentData<GasMixComponent>(ContainerEntity);
				return gasData.Volume;
			}
		}

		public float Moles
		{
			[BurstCompile]
			get
			{
				var gasData = em.GetComponentData<GasMixComponent>(ContainerEntity);
				return gasData.Moles;
			}
		}

		public float WholeHeatCapacity
		{
			[BurstCompile]
			get
			{
				var gasData = em.GetComponentData<GasMixComponent>(ContainerEntity);
				return gasData.WholeHeatCapacity;
			}
		}

		public Entity ContainerEntity { get; private set; }

		private EntityManager em;

		private ArchetypeSystem archetypeSystem;

		public void CreateStorage()
		{
			archetypeSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ArchetypeSystem>();
			em = World.DefaultGameObjectInjectionWorld.EntityManager;
			ContainerEntity = archetypeSystem.CreateStorageEntity();
		}

		public void CreateAtmosTile(Entity atmosTileEntity)
		{
			archetypeSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ArchetypeSystem>();
			em = World.DefaultGameObjectInjectionWorld.EntityManager;
			ContainerEntity = atmosTileEntity;
		}

		public void CreatePipe(int x, int y)
		{
			archetypeSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ArchetypeSystem>();
			em = World.DefaultGameObjectInjectionWorld.EntityManager;
			ContainerEntity = archetypeSystem.CreatePipeEntity(x, y);
		}

        public (GasMixComponent gasMix, DynamicBuffer<GasDataBuffer> buffer) GetGasData(bool readOnly = false)
        {
        	var gasMix = em.GetComponentData<GasMixComponent>(ContainerEntity);
        	var buffer = em.GetBuffer<GasDataBuffer>(ContainerEntity, readOnly);

        	return (gasMix, buffer);
        }

        public DynamicBuffer<GasDataBuffer> GetGasBuffer(bool readOnly = false)
        {
	        var buffer = em.GetBuffer<GasDataBuffer>(ContainerEntity, readOnly);

	        return buffer;
        }

        public void SetGasData(ref GasMixComponent gasMix, in DynamicBuffer<GasDataBuffer> buffer)
        {
        	gasMix.Recalculate(in buffer);
        	em.SetComponentData(ContainerEntity, gasMix);

            //Dont need to set buffer back

            //Wake up entity
            WakeUp();
        }

        public void RecalculatePressure(ref GasMixComponent gasMix, in DynamicBuffer<GasDataBuffer> buffer)
        {
	        gasMix.CalcPressure(in buffer);
	        em.SetComponentData(ContainerEntity, gasMix);

	        //Wake up entity
	        WakeUp();
        }

        public float GetMoles(byte gas)
        {
	        return em.GetBuffer<GasDataBuffer>(ContainerEntity, true).GetMoles(gas);
        }

        public int GasCount()
        {
	        return em.GetBuffer<GasDataBuffer>(ContainerEntity, true).Length;
        }

        public float GetPressure(byte gas)
        {
	        if (Moles.Approx(0)) return 0;

	        return Pressure * (GetMoles(gas) / Moles);
        }

        public void AddGas(byte gas, float moles)
        {
        	var (gasMix, buffer) = GetGasData();

            var info = archetypeSystem.GasReferences[gas];

            buffer.AddMoles(info, gas, moles);

            RecalculatePressure(ref gasMix, in buffer);
        }

        public void RemoveGas(byte gas, float moles)
        {
        	var (gasMix, buffer) = GetGasData();

            var info = archetypeSystem.GasReferences[gas];

            buffer.RemoveMoles(info, gas, moles);

            RecalculatePressure(ref gasMix, in buffer);
        }

        /// <summary>
        /// Remove moles, is not split evenly will try take all from first gas, then next until done
        /// </summary>
        public void RemoveMoles(float moles)
        {
	        var (gasMix, buffer) = GetGasData();

	        if (gasMix.Moles < moles)
	        {
		        buffer.Clear();
		        RecalculatePressure(ref gasMix, in buffer);
		        return;
	        }

	        var amountLeft = moles;

	        foreach (var gas in buffer)
	        {
		        var molesOfGas = gas.GasData.Moles;
		        if (amountLeft - molesOfGas < 0)
		        {
			        buffer.RemoveMoles(gas.GasData.GasInfoReference, gas.GasData.GasSO, amountLeft);
			        break;
		        }

		        buffer.RemoveMoles(gas.GasData.GasInfoReference, gas.GasData.GasSO, molesOfGas);
		        amountLeft -= molesOfGas;
	        }

	        RecalculatePressure(ref gasMix, in buffer);
        }

        public void MultiplyGases(float factor)
        {
        	var (gasMix, buffer) = GetGasData();

        	for (int i = buffer.Length - 1; i >= 0; i--)
        	{
	            buffer.MultiplyMoles(buffer[i].GasData.GasInfoReference, buffer[i].GasData.GasSO, factor);
        	}

        	gasMix.SetPressure(in buffer,Pressure * factor);
            SetGasData(ref gasMix, in buffer);
        }

        public void DivideGases(float factor)
        {
        	if(factor.Approx(0)) return;

        	var (gasMix, buffer) = GetGasData();

        	for (int i = buffer.Length - 1; i >= 0; i--)
        	{
	            buffer.DivideMoles(buffer[i].GasData.GasInfoReference, buffer[i].GasData.GasSO, factor);
        	}

        	gasMix.SetPressure(in buffer,Pressure / factor);
            SetGasData(ref gasMix, in buffer);
        }

        [BurstDiscard]
        public void TransferGases(GasVessel targetVessel, float moles)
        {
	        var (sourceGasMix, sourceBuffer) = GetGasData();

	        //Don't bother if theres no moles to transfer
	        if(sourceGasMix.Moles.Approx(0)) return;

	        var (targetGasMix, targetBuffer) = targetVessel.GetGasData();

	        AtmosUtils.TransferGases(ContainerEntity, targetVessel.ContainerEntity,
		        ref sourceBuffer, ref sourceGasMix, ref targetBuffer, ref targetGasMix, moles);

	        //Wake up entities
	        SetGasData(ref sourceGasMix, in sourceBuffer);
	        targetVessel.SetGasData(ref targetGasMix, in targetBuffer);
        }

        [BurstDiscard]
        public void TransferSpecifiedTo(GasVessel targetVessel, byte gas, float moles)
        {
	        var (sourceGasMix, sourceBuffer) = GetGasData();

	        //Don't bother if theres no moles to transfer
	        if(sourceGasMix.Moles.Approx(0)) return;

	        //Don't bother if theres no moles of this gas to transfer
	        var molesOfGas = GetMoles(gas);
	        if(molesOfGas.Approx(0)) return;

	        if (molesOfGas < moles)
	        {
		        moles = molesOfGas;
	        }

	        var (targetGasMix, targetBuffer) = targetVessel.GetGasData();

	        AtmosUtils.TransferSpecifiedTo(ContainerEntity, targetVessel.ContainerEntity,
		        ref sourceBuffer, ref sourceGasMix, ref targetBuffer, ref targetGasMix, gas, moles);

	        RecalculatePressure(ref sourceGasMix, in sourceBuffer);
	        targetVessel.RecalculatePressure(ref targetGasMix, in targetBuffer);
        }

        /// <summary>
        /// Source and target GasVessels and interchange their moles
        /// </summary>
        /// <param name="otherGasVessel"></param>
        [BurstDiscard]
        public void MergeGasVessel(GasVessel otherGasVessel)
        {
	        var totalInternalEnergy = InternalEnergy + otherGasVessel.InternalEnergy;
	        var totalWholeHeatCapacity = WholeHeatCapacity + otherGasVessel.WholeHeatCapacity;
	        var newTemperature = totalWholeHeatCapacity > 0 ? totalInternalEnergy / totalWholeHeatCapacity : 0;
	        var totalVolume = Volume + otherGasVessel.Volume;

	        var (sourceGasMix, sourceBuffer) = GetGasData();

	        var (otherGasGasMix, otherGasBuffer) = otherGasVessel.GetGasData();

	        var gasesDone = new NativeList<byte>(sourceBuffer.Length, Allocator.Temp);

	        foreach (var gasData in sourceBuffer)
	        {
		        var gasMoles = gasData.GasData.Moles;
		        gasMoles += otherGasBuffer.GetMoles(gasData.GasData.GasSO);
		        gasMoles /= totalVolume;

		        sourceBuffer.SetMoles(gasData.GasData.GasInfoReference, gasData.GasData.GasSO, gasMoles * Volume);
		        otherGasBuffer.SetMoles(gasData.GasData.GasInfoReference, gasData.GasData.GasSO, gasMoles * otherGasVessel.Volume);

		        gasesDone.Add(gasData.GasData.GasSO);
	        }

	        foreach (var gasData in otherGasBuffer)
	        {
		        //Check if already merged
		        if (gasesDone.Contains(gasData.GasData.GasSO)) continue;

		        var gasMoles = gasData.GasData.Moles;
		        gasMoles += sourceBuffer.GetMoles(gasData.GasData.GasSO);
		        gasMoles /= totalVolume;

		        sourceBuffer.SetMoles(gasData.GasData.GasInfoReference, gasData.GasData.GasSO, gasMoles * Volume);
		        otherGasBuffer.SetMoles(gasData.GasData.GasInfoReference, gasData.GasData.GasSO, gasMoles * otherGasVessel.Volume);
	        }

	        gasesDone.Dispose();

	        RecalculatePressure(ref sourceGasMix, in sourceBuffer);
	        otherGasVessel.RecalculatePressure(ref otherGasGasMix, in otherGasBuffer);

	        Temperature = newTemperature;
	        otherGasVessel.Temperature = newTemperature;
        }

        public void CleanUp()
        {
	        if(World.DefaultGameObjectInjectionWorld == null) return;

	        //Get EntityManager manually as might be null on shutdown
	        em.DestroyEntity(ContainerEntity);
        }

        public void WakeUp()
        {
	        var isSleep = em.HasComponent<DeactivatedTag>(ContainerEntity);
	        if(isSleep == false) return;

	        var cmb = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<AtmosBeginningEntityCommandBufferSystem>()
		        .CreateCommandBuffer();

	        cmb.RemoveComponent<DeactivatedTag>(ContainerEntity);
        }

        /// <summary>
        /// Clear for reuse
        /// </summary>
        public void Clear()
        {
	        var (gasMix, buffer) = GetGasData();
	        buffer.Clear();
	        SetGasData(ref gasMix, in buffer);
        }
	}
}