using Godot;
using System.Collections.Generic;
public static class TreeObjectsGenerator
{
        const int grid_padding = 2;

        private class ObjectsSpacingGrid(int grid_width)
        {
                // 1'st dimension -> x + y * grid_width 
                // 2'nd dimension -> all objects in this grid cell
                public ObjectSpacing[] grid = new ObjectSpacing[grid_width * grid_width];
                private readonly int grid_width = grid_width;
                public ObjectSpacing? this[Vector2I pos]
                {
                        get
                        {
                                if (pos.X < -1 || pos.X >= grid_width - 1 || pos.Y < -1 || pos.Y >= grid_width - 1)
                                        return null;

                                int index = pos.X + grid_padding / 2 + (pos.Y + grid_padding / 2) * grid_width;
                                return grid[index];
                        }
                        set
                        {

                                if (pos.X < -1 || pos.X >= grid_width - 1 || pos.Y < -1 || pos.Y >= grid_width - 1)
                                        return;

                                int index = pos.X + grid_padding / 2 + (pos.Y + grid_padding / 2) * grid_width;
                                grid[index] = value;
                        }
                }
        }
        public class ObjectSpacing(Vector2 pos, float min_distance_sqrt)
        {
                public Vector2 pos = pos;
                public float min_distance_sqrt = min_distance_sqrt;
        }
        private static float GridCellWidth(float minimal_object_spacing_sqrt)
        {
                var dist_max = minimal_object_spacing_sqrt;
                return dist_max / Mathf.Sqrt2;
        }
        public static void GenerateObjectsForMeshChunk(float base_object_spawn_chance, float minimal_object_spacing_sqrt, int mesh_chunk_size, GroundMeshGen ground_mesh_gen,
                BiomeGenerator.TextureData biome_data, Vector2 base_world_position, StructureGen.StructureGrid structure_grid,
                ref Dictionary<TerrainObject, List<ObjectInstantiationData>> object_instances_dictionary)
        {

                float grid_cell_width = GridCellWidth(minimal_object_spacing_sqrt);
                var grid_cells_count_per_dimension = Mathf.CeilToInt(mesh_chunk_size / grid_cell_width) + grid_padding;
                var grid = new ObjectsSpacingGrid(grid_cells_count_per_dimension);
                for (int x = -1; x < grid_cells_count_per_dimension - 1; x++)
                {
                        for (int y = -1; y < grid_cells_count_per_dimension - 1; y++)
                        {
                                if (GD.Randf() > base_object_spawn_chance)
                                        continue;
                                var base_cell_world_pos = base_world_position + new Vector2(x, y) * grid_cell_width;
                                bool is_margin = x == -1 || y == -1 || x == grid_cells_count_per_dimension - 2 || y == grid_cells_count_per_dimension - 2;

                                GenerateObjectForGridCell(minimal_object_spacing_sqrt, base_cell_world_pos, new(x, y), grid_cell_width, is_margin,
                                        ground_mesh_gen, biome_data, structure_grid, ref grid, ref object_instances_dictionary);
                        }
                }
        }

        private static void GenerateObjectForGridCell(float minimal_object_spacing_sqrt, Vector2 base_cell_world_pos, Vector2I grid_pos, float grid_cell_width, bool is_margin,
             GroundMeshGen ground_mesh_gen, BiomeGenerator.TextureData biome_data, StructureGen.StructureGrid structure_grid,
             ref ObjectsSpacingGrid grid, ref Dictionary<TerrainObject, List<ObjectInstantiationData>> instances_data_for_object_type)
        {

                Vector2 uv = new(GD.Randf(), GD.Randf());
                var world_pos_2d = uv * grid_cell_width + base_cell_world_pos;
                if (!IsPosValid(world_pos_2d, minimal_object_spacing_sqrt, grid_pos, grid) || !structure_grid.IsObjectValid(world_pos_2d))
                {
                        return;
                }

                var height = ground_mesh_gen.CalculateHeight(world_pos_2d, out var terrain_aspects);
                var biomes_influence = biome_data.GetBiomeInfluenceForUV(uv);
                Vector3 world_pos_3d = new(world_pos_2d.X, height, world_pos_2d.Y);

                foreach (var biome_influence in biomes_influence)
                {
                        var influence_cubed = biome_influence.influence * biome_influence.influence * biome_influence.influence;
                        if (GD.Randf() > influence_cubed)
                                continue;
                        var object_inst_data = biome_influence.biome.objects_data.GetObjectOfType(BiomeObjectsGenData.GetterType.tree, world_pos_3d, terrain_aspects);

                        if (object_inst_data == null)
                                continue;

                        if (!is_margin)
                        {
                                if (instances_data_for_object_type.TryGetValue(object_inst_data.Value.obj, out var object_type_array))
                                {
                                        object_type_array.Add(object_inst_data.Value);
                                }
                                else
                                {
                                        instances_data_for_object_type.Add(object_inst_data.Value.obj, [object_inst_data.Value]);
                                }
                        }

                        grid[grid_pos] = new(world_pos_2d, minimal_object_spacing_sqrt);
                        break;
                }


        }
        private static bool IsPosValid(Vector2 pos, float main_min_distance_sqrt, Vector2I grid_pos, ObjectsSpacingGrid grid)
        {
                for (int x = -1; x <= 1; x++)
                {
                        for (int y = -1; y <= 1; y++)
                        {
                                var obj = grid[grid_pos + new Vector2I(x, y)];
                                if (obj == null)
                                        continue;
                                if (!IsFarFarEnough(pos, main_min_distance_sqrt, obj))
                                        return false;

                        }
                }
                return true;
        }
        private static bool IsFarFarEnough(Vector2 pos, float main_min_distance_sqrt, ObjectSpacing obj)
        {
                var spacing = Mathf.Max(main_min_distance_sqrt, obj.min_distance_sqrt);
                if (pos.DistanceSquaredTo(obj.pos) < spacing * spacing)
                        return false;
                return true;
        }

}

