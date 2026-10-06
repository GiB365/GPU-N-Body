#[compute]
#version 450
#define THREADS 256
#define FLT_MAX 3.402823466e+38

layout(local_size_x = THREADS) in;

layout (set = 0, binding = 0, std430) restrict buffer BoundsBuffer {
    float bounds[]; //(max, max, min, min)
};

layout(push_constant, std430) uniform Parameters {
    uint groups;
} parameters;

shared vec2 mins[THREADS];
shared vec2 maxes[THREADS];

void main() {
    uint local_id = gl_LocalInvocationID.x;
    vec2 min_position = vec2(FLT_MAX);
    vec2 max_position = vec2(-FLT_MAX);

    for (uint current_workgroup = local_id; current_workgroup < parameters.groups; current_workgroup += THREADS) {
        min_position = min(min_position, vec2(bounds[current_workgroup*4 + 2], bounds[current_workgroup*4 + 3]));
        max_position = max(max_position, vec2(bounds[current_workgroup*4], bounds[current_workgroup*4 + 1]));
    }

    mins[local_id] = min_position;
    maxes[local_id] = max_position;
    barrier();

    for (uint split = THREADS/2; split > 0; split >>= 1) {
        if (local_id < split) {
            mins[local_id]  = min(mins[local_id],  mins[local_id + split]);
            maxes[local_id] = max(maxes[local_id], maxes[local_id + split]);
        }

        barrier();
    }

    if (local_id == 0) {
        bounds[0] = maxes[0].x;
        bounds[1] = maxes[0].y;
        bounds[2] = mins[0].x;
        bounds[3] = mins[0].y;
    }
}