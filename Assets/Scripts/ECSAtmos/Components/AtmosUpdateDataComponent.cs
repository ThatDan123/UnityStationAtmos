using ECSAtmos.Util;
using Unity.Entities;

namespace ECSAtmos.Components
{
	public struct AtmosTileOffsetShared : ISharedComponentData
	{
		//Used to make sure theres no multiple access to the same tiles
		public byte XUpdateID;
		public byte YUpdateID;

		public AtmosTileOffsetShared(byte xUpdateID, byte yUpdateID)
		{
			XUpdateID = xUpdateID;
			YUpdateID = yUpdateID;
		}
		
		public AtmosTileOffsetShared(OffsetLogic offsetLogic)
		{
			XUpdateID = offsetLogic.XUpdateID;
			YUpdateID = offsetLogic.YUpdateID;
		}
	}
	
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