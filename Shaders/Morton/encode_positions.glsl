#[compute]
#version 450
#define MAX_16 65535.0

layout(local_size_x = 256) in;

layout(set = 0, binding = 0, std430) restrict buffer PositionBuffer {
    vec2 position_buffer[];
};

layout (set = 0, binding = 1, std430) restrict buffer MortonBuffer {
    uint morton_buffer[];
};

layout(set = 0, binding = 2, std430) restrict buffer BoundsBuffer {
    float bounds[]; // (max, max, min, min)
};

layout(push_constant, std430) uniform Parameters {
    uint bits_per_axis;
    uint particle_count;
} parameters;

uint mortonShift(uint x) {
    x &= 0x0000ffff;
    x = (x ^ (x <<  8)) & 0x00ff00ff; 
    x = (x ^ (x <<  4)) & 0x0f0f0f0f; 
    x = (x ^ (x <<  2)) & 0x33333333; 
    x = (x ^ (x <<  1)) & 0x55555555;
    return x;
}

uint mortonEncode(uint x, uint y) {
    return uint((mortonShift(y) << 1) | mortonShift(x));
}

void main() {
    uint global_id = gl_GlobalInvocationID.x;

    if (global_id >= parameters.particle_count) return;

    float size = max(bounds[0] - bounds[2], bounds[1] - bounds[3]);
    float normalized_x = (position_buffer[global_id].x - bounds[2]) / size;
    float normalized_y = (position_buffer[global_id].y - bounds[3]) / size;

    uint quantized_x = uint(clamp(normalized_x, 0.0f, 1.0f) * MAX_16);
    uint quantized_y = uint(clamp(normalized_y, 0.0f, 1.0f) * MAX_16);

    morton_buffer[global_id] = mortonEncode(quantized_x, quantized_y);
}