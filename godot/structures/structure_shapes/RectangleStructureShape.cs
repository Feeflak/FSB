using System.Collections.Generic;
using Godot;
[GlobalClass, Tool]
public partial class RectangleStructureShape : Resource, IStructureShape
{
        [Export] public Vector2 base_size;
        [Export] public float base_rotation_y;
        [Export] public Vector2 base_offset;
        [Export] public float sample_points_spacing;

        public List<Vector2> GetSampleWorldPosPointsInsideTheShape(StructureInstanceData instance_data)
        {
                var points = new List<Vector2>();

                // Get actual size and rotation from instance data
                Vector2 size = base_size * instance_data.scale;
                float rotation_rad = Mathf.DegToRad(base_rotation_y + instance_data.rotation_y);
                Vector2 offset = base_offset + instance_data.base_world_pos;

                // Calculate how many samples we need in each dimension
                int countX = Mathf.Max(1, Mathf.CeilToInt(size.X / sample_points_spacing));
                int countY = Mathf.Max(1, Mathf.CeilToInt(size.Y / sample_points_spacing));

                // Adjust spacing to fit evenly
                float actualSpacingX = size.X / countX;
                float actualSpacingY = size.Y / countY;

                // Generate points in local space
                for (int x = 0; x <= countX; x++)
                {
                        for (int y = 0; y <= countY; y++)
                        {
                                // Local point centered at origin
                                Vector2 localPoint = new(
                                    x * actualSpacingX - size.X / 2f,
                                    y * actualSpacingY - size.Y / 2f
                                );

                                // Apply rotation
                                Vector2 rotatedPoint = localPoint.Rotated(rotation_rad);

                                // Apply offset
                                points.Add(rotatedPoint + offset);
                        }
                }

                return points;
        }

        public bool IsPointWithinTheShape(Vector2 world_pos, StructureInstanceData instance_data)
        {
                // Get actual size and rotation from instance data
                Vector2 size = base_size * instance_data.scale;
                float rotation_rad = Mathf.DegToRad(base_rotation_y + instance_data.rotation_y);
                Vector2 offset = base_offset + instance_data.base_world_pos;

                // Transform point to local space (inverse of the rectangle's transform)
                Vector2 localPoint = (world_pos - offset).Rotated(-rotation_rad);

                // Check if point is within the axis-aligned bounds
                return Mathf.Abs(localPoint.X) <= size.X / 2f &&
                       Mathf.Abs(localPoint.Y) <= size.Y / 2f;
        }
}
