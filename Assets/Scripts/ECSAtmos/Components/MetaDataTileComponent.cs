using System;
using ECSAtmos.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace ECSAtmos.Components
{
    public struct MetaDataTileComponent : IComponentData
    {
	    public int3 TileLocalPos;

	    public int MatrixId;

        /// <summary>
        /// If this node is in a closed room, it's assigned to it by the room's number
        /// Set to 0 on spawn
        /// </summary>
        public int RoomNumber;

        /// <summary>
        /// Type of this node.
        /// </summary>
        public NodeType NodeType;

        /// <summary>
        /// Type of this node.
        /// </summary>
        public NodeOccupiedType OccupiedType;

        /// <summary>
        /// Does this tile contain a closed airlock/shutters? Prevents gas exchange to adjacent tiles
        /// (used for gas freezing)
        /// </summary>
        public bool IsIsolatedNode => OccupiedType == NodeOccupiedType.Full;

        /// <summary>
        /// Solid tiles such as walls
        /// </summary>
        public bool IsSolid => OccupiedType == NodeOccupiedType.Solid;

        /// <summary>
        /// Is this tile in space
        /// </summary>
        public bool IsSpace => NodeType == NodeType.Space;

        /// <summary>
        /// Is this tile in a room
        /// </summary>
        public bool IsRoom => NodeType == NodeType.Room;
    }

    /// <summary>
    /// Used to store the neighboring atmos entity tiles
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct NeighbourBuffer : IBufferElementData
    {
        public Entity NeighbourEntity;
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
        Room
    }

    /// <summary>
    /// Directionally atmos impassable
    /// </summary>
    [Flags]
    public enum NodeOccupiedType : byte
    {
	    None = 0,
        Right = 1 << 0,
        Up = 1 << 1,
        Left = 1 << 2,
        Down = 1 << 3,

        //Atmos blocked on all sides (is an isolated node e.g closed door), but atmos still runs on tile (reactions)
        Full = Up | Right | Down | Left,

        Solid = 1 << 4
    }
}
