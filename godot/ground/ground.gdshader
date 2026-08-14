shader_type spatial;

struct NormalAlbedoRoughness{
	vec3 normal;
	vec3 albedo;
	vec3 roughness;
};


//  Under the appache license https://github.com/acegiak/Godot4TerrainShader/tree/main?tab=Apache-2.0-1-ov-file#readme
// Taken from the https://github.com/acegiak/Godot4TerrainShader/blob/main/addons/terrain-shader/StochasticTexture.gdshader.
// Modified so that it works with multiple textures. '-'
vec2 hash( vec2 p )
{
	return fract( sin( p * mat2( vec2( 127.1, 311.7 ), vec2( 269.5, 183.3 ) ) ) * 43758.5453 );
}
// 3 birds with one stone
NormalAlbedoRoughness triple_stochastic_sample(sampler2D normal_texture,sampler2D albedo_texture,sampler2D roughness_texture, vec2 uv){
        vec2 skewV = mat2(vec2(1.0,1.0),vec2(-0.57735027 , 1.15470054))*uv * 3.464;
	
	vec2 vxID = floor(skewV);
	vec2 fracV = fract(skewV);
	vec3 barry = vec3(fracV.x,fracV.y,1.0-fracV.x-fracV.y);
	
	mat4 bw_vx = barry.z>0.0?
		mat4(vec4(vxID,0.0,0.0),vec4((vxID+vec2(0.0,1.0)),0.0,0.0),vec4(vxID+vec2(1.0,0.0),0,0),vec4(barry.zyx,0)):
		mat4(vec4(vxID+vec2(1.0,1.0),0.0,0.0),vec4((vxID+vec2(1.0,0.0)),0.0,0.0),vec4(vxID+vec2(0.0,1.0),0,0),vec4(-barry.z,1.0-barry.y,1.0-barry.x,0));
		
	vec2 ddx = dFdx(uv);
	vec2 ddy = dFdy(uv);

//        vec4 normal = textureGrad(normal_texture,uv+hash(bw_vx[0].xy),ddx,ddy)*bw_vx[3].x) +
//	(textureGrad(normal_texture,uv+hash(bw_vx[1].xy),ddx,ddy)*bw_vx[3].y) +
//	(textureGrad(normal_texture,uv+hash(bw_vx[2].xy),ddx,ddy)*bw_vx[3].z);

    vec4 normal = (textureGrad(normal_texture,uv+hash(bw_vx[0].xy),ddx,ddy)*bw_vx[3].x) +
	(textureGrad(normal_texture,uv+hash(bw_vx[1].xy),ddx,ddy)*bw_vx[3].y) +
	(textureGrad(normal_texture,uv+hash(bw_vx[2].xy),ddx,ddy)*bw_vx[3].z);
	vec4 albedo = (textureGrad(albedo_texture,uv+hash(bw_vx[0].xy),ddx,ddy)*bw_vx[3].x) +
	(textureGrad(albedo_texture,uv+hash(bw_vx[1].xy),ddx,ddy)*bw_vx[3].y) +
	(textureGrad(albedo_texture,uv+hash(bw_vx[2].xy),ddx,ddy)*bw_vx[3].z);
	vec4 roughness = (textureGrad(roughness_texture,uv+hash(bw_vx[0].xy),ddx,ddy)*bw_vx[3].x) +
	(textureGrad(roughness_texture,uv+hash(bw_vx[1].xy),ddx,ddy)*bw_vx[3].y) +
	(textureGrad(roughness_texture,uv+hash(bw_vx[2].xy),ddx,ddy)*bw_vx[3].z);
	return NormalAlbedoRoughness(normal.xyz,albedo.xyz,roughness.xyz);

}
// Rest is written by me, *FR*.


const int BIOMES_COUNT = 8;

uniform float global_color_gain;
uniform float global_color_offset;

uniform float rock_saturation;
uniform float rock_scale;
uniform sampler2D rock_texture;
uniform sampler2D rock_normal_map;
uniform sampler2D rock_roughness;

//WARN: this value HAS TO BE THE SAME as the one in the TerrianGen.cs !!!
const int biome_textures_count = 517;
instance uniform int biome_texture_index;
// repeat is disabled, so that the data from one end of the chunk doesn't influence data form the other end of the chunk
uniform sampler2D[biome_textures_count] biome_textures_1 : repeat_disable;
uniform sampler2D[biome_textures_count] biome_textures_2 : repeat_disable;

uniform vec3[BIOMES_COUNT] biome_texture_tints;
uniform float[BIOMES_COUNT] biome_texture_color_offsets;
uniform float[BIOMES_COUNT] biome_texture_scales;
uniform sampler2D[BIOMES_COUNT] biome_albedo_textures;
uniform sampler2D[BIOMES_COUNT] biome_normal_textures;
uniform sampler2D[BIOMES_COUNT] biome_roughness_textures;

uniform sampler2D post_processing_noise;
uniform float post_processing_noise_scale;
uniform float metallic;
uniform float post_processing_albedo_influence;
uniform float spectacular;


NormalAlbedoRoughness triple_stochastic_triplanar (vec3 position, vec3 worldNormal, vec3 adjusted_normal,vec3 biome_tint,
	float biome_color_offset, float biome_scale,
	sampler2D biome_normal_map, sampler2D biome_texture, sampler2D biome_roughness){
	vec3 weights = adjusted_normal / (adjusted_normal.x + adjusted_normal.y + adjusted_normal.z) * 3.0;

	vec2 uv_x = position.zy;
	vec2 uv_y = position.xz;
	vec2 uv_z = position.xy;

	NormalAlbedoRoughness output = NormalAlbedoRoughness(vec3(0), vec3(0), vec3(0));
	NormalAlbedoRoughness partial;
	if (weights.x > 0.01){
		partial = triple_stochastic_sample(rock_normal_map, rock_texture, rock_roughness, uv_x * rock_scale);
		output.normal += partial.normal * weights.x;
		output.albedo += partial.albedo * rock_saturation * weights.x;
		output.roughness += partial.roughness * weights.x;
	}
	if (weights.y > 0.01){
		partial = triple_stochastic_sample(biome_normal_map, biome_texture, biome_roughness, uv_y * biome_scale);
		output.normal += partial.normal * weights.y;
		output.albedo += (partial.albedo + biome_tint - vec3(biome_color_offset))* weights.y  ;
		output.roughness += partial.roughness * weights.y;
	}
	if (weights.z > 0.01){
		partial = triple_stochastic_sample(rock_normal_map, rock_texture, rock_roughness, uv_z * rock_scale);
		output.normal += partial.normal * weights.z;
		output.albedo += partial.albedo * rock_saturation * weights.z;
		output.roughness += partial.roughness * weights.z;
	}

	return output;
}

void handle_biome(int biome, float influence, vec3 position, vec3 worldNormal, vec3 adjusted_normal,
	inout vec3 output_color, inout vec3 output_normal, inout vec3 output_roughness){
	if (influence < 0.01){
		return ;
	}

	NormalAlbedoRoughness output = triple_stochastic_triplanar(position, worldNormal, adjusted_normal,
		biome_texture_tints[biome], biome_texture_color_offsets[biome], biome_texture_scales[biome],
		biome_normal_textures[biome], biome_albedo_textures[biome], biome_roughness_textures[biome]);

	output_color += (output.albedo) * influence;
	output_normal += output.normal * influence;
	output_roughness += output.roughness * influence;
}

NormalAlbedoRoughness collect_biome_data(vec4 biome_data_1, vec4 biome_data_2,vec3 world_pos, vec3 world_normal, vec3 adjusted_normal){
	vec3 output_color = vec3(0, 0, 0);
	vec3 output_normal = vec3(0, 0, 0);
	vec3 output_roughness = vec3(0, 0, 0);

	handle_biome(0, biome_data_1.r, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(1, biome_data_1.g, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(2, biome_data_1.b, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(3, biome_data_1.a, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(4, biome_data_2.r, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(5, biome_data_2.g, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(6, biome_data_2.b, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);
	handle_biome(7, biome_data_2.a, world_pos, world_normal, adjusted_normal, output_color, output_normal, output_roughness);

	return NormalAlbedoRoughness(output_normal, output_color, output_roughness);
}

void fragment(){
	vec3 world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 world_normal = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec3 adjusted_normal = pow(abs(world_normal), vec3(8.0));


	vec4 biome_data_1 =texture(biome_textures_1[biome_texture_index],UV);
	vec4 biome_data_2 =texture(biome_textures_2[biome_texture_index],UV);
	
	
	NormalAlbedoRoughness output = collect_biome_data(biome_data_1, biome_data_2, world_pos, world_normal, adjusted_normal);
	ALBEDO = output.albedo;
	NORMAL_MAP = output.normal;
	ROUGHNESS  = output.roughness.r;

	//apply processing

	ALBEDO *= global_color_gain;
	ALBEDO *= mix(1.0 - post_processing_albedo_influence, 1.0 + post_processing_albedo_influence, texture(post_processing_noise, world_pos.xz / 100.0 * post_processing_noise_scale).r);
	ALBEDO -= vec3(1) * global_color_offset;
	 //added some offset so that I can use one noise and make the metallic and spectacular independent
	METALLIC = texture(post_processing_noise, world_pos.xz / 100.0 * post_processing_noise_scale + vec2(1230,3210)).r * metallic;
	SPECULAR = texture(post_processing_noise, world_pos.xz / 100.0 * post_processing_noise_scale + vec2(4560,6540)).r * spectacular;
}
