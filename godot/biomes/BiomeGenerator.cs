using System.Collections.Generic;
using Godot;
[Tool]
public partial class BiomeGenerator : Node
{

        private const int COLOR_CHANNELS = 4;
        [Export] int biome_map_resolution;
        [Export] TerrainAspectsSolver terrain_aspects_solver;

        public class TextureData(int texture_resolution, byte[][] biome_maps, Biome[] biomes)
        {
                // biome_maps[biome texture index][x pixel inside the texture + y pixel * map map_resolution]
                readonly byte[][] biome_textures = biome_maps;
                readonly int texture_resolution = texture_resolution;
                readonly Biome[] biomes = biomes;

                public List<BiomeInfluence> GetBiomeInfluenceForUV(Vector2 uv)
                {
                        int x = Mathf.FloorToInt(texture_resolution * uv.X);// uv to pixel pos
                        int y = Mathf.FloorToInt(texture_resolution * uv.Y);

                        int base_pixel_index = x + y * texture_resolution;
                        List<BiomeInfluence> output = [];
                        for (int biome_index = 0; biome_index < biomes.Length; biome_index++)
                        {
                                HandleBiomeInfluenceSampling(base_pixel_index, biome_index, ref output);
                        }
                        return output;

                }
                private void HandleBiomeInfluenceSampling(int base_pixel_index, int biome_index, ref List<BiomeInfluence> output)
                {
                        int biome_texture = biome_index / COLOR_CHANNELS;
                        int index_inside_biome_texture = base_pixel_index + biome_index % COLOR_CHANNELS;
                        float influence = FloatConversions.ByteToFloat(biome_textures[biome_texture][index_inside_biome_texture]);
                        if (influence < 0.1f)
                        {
                                return;
                        }
                        output.Add(new(biomes[biome_index], influence));
                }


                public ImageTexture GetTexture(int texture_index)
                {
                        if (texture_index >= biome_textures.Length)
                        {
                                GD.PushWarning($"The requested biome texture index was outside of the array, returning blank texture\n " +
                                                $" requested-{texture_index} length- {biome_textures.Length}");
                                var image = Image.CreateEmpty(texture_resolution, texture_resolution, false, Image.Format.Rgba8);
                                return ImageTexture.CreateFromImage(image);
                        }
                        else
                        {
                                var data = biome_textures[texture_index];
                                var image = Image.CreateFromData(texture_resolution, texture_resolution, false, Image.Format.Rgba8, data);
                                return ImageTexture.CreateFromImage(image);
                        }
                }
        }
        public class BiomeInfluence(Biome biome, float influence)
        {
                public Biome biome = biome;
                public float influence = influence;
        }

        [Export] float biome_transitions_smoothness;
        private float GetInfluenceForTerrainAspect(FloatRange preferred, float value)
        {
                return Mathf.SmoothStep(preferred.min - biome_transitions_smoothness, preferred.max, value)
                         * (1 - Mathf.SmoothStep(preferred.min, preferred.max + biome_transitions_smoothness, value));
        }
        [Export] int backup_biome_index;
        private List<BiomeInfluence> GetBiomeInfluences(TerrainAspectsSolver.TerrainAspects terrain_aspects, Biome[] biomes)
        {
                float sum_of_influences = 0;
                List<BiomeInfluence> output = [];

                foreach (var biome in biomes)
                {
                        float influence =
                                    GetInfluenceForTerrainAspect(biome.preferred_moisture, terrain_aspects.moisture) *
                                    GetInfluenceForTerrainAspect(biome.preferred_elevation, terrain_aspects.elevation) *
                                    GetInfluenceForTerrainAspect(biome.preferred_temperature, terrain_aspects.temperature);
                        if (influence == 0)
                        {
                                continue;
                        }
                        sum_of_influences += influence;
                        output.Add(new(biome, influence));
                }
                if (sum_of_influences == 0)
                {
                        GD.Print($"There is no valid biome for this terrain, using the backup biome! terrain aspects:moisture:{terrain_aspects.moisture} elevation:{terrain_aspects.elevation} temperature:{terrain_aspects.temperature}");
                        return [new BiomeInfluence(biomes[backup_biome_index], influence: 1)];
                }
                var normalization_factor = 1f / sum_of_influences;
                foreach (var biome in output)
                {
                        biome.influence *= normalization_factor;
                }

                return output;
        }
        private static byte[][] InitializeBiomeTexturesArray(int textures_count, int pixels_per_axis)
        {

                int texture_size = pixels_per_axis * pixels_per_axis * COLOR_CHANNELS;
                byte[][] biome_textures = new byte[textures_count][];

                for (int i = 0; i < textures_count; i++)
                {
                        biome_textures[i] = new byte[texture_size];
                }

                return biome_textures;
        }
        public TextureData GenerateTextureData(Vector2 base_world_position, int terrain_chunk_size, Biome[] biomes)
        {
                float pixel_size = (float)terrain_chunk_size / (biome_map_resolution - 1);

                var biome_textures_count = Mathf.CeilToInt((float)biomes.Length / COLOR_CHANNELS);
                byte[][] biome_textures = InitializeBiomeTexturesArray(biome_textures_count, biome_map_resolution);

                for (int x = 0; x < biome_map_resolution; x++)
                {
                        for (int y = 0; y < biome_map_resolution; y++)
                        {
                                Vector2 world_pos = new Vector2(x, y) * pixel_size + base_world_position;

                                List<BiomeInfluence> biome_influences = GetBiomeInfluences(terrain_aspects_solver.SolveForPos(world_pos), biomes);
                                int pixel_index = (x + y * biome_map_resolution) * COLOR_CHANNELS;
                                foreach (var biome_influence in biome_influences)
                                {
                                        var texture_index = biome_influence.biome.index_in_biomes_array / COLOR_CHANNELS;
                                        int color_channel_index = biome_influence.biome.index_in_biomes_array % COLOR_CHANNELS;
                                        biome_textures[texture_index][pixel_index + color_channel_index] = FloatConversions.FloatToByte(biome_influence.influence);
                                }
                        }
                }

                return new(biome_map_resolution, biome_textures, biomes);
        }

}
