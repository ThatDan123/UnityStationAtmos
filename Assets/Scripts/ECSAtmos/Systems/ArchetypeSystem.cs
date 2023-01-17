using ECSAtmos.Components;
using ECSAtmos.DataTypes;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECSAtmos.Systems
{
    [UpdateInGroup(typeof(AtmosInitSystemGroup), OrderFirst = true)]
    public partial class ArchetypeSystem : SystemBase
    {
	    private EntityArchetype AtmosEntityArchetype { get; set; }

        private EntityArchetype AtmosStorageEntityArchetype { get; set; }

        private EntityArchetype AtmosPipeEntityArchetype { get; set; }

        public NativeParallelHashMap<byte, BlobAssetReference<GasInfo>> GasReferences;

        protected override void OnCreate()
        {
	        GasReferences = new NativeParallelHashMap<byte, BlobAssetReference<GasInfo>>(30, Allocator.Persistent);

	        AtmosEntityArchetype = EntityManager.CreateArchetype(
	            typeof(AtmosUpdateDataComponent),
	            typeof(AtmosTileOffsetShared),

                //Data node stuff
                typeof(MetaDataTileComponent),
                typeof(NeighbourBuffer),

                //Gas Stuff
                typeof(GasMixComponent),
                typeof(GasDataBuffer),

                //Conductivity
                typeof(ConductivityComponent),

                //Tag for tile atmos stuff (to exclude storage and pipes)
                typeof(TileAtmosTag),

	            //Start deactivated
	            typeof(DeactivatedTag));

            AtmosStorageEntityArchetype = EntityManager.CreateArchetype(
	            //Not strictly needed for the systems but is assumed to be on all atmos entities for GasVessel to work
	            typeof(AtmosUpdateDataComponent),

	            //Gas Stuff
	            typeof(GasMixComponent),
	            typeof(GasDataBuffer),

	            //Tag for storage atmos stuff (to exclude tiles and pipes)
	            typeof(StorageTag),

	            //Start deactivated
	            typeof(DeactivatedTag));

            AtmosPipeEntityArchetype = EntityManager.CreateArchetype(
	            typeof(AtmosUpdateDataComponent),
	            typeof(AtmosTileOffsetShared),

	            //Data node stuff
	            typeof(NeighbourBuffer),

	            //Gas Stuff
	            typeof(GasMixComponent),
	            typeof(GasDataBuffer),

	            //Tag for pipe atmos stuff (to exclude storage and tiles)
	            typeof(PipeAtmosTag),

	            //Start deactivated
	            typeof(DeactivatedTag));
        }

        protected override void OnUpdate() { }

        protected override void OnDestroy()
        {
	        GasReferences.Dispose();
        }

        public Entity CreateStorageEntity()
        {
	        var storageEntity = EntityManager.CreateEntity(AtmosStorageEntityArchetype);

	        EntityManager.SetName(storageEntity, $"Gas Storage Entity {storageEntity.Index},{storageEntity.Version}");

	        var buffer = EntityManager.GetBuffer<GasDataBuffer>(storageEntity, true);

	        EntityManager.SetComponentData(storageEntity, new GasMixComponent(in buffer));

	        return storageEntity;
        }

        public Entity CreateAtmosTileEntity(int x, int y, int matrixId)
        {
	        var atmosTileEntity = EntityManager.CreateEntity(AtmosEntityArchetype);

	        EntityManager.SetName(atmosTileEntity, $"Atmos Tile Entity {atmosTileEntity.Index},{atmosTileEntity.Version}");

	        EntityManager.SetComponentData(atmosTileEntity, new AtmosUpdateDataComponent
	        {
		        XUpdateID = (byte)(math.abs(x) % 3),
		        YUpdateID = (byte)(math.abs(y) % 3)
	        });

	        EntityManager.SetComponentData(atmosTileEntity, new MetaDataTileComponent
	        {
		        TileLocalPos = new int3(x, y, 0),
		        MatrixId = matrixId
	        });

	        return atmosTileEntity;
        }

        public Entity CreatePipeEntity(int x, int y)
        {
	        //Create pipe entity
	        var pipeEntity = EntityManager.CreateEntity(AtmosPipeEntityArchetype);

	        EntityManager.SetComponentData(pipeEntity, new AtmosUpdateDataComponent
	        {
		        XUpdateID = (byte)(math.abs(x) % 3),
		        YUpdateID = (byte)(math.abs(y) % 3)
	        });

	        var buffer = EntityManager.GetBuffer<GasDataBuffer>(pipeEntity, true);

	        EntityManager.SetComponentData(pipeEntity, new GasMixComponent(in buffer));

	        EntityManager.SetName(pipeEntity, $"Pipe Entity {pipeEntity.Index},{pipeEntity.Version}");

	        return pipeEntity;
        }
    }
}
