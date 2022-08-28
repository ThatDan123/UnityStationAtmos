using Unity.Mathematics;

namespace ECSAtmos.Util
{
    public struct OffsetLogic
    {
        public byte XUpdateID;
        public byte YUpdateID;
    }

    public static class OffsetUtil
    {
        public static void DoStep(this ref OffsetLogic offsetLogic)
        {
            offsetLogic.XUpdateID += 1;
            
            if (offsetLogic.XUpdateID > 3)
            {
                offsetLogic.XUpdateID = 0;
                offsetLogic.YUpdateID += 1;
            }
	         
            if (offsetLogic.YUpdateID > 3)
            {
                offsetLogic.XUpdateID = 0;
                offsetLogic.YUpdateID = 0;
            }
        }
    }
}