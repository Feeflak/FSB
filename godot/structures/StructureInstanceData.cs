using System.Collections.Generic;
using Godot;
public class StructureInstanceData(Vector2 base_world_pos, float scale, float rotation_y, float base_height, StructureType structure_type)
{
        public Vector2 base_world_pos = base_world_pos;
        public float base_height = base_height;
        public float scale = scale;
        public float rotation_y = rotation_y;
        public StructureType structure_type = structure_type;

        public void Instantiate(Node3D parent)
        {
                var node = (Node3D)structure_type.model.Instantiate();
                parent.AddChild(node);
                node.Scale = Vector3.One * scale;
                node.RotationDegrees = new Vector3(0, rotation_y, 0);
                node.Position = new(base_world_pos.X, base_height, base_world_pos.Y);
        }

        public HashSet<Vector2I> MeshChunksThisStructureSitsOnWorldPos(int mesh_chunk_size)
        {
                HashSet<Vector2I> chunks = [];
                foreach (var shape in structure_type.shapes)
                {
                        foreach (var point in ((IStructureShape)shape).GetSampleWorldPosPointsInsideTheShape(this))
                        {

                                Vector2I chunk = new(Mathf.RoundToInt(point.X / mesh_chunk_size) + 1, Mathf.FloorToInt(point.Y / mesh_chunk_size) + 1);
                                Vector2I chunk_world_pos = chunk * mesh_chunk_size;
                                chunks.Add(chunk_world_pos);
                        }
                }
                return chunks;
        }
        public bool IsObjectColliding(Vector2I world_pos)
        {
                foreach (var shape in structure_type.shapes)
                {
                        if (((IStructureShape)shape).IsPointWithinTheShape(world_pos, this))
                                return true;
                }
                return false;
        }
        public List<Vector2> GetSampleWorldPosPointsInsideThisStructure()
        {
                List<Vector2> output = [];
                foreach (var shape in structure_type.shapes)
                {
                        output.AddRange(((IStructureShape)shape).GetSampleWorldPosPointsInsideTheShape(this));
                }

                return output;
        }

        public bool IsValid(GroundMeshGen mesh_gen)
        {
                var min_height = float.MaxValue;
                var max_height = float.MinValue;
                foreach (var shape in structure_type.shapes)
                {
                        var i_structure_shape = (IStructureShape)shape;
                        var test_points = i_structure_shape.GetSampleWorldPosPointsInsideTheShape(this);


                        foreach (var point in test_points)
                        {
                                var height = mesh_gen.CalculateHeight(point, out _);
                                min_height = Mathf.Min(height, min_height);
                                max_height = Mathf.Max(height, max_height);
                        }
                        if (max_height - min_height > structure_type.maximal_height_delta_inside_the_shapes)
                                return false;
                }

                return true;
        }

}
