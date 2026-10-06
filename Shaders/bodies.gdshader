shader_type canvas_item;

uniform sampler2D particle_transform : filter_nearest;
uniform sampler2D particle_color : filter_nearest;
uniform int texture_width;
uniform vec2 camera_offset;

varying flat int instance_id;

void vertex() {
	instance_id = INSTANCE_ID;
	ivec2 texel = ivec2(INSTANCE_ID % texture_width, INSTANCE_ID / texture_width);
    vec4 position_velocity = texelFetch(particle_transform, texel, 0);

    VERTEX += position_velocity.xy - camera_offset;
}

void fragment() {
	ivec2 texel = ivec2(instance_id % texture_width, instance_id / texture_width);
    vec4 color_mass = texelFetch(particle_color, texel, 0);
    COLOR = vec4(color_mass.xyz, 0.0);
}