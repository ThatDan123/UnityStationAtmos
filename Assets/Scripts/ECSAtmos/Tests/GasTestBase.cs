using ECSAtmos.Components;
using ECSAtmos.DataTypes;
using ECSAtmos.Systems;
using NUnit.Framework;
using Systems.ECSAtmos;
using Systems.ECSAtmos.Other;
using Unity.Entities;
using Unity.Entities.Tests;

namespace ECSAtmos.Tests
{
	public class GasTestBase : ECSTestsFixture
	{
		protected BlobAssetReference<GasInfo> gasInfoR1;
		protected BlobAssetReference<GasInfo> gasInfoR2;
		protected BlobAssetReference<GasInfo> gasInfoR3;
		protected BlobAssetReference<GasInfo> gasInfoR4;

		protected GasInfo gasInfo1 => gasInfoR1.Value;
		protected GasInfo gasInfo2 => gasInfoR2.Value;
		protected GasInfo gasInfo3 => gasInfoR3.Value;
		protected GasInfo gasInfo4 => gasInfoR4.Value;

		protected ArchetypeSystem archetypeSystem;

		public override void Setup()
		{
			base.Setup();

			archetypeSystem = World.GetOrCreateSystem<ArchetypeSystem>();

			archetypeSystem.GasReferences.Clear();

			gasInfoR1 = AtmosUtils.CreateGasInfoBlob(1, new GasInfo
			{
				MolarHeatCapacity = 20
			});

			gasInfoR2 = AtmosUtils.CreateGasInfoBlob(2, new GasInfo
			{
				MolarHeatCapacity = 20
			});

			gasInfoR3 = AtmosUtils.CreateGasInfoBlob(3, new GasInfo
			{
				MolarHeatCapacity = 20
			});

			gasInfoR4 = AtmosUtils.CreateGasInfoBlob(4, new GasInfo
			{
				MolarHeatCapacity = 20
			});
		}

		public override void TearDown()
		{
			archetypeSystem.GasReferences.Clear();

			base.TearDown();
		}

		protected void CheckAmount(in DynamicBuffer<GasDataBuffer> buffer, byte gas, float expectedMoles)
		{
			var amount = buffer.GetMoles(gas);
			Assert.IsTrue(amount.Approx(expectedMoles), $"Expected moles: {expectedMoles}, got: {amount}");
		}

		protected void CheckLength(in DynamicBuffer<GasDataBuffer> buffer, int length)
		{
			Assert.IsTrue(buffer.Length == length, $"Expected length: {length}, got: {buffer.Length}");
		}

		protected void CheckAmount(GasVessel gasVessel, byte gas, float expectedMoles)
		{
			var amount = gasVessel.GetMoles(gas);
			Assert.IsTrue(amount.Approx(expectedMoles), $"Expected moles: {expectedMoles}, got: {amount}");
		}

		protected void CheckLength(GasVessel gasVessel, int length)
		{
			var bufferLength = gasVessel.GasCount();
			Assert.IsTrue(bufferLength == length, $"Expected length: {length}, got: {bufferLength}");
		}

		protected void CheckAmount(float actual, float expectedMoles)
		{
			Assert.IsTrue(actual.Approx(expectedMoles), $"Expected moles: {expectedMoles}, got: {actual}");
		}
	}
}