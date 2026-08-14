using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
[Tool]
public partial class GenerationController : Node
{
        [ExportToolButton("Run")] private Callable RunButton => Callable.From(RunClean);
        [Export] int terrain_chunk_size;
        [Export] Biome[] biomes;
        [Export] int max_main_thread_chunk_instantiation_per_frame;

        [ExportGroup("player")]
        [Export] Vector2 player_pos_offset;
        [Export] Node3D player;
        [Export] int view_distance_chunks;

        [ExportCategory("references")]
        [Export] PackedScene chunk_prefab;
        [Export] GroundMeshGen ground_mesh_gen;
        [Export] BiomeGenerator biome_generator;
        [Export] GroundShaderController ground_shader_controller;
        [Export] ObjectsGenerator objects_generator;
        [Export] StructureGen structure_gen;
        StructureGen.StructureGrid structure_grid;
        [Export] Node3D TestPoint;

        public override void _Ready()
        {

                if (!Engine.IsEditorHint())
                        RunClean();
        }

        /// When you want to change you need to also change the value in the ground shader 
        const int max_chunk_data_textures_count = 517;
        public override void _Process(double delta)
        {


                ChunkDataGeneration();
                InstantiateChunksFromQue();

        }
        private void DestroyChunks(Vector2I[] chunks_to_destroy)
        {
                foreach (var chunk_relative_pos in chunks_to_destroy)
                {
                        Vector2I chunk_world_position = chunk_relative_pos + last_player_chunk_grid_pos * terrain_chunk_size;

                        if (!chunk_per_world_position.TryGetValue(chunk_world_position, out var chunk))
                        {
                                GD.PushWarning("There was already a chunk in the dictionary at this position. This either indicates a but in the logic of this program or the player did some crazy movements. Regenerating the whole terrain.");

                                ClearAll();
                                GenerateDataForAllChunks();
                                return;
                        }

                        free_biome_texture_slots.Enqueue(chunk.biome_map_index);
                        chunk.QueueFree();
                        chunk_per_world_position.Remove(chunk_world_position);
                }

        }

        private void RunClean()
        {
                ClearAll();

                ChunkChangeCalculator.Init(view_distance_chunks, terrain_chunk_size);

                if (max_chunk_data_textures_count != ChunkChangeCalculator.GetAllChunksInViewDistance().Count)
                {
                        GD.PushWarning("The max amount of chunk data textures is not equal to the chunk data textures that are generated.\n" +
                                "This is not optimal and could cause chunks biomes to stop working:\n" +
                                $"current:{max_chunk_data_textures_count} optimal:{ChunkChangeCalculator.GetAllChunksInViewDistance().Count}");
                }

                ground_mesh_gen.Initialize(terrain_chunk_size);
                ground_shader_controller.SetShaderConfiguration(biomes);
                structure_grid = new StructureGen.StructureGrid(structure_gen, ground_mesh_gen, terrain_chunk_size, new(player.Position.X, player.Position.Z));

                GenerateDataForAllChunks();
        }

        public struct ChunkData(GroundMeshGen.MeshData mesh_data, BiomeGenerator.TextureData biome, Vector2I world_pos, ObjectsGenerator.ObjectTypeSpawnData[] objects_data, StructureInstanceData? structure)
        {
                public ObjectsGenerator.ObjectTypeSpawnData[] objects_data = objects_data;
                public GroundMeshGen.MeshData mesh_data = mesh_data;
                public BiomeGenerator.TextureData biome = biome;
                public Vector2I world_pos = world_pos;
                public StructureInstanceData? structure = structure;
        }

        Vector2I WorldToTerrainChunkGridPos(Vector2 world_pos)
        {
                return new Vector2I(Mathf.RoundToInt(world_pos.X / terrain_chunk_size), Mathf.RoundToInt(world_pos.Y / terrain_chunk_size));
        }
        Dictionary<Vector2I, Chunk> chunk_per_world_position;

        Vector2I last_player_chunk_grid_pos;
        private void ChunkDataGeneration()
        {

                // This could happen after building the project in the godot editor while the generation  process is running
                if (chunk_data_generation_task == null)
                {
                        ClearAll();
                        GenerateDataForAllChunks();
                }

                if (!chunk_data_generation_task.IsCompleted || !chunk_instantiation_que.IsEmpty)
                {
                        return;
                }

                // Update only once all chunks from the previous batch have been generated / destroyed  
                ground_shader_controller.UpdateTheBiomeTextures(biome_textures_channel_1, biome_textures_channel_2);

                Vector2 player_pos = new(player.Position.X, player.Position.Z);

                var current_player_chunk_grid_pos = WorldToTerrainChunkGridPos(player_pos);

                if (last_player_chunk_grid_pos == current_player_chunk_grid_pos)
                {
                        return;
                }
                var grid_pos_delta = current_player_chunk_grid_pos - last_player_chunk_grid_pos;

                if (!ChunkChangeCalculator.chunk_change_for_position_delta.TryGetValue(grid_pos_delta, out var chunk_change))
                {
                        GD.PushWarning("The position of player changed by more than a 1 chunk size which is not supported. Regenerating the whole terrain.");

                        last_player_chunk_grid_pos = current_player_chunk_grid_pos;
                        ClearAll();
                        GenerateDataForAllChunks();
                        return;
                }


                structure_grid.UpdatePlayerPos(player_pos);
                DestroyChunks(chunk_change.to_destroy_relative_pos);
                GenerateDataForChunks(chunk_change.to_generate_relative_pos, current_player_chunk_grid_pos * terrain_chunk_size);
                last_player_chunk_grid_pos = current_player_chunk_grid_pos;
        }
        private void GenerateDataForAllChunks()
        {
                var chunks_to_generate = ChunkChangeCalculator.GetAllChunksInViewDistance();
                GenerateDataForChunks([.. chunks_to_generate], last_player_chunk_grid_pos * terrain_chunk_size);
        }

        Task chunk_data_generation_task;
        ConcurrentQueue<ChunkData> chunk_instantiation_que = new();
        private void GenerateDataForChunks(Vector2I[] chunks_to_generate, Vector2I player_pos_snapped_to_chunk)
        {
                chunk_data_generation_task = Parallel.ForEachAsync(Enumerable.Range(0, chunks_to_generate.Length), async (i, _) =>
                     {
                             try
                             {
                                     var chunk = chunks_to_generate[i];
                                     Vector2I chunk_world_position = chunk + player_pos_snapped_to_chunk;

                                     var biome_data = biome_generator.GenerateTextureData(chunk_world_position, terrain_chunk_size + 1, biomes);
                                     var objects_data = objects_generator.GenerateObjectsData(terrain_chunk_size, biome_data, structure_grid, chunk_world_position);
                                     var mesh_data = ground_mesh_gen.GenerateChunkData(chunk_world_position);
                                     structure_grid[chunk_world_position].structure_instantiation_dict_terrain_chunk_world_pos.TryGetValue(chunk_world_position, out var chunk_structure_data);
                                     chunk_instantiation_que.Enqueue(new(mesh_data, biome_data, chunk_world_position, objects_data, chunk_structure_data));
                             }
                             catch (Exception e)
                             {
                                     GD.PrintErr($"GenerateDataForChunks failed: {e}");
                             }
                     });
        }
        private void InstantiateChunksFromQue()
        {
                int processed = 0;
                while (processed < max_main_thread_chunk_instantiation_per_frame && chunk_instantiation_que.TryDequeue(out var chunk_data))
                {
                        InstantiateChunk(chunk_data);
                        processed++;
                }
        }

        Queue<int> free_biome_texture_slots;
        ImageTexture[] biome_textures_channel_1;
        ImageTexture[] biome_textures_channel_2;
        private void InstantiateChunk(ChunkData chunk_data)
        {

                var chunk = (Chunk)chunk_prefab.Instantiate();
                chunk_per_world_position.Add(chunk_data.world_pos, chunk);

                AddChild(chunk);
                ground_mesh_gen.ApplyData(chunk_data.mesh_data, chunk.mesh_instance, chunk.collider);
                ObjectsGenerator.SpawnObjects(chunk_data.objects_data, chunk);


                int map_index = free_biome_texture_slots.Dequeue();
                chunk.biome_map_index = map_index;
                biome_textures_channel_1[map_index] = chunk_data.biome.GetTexture(0);
                biome_textures_channel_2[map_index] = chunk_data.biome.GetTexture(1);
                chunk.mesh_instance.SetInstanceShaderParameter("biome_texture_index", map_index);
                chunk_data.structure?.Instantiate(chunk);
        }
        private void ClearAll()
        {
                free_biome_texture_slots = new(Enumerable.Range(0, max_chunk_data_textures_count));
                biome_textures_channel_1 = new ImageTexture[max_chunk_data_textures_count];

                biome_textures_channel_2 = new ImageTexture[max_chunk_data_textures_count];

                chunk_per_world_position = [];

                foreach (var child in GetChildren())
                {
                        child.QueueFree();
                }
        }
}
