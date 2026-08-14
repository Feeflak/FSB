using System.Collections.Generic;
using Godot;
[Tool]
public partial class StructureGen : Node
{
        [Export] StructureType[] structure_pool;
        [Export] public int mesh_chunks_per_structure_grid_cell;

        public class StructureChunk(Dictionary<Vector2I, StructureInstanceData> structure_collision_dict_terrain_chunk_world_pos, Dictionary<Vector2I, StructureInstanceData> structure_instantiation_dict_terrain_chunk_world_pos)
        {
                public Dictionary<Vector2I, StructureInstanceData> structure_collision_dict_terrain_chunk_world_pos = structure_collision_dict_terrain_chunk_world_pos;
                public Dictionary<Vector2I, StructureInstanceData> structure_instantiation_dict_terrain_chunk_world_pos = structure_instantiation_dict_terrain_chunk_world_pos;
        }
        public class StructureGrid
        {
                const int grid_width = 3;
                readonly StructureChunk[] grid;
                private Vector2I current_player_grid_pos;

                private readonly int grid_cell_size;
                private readonly StructureGen structure_gen;
                private readonly GroundMeshGen mesh_gen;
                private readonly int mesh_chunk_size;
                public bool IsObjectValid(Vector2 world_pos_f)
                {
                        Vector2I world_pos = (Vector2I)world_pos_f;
                        var chunk_pos = world_pos / mesh_chunk_size;
                        var chunk_world_pos = chunk_pos * mesh_chunk_size;



                        if (!this[world_pos].structure_collision_dict_terrain_chunk_world_pos.TryGetValue(chunk_world_pos, out var structure))
                                return true;

                        return !structure.IsObjectColliding(world_pos);
                }
                public StructureChunk this[Vector2I world_pos]
                {
                        get
                        {
                                var global_grid_pos = world_pos / grid_cell_size;
                                var relative_grid_pos = global_grid_pos - current_player_grid_pos;
                                return grid[relative_grid_pos.X + 1 + (relative_grid_pos.Y + 1) * grid_width];
                        }

                }

                public void UpdatePlayerPos(Vector2 player_world_pos)
                {
                        var new_player_grid_pos = new Vector2I((int)player_world_pos.X / grid_cell_size, (int)player_world_pos.Y / grid_cell_size);
                        var delta = current_player_grid_pos - new_player_grid_pos;
                        if (delta == Vector2I.Zero)
                        {
                                return;
                        }
                        current_player_grid_pos = new_player_grid_pos;

                        // this is expensive, but allows for really fast access of the data 
                        var grid_copy = (StructureChunk[])grid.Clone();
                        for (int x = 0; x < grid_width; x++)
                        {
                                for (int y = 0; y < grid_width; y++)
                                {
                                        var new_x = x - delta.X;
                                        var new_y = y - delta.Y;
                                        if (new_x < 0 || new_y < 0 || new_x >= grid_width || new_y >= grid_width)
                                        {
                                                continue;
                                        }
                                        if (new_x == 1 || new_y == 1)
                                        {
                                                //Generate new cell data
                                                var world_x = (x - 2 + new_player_grid_pos.X) * grid_cell_size;
                                                var world_y = (y - 2 + new_player_grid_pos.Y) * grid_cell_size;
                                                grid[x + y * grid_width] = GenerateStructureChunk(new(world_x, world_y));
                                        }

                                        grid[new_x + new_y * grid_width] = grid_copy[x + y * grid_width];
                                }
                        }

                }

                public StructureGrid(StructureGen structure_gen, GroundMeshGen mesh_gen, int mesh_chunk_size, Vector2 player_world_pos)
                {
                        grid_cell_size = structure_gen.mesh_chunks_per_structure_grid_cell * mesh_chunk_size;
                        this.structure_gen = structure_gen;
                        this.mesh_gen = mesh_gen;
                        this.mesh_chunk_size = mesh_chunk_size;

                        current_player_grid_pos = new Vector2I((int)player_world_pos.X / grid_cell_size, (int)player_world_pos.Y / grid_cell_size);
                        grid = new StructureChunk[grid_width * grid_width];
                        for (int x = 0; x < grid_width; x++)
                        {
                                for (int y = 0; y < grid_width; y++)
                                {
                                        //Generate new cell data
                                        var world_x = (x - 2 + current_player_grid_pos.X) * grid_cell_size;
                                        var world_y = (y - 2 + current_player_grid_pos.Y) * grid_cell_size;
                                        grid[x + y * grid_width] = GenerateStructureChunk(new(world_x, world_y));
                                }
                        }
                }

                private StructureInstanceData GenerateRandomStructureInstance(StructureType structure_type, Vector2I base_chunk_world_pos)
                {
                        var structure_world_pos = new Vector2(GD.Randf(), GD.Randf()) * mesh_chunk_size + base_chunk_world_pos;
                        var structure_rotation = GD.Randf() * 360f;
                        var structure_scale = structure_type.base_scale + (GD.Randf() * 2 - 1) * structure_type.scale_change_amplitude;
                        var base_height = mesh_gen.CalculateHeight(structure_world_pos, out _);

                        return new StructureInstanceData(structure_world_pos, structure_scale, structure_rotation, base_height, structure_type);
                }
                private bool IsStructureValid(StructureInstanceData structure_instance, HashSet<Vector2I> collision_chunks, Dictionary<Vector2I, StructureInstanceData> structure_collision_dict_terrain_chunk_world_pos)
                {

                        if (!structure_instance.IsValid(mesh_gen))
                                return false;

                        bool there_already_was_struct_on_one_of_the_chunks = false;
                        foreach (var chunk_world_pos in collision_chunks)
                        {
                                if (structure_collision_dict_terrain_chunk_world_pos.ContainsKey(chunk_world_pos))
                                {

                                        there_already_was_struct_on_one_of_the_chunks = true;
                                        break;
                                }
                        }
                        return !there_already_was_struct_on_one_of_the_chunks;
                }
                public StructureChunk GenerateStructureChunk(Vector2I base_world_pos)
                {
                        Dictionary<Vector2I, StructureInstanceData> structure_instantiation_dict_terrain_chunk_world_pos = [];
                        Dictionary<Vector2I, StructureInstanceData> structure_collision_dict_terrain_chunk_world_pos = [];
                        RNG.SetGDRandomSeed(base_world_pos);
                        foreach (var structure_type in structure_gen.structure_pool)
                        {
                                if (structure_gen.mesh_chunks_per_structure_grid_cell < 2 * structure_type.min_distance_from_grid_border_in_mesh_chunks)
                                        GD.PrintErr("structure_gen.mesh_chunks_per_structure_grid_cell has to ge at least 2x the structure_type.min_distance_from_grid_border_in_mesh_chunks.");

                                for (int i = 0; i < structure_type.generation_attempts_per_structure_chunk; i++)
                                {

                                        if (structure_type.spawn_chance < GD.Randf())
                                                continue;

                                        Vector2I base_chunk_world_pos;
                                        {
                                                var mesh_chunk_x = GD.RandRange(structure_type.min_distance_from_grid_border_in_mesh_chunks, structure_gen.mesh_chunks_per_structure_grid_cell - structure_type.min_distance_from_grid_border_in_mesh_chunks);
                                                var mesh_chunk_y = GD.RandRange(structure_type.min_distance_from_grid_border_in_mesh_chunks, structure_gen.mesh_chunks_per_structure_grid_cell - structure_type.min_distance_from_grid_border_in_mesh_chunks);
                                                base_chunk_world_pos = new Vector2I(mesh_chunk_x, mesh_chunk_y) * mesh_chunk_size + base_world_pos;
                                        }

                                        var structure_instance = GenerateRandomStructureInstance(structure_type, base_chunk_world_pos);
                                        var collision_chunks = structure_instance.MeshChunksThisStructureSitsOnWorldPos(mesh_chunk_size);
                                        if (!IsStructureValid(structure_instance, collision_chunks, structure_collision_dict_terrain_chunk_world_pos))
                                                continue;

                                        foreach (var chunk_world_pos in collision_chunks)
                                        {
                                                structure_collision_dict_terrain_chunk_world_pos.TryAdd(chunk_world_pos, structure_instance);
                                        }


                                        structure_instantiation_dict_terrain_chunk_world_pos.TryAdd(base_chunk_world_pos, structure_instance);

                                }
                        }
                        return new(structure_collision_dict_terrain_chunk_world_pos, structure_instantiation_dict_terrain_chunk_world_pos);

                }
        }
}
