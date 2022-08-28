using Unity.Entities;

namespace ECSAtmos.Components
{
	public struct AtmosUpdateDataComponent : IComponentData
	{
		//Used to make sure theres no multiple access to the same tiles
		public byte XUpdateID;
		public byte YUpdateID;

		//Debug stuff
		public bool Updated;
		public bool TriedToUpdate;
	}
}