using Unity.Entities;
using UnityEngine;

namespace ECSAtmos.Systems
{
	[ExecuteAlways]
	[UpdateInGroup(typeof (AtmosSystemGroup))]
	[UpdateAfter(typeof(DeactivateSystem))]
	public class AtmosEndEntityCommandBufferSystem : EntityCommandBufferSystem { }

	[ExecuteAlways]
	[UpdateInGroup(typeof (AtmosSystemGroup), OrderFirst = true)]
	public class AtmosBeginningEntityCommandBufferSystem : EntityCommandBufferSystem { }
}