using Godot;
[Tool]
public partial class GroundShaderController : Node
{

        [ExportGroup("rock")]
        [Export] Texture rock_texture;
        [Export] Texture rock_normal_map;
        [Export] Texture rock_roughness;
        [Export] float rock_scale;
        [Export] float rock_color_gain;


        [ExportGroup("post processing")]
        [Export] float global_color_gain;
        [Export] float global_color_offset;

        [Export] Texture post_processing_noise;
        [Export] float post_processing_noise_scale;
        [Export] float post_processing_albedo_influence;
        [Export] float metallic;
        [Export] float spectacular;
        [Export] ShaderMaterial ground_shader_material;

        public void UpdateTheBiomeTextures(ImageTexture[] biome_textures_1, ImageTexture[] biome_textures_2)
        {
                ground_shader_material.SetShaderParameter("biome_textures_1", biome_textures_1);
                ground_shader_material.SetShaderParameter("biome_textures_2", biome_textures_2);
        }

        public void SetShaderConfiguration(Biome[] biomes)
        {

                var biome_albedo_textures = new Texture[biomes.Length];
                var biome_normal_textures = new Texture[biomes.Length];
                var biome_roughness_textures = new Texture[biomes.Length];
                var biome_texture_tints = new Vector3[biomes.Length];
                var biome_texture_color_offsets = new float[biomes.Length];
                var biome_texture_scales = new float[biomes.Length];
                int i = 0;
                foreach (var biome in biomes)
                {

                        biome.index_in_biomes_array = (byte)i;
                        biome_albedo_textures[i] = biome.albedo;
                        biome_normal_textures[i] = biome.normal;
                        biome_roughness_textures[i] = biome.roughness;
                        biome_texture_tints[i] = new(biome.tint.R, biome.tint.G, biome.tint.B);
                        biome_texture_color_offsets[i] = biome.color_offset;
                        biome_texture_scales[i] = biome.scale;
                        i++;
                }
                ground_shader_material.SetShaderParameter("rock_color_gain", rock_color_gain);
                ground_shader_material.SetShaderParameter("rock_scale", rock_scale);
                ground_shader_material.SetShaderParameter("rock_normal_map", rock_normal_map);
                ground_shader_material.SetShaderParameter("rock_roughness", rock_roughness);
                ground_shader_material.SetShaderParameter("rock_texture", rock_texture);

                ground_shader_material.SetShaderParameter("metallic", metallic);
                ground_shader_material.SetShaderParameter("spectacular", spectacular);

                ground_shader_material.SetShaderParameter("biome_texture_tints", biome_texture_tints);
                ground_shader_material.SetShaderParameter("biome_texture_color_offsets", biome_texture_color_offsets);
                ground_shader_material.SetShaderParameter("biome_texture_scales", biome_texture_scales);

                ground_shader_material.SetShaderParameter("biome_albedo_textures", biome_albedo_textures);
                ground_shader_material.SetShaderParameter("biome_roughness_textures", biome_roughness_textures);
                ground_shader_material.SetShaderParameter("biome_normal_textures", biome_normal_textures);

                ground_shader_material.SetShaderParameter("post_processing_noise", post_processing_noise);
                ground_shader_material.SetShaderParameter("post_processing_albedo_influence", post_processing_albedo_influence);

                ground_shader_material.SetShaderParameter("post_processing_noise_scale", post_processing_noise_scale);


                ground_shader_material.SetShaderParameter("global_color_offset", global_color_offset);
                ground_shader_material.SetShaderParameter("global_color_gain", global_color_gain);
        }
}
