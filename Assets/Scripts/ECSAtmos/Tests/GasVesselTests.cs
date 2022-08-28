using ECSAtmos.Components;
using ECSAtmos.DataTypes;
using NUnit.Framework;
using Systems.Atmospherics;
using Systems.ECSAtmos;
using Systems.ECSAtmos.Other;
using Unity.Entities;

namespace ECSAtmos.Tests
{
	[TestFixture]
	public class GasVesselTests : GasTestBase
	{
		private GasVessel gasVessel1;
		private GasVessel gasVessel2;
		private GasVessel gasVessel3;

		private GasMixComponent baseStorage;

		public override void Setup()
		{
			base.Setup();

			gasVessel1 = new GasVessel();
			gasVessel1.CreateStorage();

			gasVessel2 = new GasVessel();
			gasVessel2.CreateStorage();

			gasVessel3 = new GasVessel();
			gasVessel3.CreateStorage();

			baseStorage = new GasMixComponent
			{
				Pressure = 0,
				Volume = 2.5f,
				Temperature = AtmosConstants.KOffsetC + 20
			};
		}

		public override void TearDown()
		{
			gasVessel1.CleanUp();
			gasVessel2.CleanUp();
			gasVessel3.CleanUp();

			gasVessel1 = null;
			gasVessel2 = null;
			gasVessel3 = null;

			base.TearDown();
		}

		private void Reset()
		{
			gasVessel1.Clear();
			gasVessel2.Clear();
			gasVessel3.Clear();
		}

		[Test]
		public void AddGasTest()
		{
			Reset();

			gasVessel1.AddGas(gasInfo1, 20);
			gasVessel1.AddGas(gasInfo2, 40);
			gasVessel1.AddGas(gasInfo3, -10);

			CheckAmount(gasVessel1, gasInfo1, 20);
			CheckAmount(gasVessel1, gasInfo2, 40);
			CheckAmount(gasVessel1, gasInfo3, 0);

			CheckLength(gasVessel1, 2);

			gasVessel1.AddGas(gasInfo1, 20);

			CheckAmount(gasVessel1, gasInfo1, 40);
			CheckLength(gasVessel1, 2);

			//Negative should not work
			gasVessel1.AddGas(gasInfo1, -5);

			CheckAmount(gasVessel1, gasInfo1, 40);
		}

		[Test]
		public void RemoveGasTest()
		{
			Reset();

			gasVessel1.AddGas(gasInfo1, 20);
			gasVessel1.AddGas(gasInfo2, 40);

			gasVessel1.RemoveGas(gasInfo1, 10);
			gasVessel1.RemoveGas(gasInfo2, 40);

			CheckAmount(gasVessel1, gasInfo1, 10);
			CheckAmount(gasVessel1, gasInfo2, 0);

			CheckLength(gasVessel1, 1);

			//Negative should not work
			gasVessel1.RemoveGas(gasInfo1, -5);

			CheckAmount(gasVessel1, gasInfo1, 10);
		}

		[Test]
		public void RemoveMolesTest()
		{
			Reset();

			gasVessel1.AddGas(gasInfo1, 20);
			gasVessel1.AddGas(gasInfo2, 40);

			//Remove 10 moles
			gasVessel1.RemoveMoles(10);

			CheckAmount(gasVessel1, gasInfo1, 10);
			CheckAmount(gasVessel1, gasInfo2, 40);

			CheckLength(gasVessel1, 2);

			//Remove 20 moles
			gasVessel1.RemoveMoles(20);

			CheckAmount(gasVessel1, gasInfo1, 0);
			CheckAmount(gasVessel1, gasInfo2, 30);

			CheckLength(gasVessel1, 1);

			//Remove all
			gasVessel1.RemoveMoles(gasVessel1.Moles);
			CheckLength(gasVessel1, 0);
		}

		[Test]
		public void DivideTest()
		{
			Reset();

			gasVessel1.AddGas(gasInfo1, 20);
			gasVessel1.AddGas(gasInfo2, 40);

			//Divide by 10
			gasVessel1.DivideGases(10);

			CheckAmount(gasVessel1, gasInfo1, 2);
			CheckAmount(gasVessel1, gasInfo2, 4);
			CheckLength(gasVessel1, 2);

			//Divide by 0, this should not be allowed
			gasVessel1.DivideGases(0);

			CheckAmount(gasVessel1, gasInfo1, 2);
			CheckAmount(gasVessel1, gasInfo2, 4);

			//Divide by 100000, should now be set to 0
			gasVessel1.DivideGases(100000);
			CheckLength(gasVessel1, 0);
		}

		[Test]
		public void MultiplyTest()
		{
			Reset();

			gasVessel1.AddGas(gasInfo1, 20);
			gasVessel1.AddGas(gasInfo2, 40);

			//Multiply by 10
			gasVessel1.MultiplyGases(10);

			CheckAmount(gasVessel1, gasInfo1, 200);
			CheckAmount(gasVessel1, gasInfo2, 400);
			CheckLength(gasVessel1, 2);

			//Multiply by 0, this is allowed
			gasVessel1.MultiplyGases(0);

			CheckAmount(gasVessel1, gasInfo1, 0);
			CheckAmount(gasVessel1, gasInfo2, 0);
			CheckLength(gasVessel1, 0);

			gasVessel1.AddGas(gasInfo1, 20);
			gasVessel1.AddGas(gasInfo2, 40);

			//Multiply by 0.000001f, should now be set to 0
			gasVessel1.MultiplyGases(0.000001f);
			CheckLength(gasVessel1, 0);
		}

		[Test]
		public void GetMolesTest()
		{
			Reset();

			gasVessel1.AddGas(gasInfo1, 20);
			gasVessel1.AddGas(gasInfo2, 40);

			CheckAmount(gasVessel1, gasInfo1, 20);
			CheckAmount(gasVessel1, gasInfo2, 40);
			CheckLength(gasVessel1, 2);
		}

		[Test]
		public void GasCountTest()
		{
			Reset();

			gasVessel1.AddGas(gasInfo1, 20);
			CheckLength(gasVessel1, 1);

			gasVessel1.AddGas(gasInfo2, 40);
			CheckLength(gasVessel1, 2);
		}

		[Test]
		public void PressureTest()
		{
			Reset();

			gasVessel1.AddGas(gasInfo1, 20);

			//Pressure check for 20 moles
			var pressure = gasVessel1.Pressure;
			var expected = GasMixUtil.CalcPressure(baseStorage.Volume, 20, baseStorage.Temperature);
			CheckAmount(pressure, expected);

			gasVessel1.AddGas(gasInfo1, 20);

			//Pressure check when adding 20 more moles
			pressure = gasVessel1.Pressure;
			expected = GasMixUtil.CalcPressure(baseStorage.Volume, 40, baseStorage.Temperature);
			CheckAmount(pressure, expected);

			//Increase temp by 10 kelvin
			gasVessel1.Temperature += 10;

			pressure = gasVessel1.Pressure;
			expected = GasMixUtil.CalcPressure(baseStorage.Volume, 40, baseStorage.Temperature + 10);
			CheckAmount(pressure, expected);
		}

		[Test]
		public void TemperatureTest()
		{
			Reset();

			gasVessel1.AddGas(gasInfo1, 20);

			//Temperature check for 20 moles
			var temperature = gasVessel1.Temperature;
			var expected = GasMixUtil.CalcTemperature(gasVessel1.Pressure, baseStorage.Volume, gasVessel1.Moles);
			CheckAmount(temperature, expected);

			//Increase temp by 10 kelvin
			gasVessel1.Temperature += 10;

			//Temperature check for 20 moles
			temperature = gasVessel1.Temperature;
			expected = GasMixUtil.CalcTemperature(gasVessel1.Pressure, baseStorage.Volume, gasVessel1.Moles);
			CheckAmount(temperature, expected);

			gasVessel1.AddGas(gasInfo1, 20);

			//Temperature check for 40 moles
			temperature = gasVessel1.Temperature;
			expected = GasMixUtil.CalcTemperature(gasVessel1.Pressure, baseStorage.Volume, gasVessel1.Moles);
			CheckAmount(temperature, expected);

			//Set temperature -10k not possible so set to AtmosDefines.SPACE_TEMPERATURE
			gasVessel1.Temperature = -10;
			CheckAmount(gasVessel1.Temperature, AtmosDefines.SPACE_TEMPERATURE);
		}

		[Test]
		public void TransferTest()
		{
			Reset();

			gasVessel1.AddGas(gasInfo1, 20);
			gasVessel1.AddGas(gasInfo2, 10);

			gasVessel2.AddGas(gasInfo1, 20);

			var ratio = 10 / gasVessel1.Moles;

			var gasTransfer1 = 20 * ratio;
			var gasTransfer2 = 10 * ratio;

			gasVessel1.TransferGases(gasVessel2, 10);

			CheckAmount(gasVessel2, gasInfo1, 20 + gasTransfer1);
			CheckAmount(gasVessel2, gasInfo2, gasTransfer2);

			CheckAmount(gasVessel1, gasInfo1, 20 - gasTransfer1);
			CheckAmount(gasVessel1, gasInfo2, 10 - gasTransfer2);
		}

		[Test]
		public void TransferSpecificTest()
		{
			Reset();

			gasVessel1.AddGas(gasInfo1, 20);
			gasVessel1.AddGas(gasInfo2, 10);

			gasVessel2.AddGas(gasInfo1, 20);

			gasVessel1.TransferSpecifiedTo(gasVessel2, gasInfo1, 10);

			CheckAmount(gasVessel2, gasInfo1, 30);
			CheckAmount(gasVessel2, gasInfo2, 0);

			CheckAmount(gasVessel1, gasInfo1, 10);
			CheckAmount(gasVessel1, gasInfo2, 10);
		}
	}
}