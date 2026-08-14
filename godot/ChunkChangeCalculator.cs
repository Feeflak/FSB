using System.Collections.Generic;
using System.Linq;
using Godot;
public static class ChunkChangeCalculator
{
        public struct ChunkChange(Vector2I[] chunks_to_destroy_relative_positions, Vector2I[] chunks_to_instantiate)
        {
                public Vector2I[] to_destroy_relative_pos = chunks_to_destroy_relative_positions;
                public Vector2I[] to_generate_relative_pos = chunks_to_instantiate;
        }
        private static ChunkChange CalculateChunkChangeForPosChange(Vector2I delta)
        {
                delta *= terrain_chunk_size;

                HashSet<Vector2I> old_chunk_pos = [.. GetAllChunksInViewDistance()];
                HashSet<Vector2I> new_chunk_pos = [.. old_chunk_pos.Select(pos => pos + delta)];

                var to_destroy = old_chunk_pos.Except(new_chunk_pos).ToArray();

                List<Vector2I> to_generate = [];
                foreach (var chunk in old_chunk_pos)
                {
                        var new_pos = chunk + delta;
                        if (!old_chunk_pos.Contains(new_pos))
                        {
                                to_generate.Add(chunk);
                        }
                }

                return new ChunkChange(to_destroy, [.. to_generate]);
        }

        private static int view_distance_chunks;
        private static int terrain_chunk_size;

        public static List<Vector2I> GetAllChunksInViewDistance()
        {
                List<Vector2I> output = [];

                // could be pre-calculated once
                for (int x = -view_distance_chunks; x <= view_distance_chunks; x++)
                {
                        for (int y = -view_distance_chunks; y <= view_distance_chunks; y++)
                        {
                                if (x * x + y * y >= view_distance_chunks * view_distance_chunks)
                                        continue;

                                output.Add(new Vector2I(x * terrain_chunk_size, y * terrain_chunk_size));
                        }
                }

                return output;
        }
        public static Dictionary<Vector2I, ChunkChange> chunk_change_for_position_delta = [];

        public static void Init(int _view_distance_chunks, int _terrain_chunk_size)
        {
                view_distance_chunks = _view_distance_chunks;
                terrain_chunk_size = _terrain_chunk_size; chunk_change_for_position_delta = [];
                Vector2I delta = new(-1, 0);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(-1, 1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(0, 1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(1, 1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(1, 0);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(1, -1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(0, -1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
                delta = new(-1, -1);
                chunk_change_for_position_delta.Add(delta, CalculateChunkChangeForPosChange(delta));
        }
}
