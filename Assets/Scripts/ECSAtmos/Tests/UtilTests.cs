using ECSAtmos.Components;
using ECSAtmos.DataTypes;
using ECSAtmos.Systems;
using NUnit.Framework;
using Systems.ECSAtmos;
using Systems.ECSAtmos.Other;
using Unity.Entities;
using Unity.Entities.Tests;
using UnityEngine;

namespace ECSAtmos.Tests
{
    [TestFixture]
    public class UtilTests : GasTestBase
    {

	    [Test]
        public void DivideAllTest()
        {
	        var entity = m_Manager.CreateEntity();
	        m_Manager.AddBuffer<GasDataBuffer>(entity);
	        var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

            buffer.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 20, GasInfoReference = gasInfoR1}
            });

            buffer.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 40, GasInfoReference = gasInfoR2}
            });

            buffer.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = -10, GasInfoReference = gasInfoR3}
            });

            var divider = 4;

            buffer.DivideAllGases(divider);

            Assert.IsTrue(buffer.GetMoles(gasInfo1).Approx(5), "20 / 4");

            Assert.IsTrue(buffer.GetMoles(gasInfo2).Approx(10), "40 / 4");

            Assert.IsTrue(buffer.Length == 2, "-10 / 4 (Index will be removed)");
        }

        [Test]
        public void AddToTest()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity);
            var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

            buffer.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 20, GasInfoReference = gasInfoR1}
            });

            buffer.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 45, GasInfoReference = gasInfoR2}
            });

            var entity2 = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity2);
            var buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);

            buffer2.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 20, GasInfoReference = gasInfoR1}
            });

            buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);
            buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);

            buffer.AddTo(ref buffer2);

            //20 + 20
            CheckAmount(in buffer2, gasInfo1, 40);

            //45 + 0
            CheckAmount(in buffer2, gasInfo2, 45);

            //Now have two gases
            CheckLength(in buffer2, 2);

            //Buffer should still have values
            CheckAmount(in buffer, gasInfo1, 20);

            //Buffer should still have values
            CheckAmount(in buffer, gasInfo2, 45);

            //Only two gases in them
            CheckLength(in buffer, 2);
        }

        [Test]
        public void CopyFromTest()
        {
	        var entity = m_Manager.CreateEntity();
	        m_Manager.AddBuffer<GasDataBuffer>(entity);
	        var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

	        buffer.Add(new GasDataBuffer
	        {
		        GasData = new GasData
			        {Moles = 20, GasInfoReference = gasInfoR1}
	        });

	        buffer.Add(new GasDataBuffer
	        {
		        GasData = new GasData
			        {Moles = 45, GasInfoReference = gasInfoR2}
	        });

	        var entity2 = m_Manager.CreateEntity();
	        m_Manager.AddBuffer<GasDataBuffer>(entity2);
	        var buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);

	        buffer2.Add(new GasDataBuffer
	        {
		        GasData = new GasData
			        {Moles = 20, GasInfoReference = gasInfoR1}
	        });

	        buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);
	        buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);

	        //Direction buffer copy
	        buffer.CopyFrom(buffer2);

	        //Buffer2 still have old values
	        CheckAmount(in buffer2, gasInfo1, 20);

	        //Still have only one gas
	        CheckLength(in buffer2, 1);

	        //Set from buffer2
	        CheckAmount(in buffer, gasInfo1, 20);

	        //Only two gases in them
	        CheckLength(in buffer, 1);
        }

        [Test]
        public void TransferTest()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity);
            var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);
            m_Manager.AddComponentData(entity, new GasMixComponent(in buffer, volume: 2.5f, temperature: 273.15f, pressure: 273.15f));
            buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

            buffer.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 20, GasInfoReference = gasInfoR1}
            });

            buffer.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 40, GasInfoReference = gasInfoR2}
            });

            var entity2 = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity2);
            var buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);
            m_Manager.AddComponentData(entity2, new GasMixComponent(in buffer2, volume: 2.5f, temperature: 273.15f, pressure: 273.15f));
            buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);

            buffer2.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 20, GasInfoReference = gasInfoR1}
            });

            var gasMix2 = m_Manager.GetComponentData<GasMixComponent>(entity2);
            var gasMix = m_Manager.GetComponentData<GasMixComponent>(entity);
            buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);
            buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

            AtmosUtils.TransferAllGas(ref buffer, ref gasMix, ref buffer2, ref gasMix2);

            CheckAmount(in buffer2, gasInfo1, 40);

            CheckAmount(in buffer2, gasInfo2, 40);

            //Does not clear from old mix so buffer will still be 2
            CheckLength(buffer, 2);

            //Should now be 2 in buffer2
            CheckLength(buffer2, 2);
        }

        [Test]
        public void SetMolesTest()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity);
            var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

            buffer.SetMoles(gasInfoR1, gasInfo1, 23.5f);
            buffer.SetMoles(gasInfoR2, gasInfo2, 15f);

            CheckAmount(in buffer, gasInfo1, 23.5f);
            CheckAmount(in buffer, gasInfo2, 15f);

            buffer.SetMoles(gasInfoR2, gasInfo2, 10f);

            //Set from 15 to 10
            CheckAmount(in buffer, gasInfo2, 10);

            //Only Two Gases
            CheckLength(in buffer, 2);

            buffer.SetMoles(gasInfoR1, gasInfo1, 0);

            //Set gas to 0 moles
            CheckLength(in buffer, 1);

            buffer.SetMoles(gasInfoR2, gasInfo2, -10);

			//Set gas to negative moles
            CheckLength(in buffer, 0);
        }

        [Test]
        public void ChangeMolesTest()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity);
            var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

            buffer.ChangeMoles(gasInfoR1, gasInfo1, 23.5f);
            buffer.ChangeMoles(gasInfoR2, gasInfo2, 15f);

            CheckAmount(in buffer, gasInfo1, 23.5f);
            CheckAmount(in buffer, gasInfo2, 15);

            buffer.ChangeMoles(gasInfoR2, gasInfo2, 10f);

            //Add 15 to 10
            CheckAmount(in buffer, gasInfo2, 25);

            //Should be length 2
            CheckLength(in buffer, 2);

            buffer.ChangeMoles(gasInfoR3, gasInfo3, 0);

            //Tried to add 0 moles of new gas, so should still be length 2
            CheckLength(in buffer, 2);

            buffer.ChangeMoles(gasInfoR2, gasInfo2, -10);

            //25 - 10
            CheckAmount(in buffer, gasInfo2, 15);
        }

        [Test]
        public void MultiplyMolesTest()
        {
	        var entity = m_Manager.CreateEntity();
	        m_Manager.AddBuffer<GasDataBuffer>(entity);
	        var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

	        buffer.ChangeMoles(gasInfoR1, gasInfo1, 23.5f);
	        buffer.ChangeMoles(gasInfoR2, gasInfo2, 15f);

	        CheckAmount(in buffer, gasInfo1, 23.5f);
	        CheckAmount(in buffer, gasInfo2, 15);

	        buffer.MultiplyMoles(gasInfoR2, gasInfo2, 10f);

	        //Add 15 * 10
	        CheckAmount(in buffer, gasInfo2, 150);

	        //Should be length 2
	        CheckLength(in buffer, 2);

	        buffer.MultiplyMoles(gasInfoR3, gasInfo3, 0);

	        //Tried to multiply 0 moles of new gas, so should still be length 2
	        CheckLength(in buffer, 2);

	        buffer.MultiplyMoles(gasInfoR1, gasInfo1, 2);

	        //23.5 * 2
	        CheckAmount(in buffer, gasInfo1, 47);
        }

        [Test]
        public void DivideMolesTest()
        {
	        var entity = m_Manager.CreateEntity();
	        m_Manager.AddBuffer<GasDataBuffer>(entity);
	        var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

	        buffer.ChangeMoles(gasInfoR1, gasInfo1, 23.5f);
	        buffer.ChangeMoles(gasInfoR2, gasInfo2, 15f);

	        CheckAmount(in buffer, gasInfo1, 23.5f);
	        CheckAmount(in buffer, gasInfo2, 15);

	        buffer.DivideMoles(gasInfoR2, gasInfo2, 10f);

	        //Add 15 / 10
	        CheckAmount(in buffer, gasInfo2, 1.5f);

	        //Should be length 2
	        CheckLength(in buffer, 2);

	        buffer.DivideMoles(gasInfoR3, gasInfo3, 0);

	        //Tried to multiply 0 moles of new gas, so should still be length 2
	        CheckLength(in buffer, 2);

	        buffer.DivideMoles(gasInfoR1, gasInfo1, 2);

	        //23.5 / 2
	        CheckAmount(in buffer, gasInfo1, 11.75f);
        }

        [Test]
        public void GetGasTypeTest()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity);
            var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

            buffer.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 20, GasInfoReference = gasInfoR1}
            });

            buffer.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 10, GasInfoReference = gasInfoR2}
            });

            buffer.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 10, GasInfoReference = gasInfoR3}
            });

            buffer.GetGasType(gasInfo1, out var gas);
            Assert.IsTrue(gas.HasValue && gas.Value.GasSO == gasInfo1 && gas.Value.Moles.Approx(20), "Gas 1");

            buffer.GetGasType(gasInfo2, out var gas1);
            Assert.IsTrue(gas1.HasValue && gas1.Value.GasSO == gasInfo2 && gas1.Value.Moles.Approx(10), "Gas 2");

            buffer.GetGasType(gasInfo3, out var gas2);
            Assert.IsTrue(gas2.HasValue && gas2.Value.GasSO == gasInfo3 && gas2.Value.Moles.Approx(10), "Gas 3");

            buffer.GetGasType(gasInfo4, out var gas3);
            Assert.IsTrue(gas3.HasValue == false, "Gas null");

            CheckLength(in buffer, 3);
        }

        [Test]
        public void HasGasTypeTest()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<GasDataBuffer>(entity);
            var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

            buffer.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 20, GasInfoReference = gasInfoR1}
            });

            buffer.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 10, GasInfoReference = gasInfoR2}
            });

            buffer.Add(new GasDataBuffer
            {
                GasData = new GasData
                    {Moles = 10, GasInfoReference = gasInfoR3}
            });

            Assert.IsTrue(buffer.HasGasType(gasInfo1), "Gas 1");

            Assert.IsTrue(buffer.HasGasType(gasInfo2), "Gas 2");

            Assert.IsTrue(buffer.HasGasType(gasInfo3), "Gas 3");

            Assert.IsTrue(buffer.HasGasType(gasInfo4) == false, "Gas null");

            CheckLength(in buffer, 3);
        }

        [Test]
        public void TransferGasesTest()
        {
	        var entity = m_Manager.CreateEntity();
	        m_Manager.AddBuffer<GasDataBuffer>(entity);
	        var buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);
	        m_Manager.AddComponentData(entity, new GasMixComponent(in buffer, volume: 2.5f, temperature: 273.15f, pressure: 273.15f));
	        buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

	        buffer.Add(new GasDataBuffer
	        {
		        GasData = new GasData
			        {Moles = 40, GasInfoReference = gasInfoR1}
	        });

	        buffer.Add(new GasDataBuffer
	        {
		        GasData = new GasData
			        {Moles = 40, GasInfoReference = gasInfoR2}
	        });

	        var entity2 = m_Manager.CreateEntity();
	        m_Manager.AddBuffer<GasDataBuffer>(entity2);
	        var buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);
	        m_Manager.AddComponentData(entity2, new GasMixComponent(in buffer2, volume: 2.5f, temperature: 273.15f, pressure: 273.15f));
	        buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);

	        buffer2.Add(new GasDataBuffer
	        {
		        GasData = new GasData
			        {Moles = 20, GasInfoReference = gasInfoR1}
	        });

	        var gasMix2 = m_Manager.GetComponentData<GasMixComponent>(entity2);
	        var gasMix = m_Manager.GetComponentData<GasMixComponent>(entity);
	        buffer2 = m_Manager.GetBuffer<GasDataBuffer>(entity2);
	        buffer = m_Manager.GetBuffer<GasDataBuffer>(entity);

	        gasMix.Recalculate(buffer);
	        gasMix2.Recalculate(buffer2);

	        //Transfer 10 moles, therefore 5 moles of each from buffer into buffer2
	        AtmosUtils.TransferGases(in entity, in entity2, ref buffer, ref gasMix,
		        ref buffer2, ref gasMix2, 10);

	        CheckAmount(in buffer2, gasInfo1, 25);

	        CheckAmount(in buffer2, gasInfo2, 5);

	        CheckLength(in buffer2, 2);

	        CheckAmount(in buffer, gasInfo1, 35);

	        CheckAmount(in buffer, gasInfo2, 35);

	        CheckLength(in buffer, 2);
        }
    }
}
