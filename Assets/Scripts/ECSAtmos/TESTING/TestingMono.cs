using ECSAtmos.Components;
using ECSAtmos.DataTypes;
using ECSAtmos.Systems;
using Systems.ECSAtmos;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class TestingMono : MonoBehaviour
{
    public GasData gas;

    public Entity cube;

    public static Entity Cube;

    public static bool isPaused;

    private void Awake()
    {
        Cube = cube;
    }

    [ContextMenu("Start")]
    public void StartECS()
    {
        World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<ArchetypeSystem>();
    }

    [ContextMenu("Add Gas")]
    public void AddGas()
    {
        if(World.DefaultGameObjectInjectionWorld?.EntityManager == null) return;

        var entities = World.DefaultGameObjectInjectionWorld.EntityManager.GetAllEntities();

        foreach (var entity in entities)
        {
            if(entity.TryGetComponent<GasMixComponent>(out var gas) == false) continue;
            if(entity.TryGetComponent<WorldTransform>(out var world) == false) continue;
            var buffer = World.DefaultGameObjectInjectionWorld?.EntityManager.GetBuffer<GasDataBuffer>(entity);

            //All start with some gas
            var inter = buffer.Value.Reinterpret<float>();
            inter[0] += 10;
        }

        entities.Dispose();
    }

    [ContextMenu("Remove Gas")]
    public void RemoveGas()
    {
        if(World.DefaultGameObjectInjectionWorld?.EntityManager == null) return;

        var entities = World.DefaultGameObjectInjectionWorld.EntityManager.GetAllEntities(Allocator.Temp);

        foreach (var entity in entities)
        {
            if(entity.TryGetComponent<GasMixComponent>(out var gas) == false) continue;
            if(entity.TryGetComponent<WorldTransform>(out var world) == false) continue;
            var buffer = World.DefaultGameObjectInjectionWorld?.EntityManager.GetBuffer<GasDataBuffer>(entity);

            //All start with some gas
            buffer.Value.Clear();
        }

        entities.Dispose();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        isPaused = pauseStatus;
    }
}
