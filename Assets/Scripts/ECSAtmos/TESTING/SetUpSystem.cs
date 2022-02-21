using ECSAtmos.Components;
using ECSAtmos.DataTypes;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;


public class SetUpSystem : SystemBase
{
    // atmosTiles * atmosTiles = amount of tiles
    private const int atmosTiles = 100;
    
    protected override void OnCreate()
    {
        //TODO might want to split this up somehow?
        //Do we need LocalToWorld??
        var arch = EntityManager.CreateArchetype(
            //Basic stuff
            typeof(Translation), 
            typeof(LocalToWorld), 
            typeof(LocalToParent), 
            typeof(Rotation), 
            //Parent is the entity created to mirror each matrix pos
            typeof(Parent), 
            //Data node stuff
            typeof(MetaDataTileComponent), 
            typeof(MetaDataTileBuffer),
            //Gas Stuff
            typeof(GasMixComponent), 
            typeof(GasDataBuffer),
            //Conductivity
            typeof(ConductivityComponent));
        
        EntityManager.CreateEntity(arch, atmosTiles * atmosTiles);

        var countX = 0;
        var countY = 0;
        var random = new Random(1);

        Entities.ForEach((Entity entity, ref Translation translation,
            ref GasMixComponent gasMixComponent, 
            ref DynamicBuffer<GasDataBuffer> gasDataBuffer,
            ref ConductivityComponent conductivity) =>
        {
            if (countX > atmosTiles)
            {
                countY++;
                countX = 0;
            }

            //Should fill grid of 100 * 100
            var pos = new float3(countX, countY, 0);
            translation.Value = pos;
            
            countX++;

            gasDataBuffer.Add(new GasDataBuffer()
            {
                GasData = new GasData()
                {
                    GasSO = 0,
                    Moles = random.NextFloat(0, 30),
                    MolarHeatCapacity = 20
                }
            });

            gasMixComponent.Volume = 2.5f;
            gasMixComponent.Temperature =  273.15f;
            gasMixComponent.Pressure =  273.15f;
            
            gasMixComponent.ReCalculate(in gasDataBuffer);

            conductivity = new ConductivityComponent()
            {
                HeatCapacity = 200f,
                ThermalConductivity = 0.5f
            };

        }).Run();
    }

    protected override void OnUpdate() { }
}
