using ECSAtmos.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ECSAtmos.Systems
{
    public class MatrixEntityPositionSystem : SystemBase
    {
        public static EntityArchetype matrixEntityArchetype;
        protected override void OnCreate()
        {
            matrixEntityArchetype = EntityManager.CreateArchetype(
                //Basic stuff
                typeof(Translation),
                typeof(LocalToWorld),
                typeof(Rotation),
                //Child is all the child entities (atmos tiles)
                typeof(Child),
                //Has a matrixId back to the connected matrix
                typeof(MatrixEntity));
        }

        protected override void OnUpdate()
        {
            Entities.ForEach((ref Translation translation, in MatrixEntity matrix) =>
            {
                var id = matrix.MatrixId;
                if(id == -1) return;

                //TODO get matrix from id
                //var matrixInfo = MatrixManager.ActiveMatrices[id];
                
                //var currentPos = matrixInfo.GetOffset();
                //translation.Value = currentPos;
                
                //TODO rotation
                
            }).Run();
        }
    }
}
