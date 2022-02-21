using System;
using ECSAtmos.Systems;
using Unity.Entities;

namespace ECSAtmos.Components
{
    public struct MetaDataTileComponent : IComponentData
    {
        /// <summary>
        /// If this node is in a closed room, it's assigned to it by the room's number
        /// Set to -1 on spawn
        /// </summary>
        public int RoomNumber;
        
        /// <summary>
        /// Type of this node.
        /// </summary>
        public NodeType NodeType;
        
        /// <summary>
        /// Type of this node.
        /// </summary>
        public BlockType BlockType;
        
        /// <summary>
        /// Is this tile occupied by something impassable (airtight!)
        /// </summary>
        public bool IsOccupied => NodeType == NodeType.Occupied;
        
        /// <summary>
        /// Is this tile in space
        /// </summary>
        public bool IsSpace => NodeType == NodeType.Space;

        /// <summary>
        /// Is this tile in a room
        /// </summary>
        public bool IsRoom => NodeType == NodeType.Room;
        
        public bool Sleeping;
        
        //Debug stuff
        public bool Updated;
        public bool TriedToUpdate;
    }
    
    /// <summary>
    /// Used to store the neighboring atmos entity tiles
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct MetaDataTileBuffer : IBufferElementData
    {
        public TileNeighbourSystem.Data Data;
        public Entity DataTile => Data.Entity;
    }
    
    public enum NodeType : byte
    {
        None,
        /// <summary>
        /// Node out in space
        /// </summary>
        Space,
        /// <summary>
        /// Node in a room on a tile that is not occupied.
        /// </summary>
        Room,
        /// <summary>
        /// Node occupied by something such that it is not passable or atmos passable
        /// </summary>
        Occupied
    }
    
    /// <summary>
    /// Directionally atmos impassable
    /// </summary>
    [Flags]
    public enum BlockType : byte
    {
        None = 0,
        Up = 1 << 0,
        Down = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
        All = Up | Down | Left| Right
    }
}
