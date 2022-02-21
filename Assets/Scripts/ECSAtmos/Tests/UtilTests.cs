using System;
using Systems.Atmospherics;
using ECSAtmos.Components;
using ECSAtmos.DataTypes;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Tests;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ECSAtmos.Tests
{
    [TestFixture]
    public class UtilTests : ECSTestsFixture
    {
        [Test]
        public void DivideTest()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity);
            var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);
            
            buffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 0, MolarHeatCapacity = 20, Moles = 20}
            });
            
            buffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 1, MolarHeatCapacity = 20, Moles = 40}
            });
            
            buffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 2, MolarHeatCapacity = 20, Moles = -10}
            });
            
            var divider = 4;
            
            buffer.DivideAllGases(divider);

            Assert.IsTrue(buffer[0].GasData.Moles.Approx(5), "20 / 4");
            
            Assert.IsTrue(buffer[1].GasData.Moles.Approx(10), "40 / 4");
            
            Assert.IsTrue(buffer.Length == 2, "-10 / 4 (Index will be removed)");
        }
        
        [Test]
        public void CopyToTest()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity);
            var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);
            
            buffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 0, MolarHeatCapacity = 20, Moles = 20}
            });
            
            buffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 1, MolarHeatCapacity = 20, Moles = 45}
            });
            
            var entity2 = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity2);
            var buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);
            
            buffer2.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 0, MolarHeatCapacity = 20, Moles = 20}
            });
            
            buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);
            buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);
            
            buffer.CopyTo(ref buffer2);

            Assert.IsTrue(buffer2[0].GasData.Moles.Approx(40), "20 + 20");
            
            Assert.IsTrue(buffer[1].GasData.Moles.Approx(45), "45 + 0");
            
            Assert.IsTrue(buffer.Length == 2, "Only two gases in them");
        }
        
        [Test]
        public void TransferTest()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity);
            var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);
            m_Manager.AddComponentData(entity, new GasMixComponent(ref buffer, volume: 2.5f, temperature: 273.15f, pressure: 273.15f));
            buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);
            
            buffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 0, MolarHeatCapacity = 20, Moles = 20}
            });
            
            buffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 1, MolarHeatCapacity = 20, Moles = 40}
            });
            
            var entity2 = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity2);
            var buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);
            m_Manager.AddComponentData(entity2, new GasMixComponent(ref buffer2, volume: 2.5f, temperature: 273.15f, pressure: 273.15f));
            buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);
            
            buffer2.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 0, MolarHeatCapacity = 20, Moles = 20}
            });
            
            var gasMix2 = m_Manager.GetComponentData<GasMixComponent>(entity2);
            var gasMix = m_Manager.GetComponentData<GasMixComponent>(entity);
            buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);
            buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

            AtmosUtils.TransferAllGas(ref buffer, ref gasMix, ref buffer2, ref gasMix2);

            Assert.IsTrue(buffer2[0].GasData.Moles.Approx(40), "40");
            
            Assert.IsTrue(buffer2[1].GasData.Moles.Approx(40), "40");
            
            Assert.IsTrue(buffer.Length == 2, "Only two gases");
        }
        
        [Test]
        public void SetMolesTest()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity);
            var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);
            
            buffer.SetMoles(0, 23.5f);
            buffer.SetMoles(1, 15f);
            
            Assert.IsTrue(buffer[0].GasData.Moles.Approx(23.5f), "23.5f");
            Assert.IsTrue(buffer[1].GasData.Moles.Approx(15f), "15f");
            
            buffer.SetMoles(1, 10f);

            Assert.IsTrue(buffer[1].GasData.Moles.Approx(10), "Set from 15 to 10");

            Assert.IsTrue(buffer.Length == 2, "Only Two Gases");
            
            buffer.SetMoles(0, 0);
            
            Assert.IsTrue(buffer.Length == 1, "Set gas to 0 moles");
            
            buffer.SetMoles(1, -10);
            
            Assert.IsTrue(buffer.Length == 0, "Set gas to negative moles");
        }
        
        [Test]
        public void ChangeMolesTest()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity);
            var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);
            
            buffer.ChangeMoles(0, 23.5f);
            buffer.ChangeMoles(1, 15f);
            
            Assert.IsTrue(buffer[0].GasData.Moles.Approx(23.5f), "23.5f");
            Assert.IsTrue(buffer[1].GasData.Moles.Approx(15f), "15f");
            
            buffer.ChangeMoles(1, 10f);

            Assert.IsTrue(buffer[1].GasData.Moles.Approx(25), "Add 15 to 10");

            Assert.IsTrue(buffer.Length == 2, "Only Two Gases");
            
            buffer.ChangeMoles(2, 0);
            
            Assert.IsTrue(buffer.Length == 2, "Tried to add 0 of new gas");
            
            buffer.ChangeMoles(1, -10);
            
            Assert.IsTrue(buffer[1].GasData.Moles.Approx(15f), "25 - 10");
        }
        
        [Test]
        public void GetGasTypeTest()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity);
            var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);
            
            buffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 0, MolarHeatCapacity = 20, Moles = 20}
            });
            
            buffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 1, MolarHeatCapacity = 20, Moles = 10}
            });
            
            buffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 2, MolarHeatCapacity = 20, Moles = 10}
            });

            buffer.GetGasType(0, out var gas);
            Assert.IsTrue(gas.HasValue && gas.Value.GasSO == 0 && gas.Value.Moles.Approx(20), "Gas 0");
            
            buffer.GetGasType(1, out var gas1);
            Assert.IsTrue(gas1.HasValue && gas1.Value.GasSO == 1 && gas1.Value.Moles.Approx(10), "Gas 1");
            
            buffer.GetGasType(2, out var gas2);
            Assert.IsTrue(gas2.HasValue && gas2.Value.GasSO == 2 && gas2.Value.Moles.Approx(10), "Gas 2");
            
            buffer.GetGasType(3, out var gas3);
            Assert.IsTrue(gas3.HasValue == false, "Gas null");
            
            Assert.IsTrue(buffer.Length == 3, "Three gases");
        }
        
        [Test]
        public void HasGasTypeTest()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity);
            var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);
            
            buffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 0, MolarHeatCapacity = 20, Moles = 20}
            });
            
            buffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 1, MolarHeatCapacity = 20, Moles = 10}
            });
            
            buffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                    {GasSO = 2, MolarHeatCapacity = 20, Moles = 10}
            });

            Assert.IsTrue(buffer.HasGasType(0), "Gas 0");
            
            Assert.IsTrue(buffer.HasGasType(1), "Gas 1");
            
            Assert.IsTrue(buffer.HasGasType(2), "Gas 2");
            
            Assert.IsTrue(buffer.HasGasType(3) == false, "Gas null");
            
            Assert.IsTrue(buffer.Length == 3, "Three gases");
        }
    }
}
