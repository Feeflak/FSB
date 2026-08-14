using System.Collections.Generic;
using Godot;

public interface IStructureShape
{
        public List<Vector2> GetSampleWorldPosPointsInsideTheShape(StructureInstanceData instance_data);
        public bool IsPointWithinTheShape(Vector2 point, StructureInstanceData instance_data);
}
