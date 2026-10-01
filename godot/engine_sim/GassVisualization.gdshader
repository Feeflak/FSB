shader_type spatial;
uniform sampler2D pressureSampler;
uniform float pressure;
uniform float minPressure;
uniform float maxPressure;

void fragment() {
vec3 albedo;
float scaledPressure = distance(minPressure, pressure) / distance(minPressure,maxPressure);
albedo = texture(pressureSampler,vec2(clamp(scaledPressure,0.001,.999),0)).xyz;

ALBEDO = albedo;
}
