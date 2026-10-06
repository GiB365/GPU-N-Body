#[compute]
#version 450
#define THREADS 256
#define FLT_MAX 3.402823466e+38

layout(local_size_x = THREADS) in;

layout(set = 0, binding = 0, std430) restrict buffer PositionBuffer {
    vec2 position_buffer[];
};

layout (set = 0, binding = 1, std430) restrict buffer BoundsBuffer {
    float bounds[]; //(max, max, min, min)
};

shared vec2 mins[THREADS];
shared vec2 maxes[THREADS];

layout(push_constant, std430) uniform Parameters {
    uint particle_count;
} parameters;

void main() {
    uint local_id = gl_LocalInvocationID.x;
    uint global_id = gl_GlobalInvocationID.x;

    vec2 current_position = (global_id < parameters.particle_count) ? position_buffer[global_id] : vec2(0.0f);
    bool valid_index = global_id < parameters.particle_count;
    mins[local_id] = valid_index ? current_position : vec2(FLT_MAX);
    maxes[local_id] = valid_index ? current_position : vec2(-FLT_MAX);

    barrier();

    for (uint split = THREADS / 2; split > 0; split >>= 1) {
        if (local_id < split) {
            mins[local_id] = min(mins[local_id], mins[local_id + split]);
            maxes[local_id] = max(maxes[local_id], maxes[local_id + split]);
        }

        barrier();
    }

    if (local_id == 0) {
        uint base_index = gl_WorkGroupID.x*4;
        bounds[base_index] = maxes[0].x;
        bounds[base_index + 1] = maxes[0].y;
        bounds[base_index+ 2] = mins[0].x;
        bounds[base_index + 3] = mins[0].y;
    }
}