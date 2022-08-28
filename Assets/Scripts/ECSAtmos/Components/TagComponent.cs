using Unity.Entities;

namespace ECSAtmos.Components
{
	//Used to tag Storage Entities
	public struct StorageTag : IComponentData { }

	//Used to tag tiles atmos entities
	public struct TileAtmosTag : IComponentData { }

	//Used to tag pipe atmos entities
	public struct PipeAtmosTag : IComponentData { }

	//Used to tag deactivated entities
	public struct DeactivatedTag : IComponentData { }
}