#[compute]
#version 450

layout(local_size_x = 256) in;

layout(set = 0, binding = 0, std430) restrict buffer PositionBuffer { 
    vec2 position_buffer[];
};

layout(set = 0, binding = 1, std430) restrict buffer VelocityBuffer { 
    vec2 velocity_buffer[];
};

layout(set = 0, binding = 2, std430) restrict buffer AccelerationBuffer { 
    vec2 acceleration_buffer[];
};

layout(set = 0, binding = 3, std430) restrict buffer ColorBuffer { 
    vec4 color_buffer[];
};

layout(set = 0, binding = 4, rgba32f) uniform restrict writeonly image2D particle_transform;
layout(set = 0, binding = 5, rgba32f) uniform restrict writeonly image2D particle_color;

layout(push_constant, std430) uniform Parameters {
    float delta;
    uint texture_width;
} parameters;

void main() {
    uint affected_index = gl_GlobalInvocationID.x;

    if (affected_index >= position_buffer.length()) return;

    position_buffer[affected_index] += velocity_buffer[affected_index] * parameters.delta;
    velocity_buffer[affected_index] += 0.5 * acceleration_buffer[affected_index] * parameters.delta;

    uint texture_width_int = uint(parameters.texture_width);
    ivec2 texel = ivec2(affected_index % texture_width_int, affected_index / parameters.texture_width);

    imageStore(particle_transform, texel, vec4(position_buffer[affected_index], velocity_buffer[affected_index]));
    imageStore(particle_color, texel, vec4(color_buffer[affected_index]));
}