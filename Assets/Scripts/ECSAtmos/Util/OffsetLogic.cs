using Unity.Mathematics;

namespace ECSAtmos.Util
{
    public struct OffsetLogic
    {
        public int3 Offset;
    }

    public static class OffsetUtil
    {
        public static void DoStep(this ref OffsetLogic offsetLogic)
        {
            offsetLogic.Offset += new int3(1, 0, 0);
            if (offsetLogic.Offset.x > 3)
            {
                offsetLogic.Offset = new int3(0, offsetLogic.Offset.y + 1, 0);
            }
	         
            if (offsetLogic.Offset.y > 3)
            {
                offsetLogic.Offset = new int3(0, 0, 0);
            }
        }
    }
}