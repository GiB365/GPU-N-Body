#[compute]
#version 450
#define MAX_BASE 256

layout(local_size_x = 256) in;

layout(set = 0, binding = 0, std430) restrict buffer HistogramBuffer { 
    uint histogram_buffer[];
};

layout(set = 0, binding = 1, std430) restrict buffer ScanBuffer {
    uint scan_buffer[];
};

layout(push_constant, std430) uniform Parameters {
    uint base;
} parameters;

shared uint tree[MAX_BASE];


// TODO: Fix bank conflicts
void main() {
    uint local_id = gl_LocalInvocationID.x;
    uint workgroup_id = gl_WorkGroupID.x;
    uint offset = 1;

    if (local_id < parameters.base) {
        uint global_index = local_id + workgroup_id*parameters.base;
        tree[local_id] = histogram_buffer[global_index];
    }

    for (uint threads = parameters.base >> 1; threads > 0; threads >>= 1) {
        barrier();
        if (local_id < threads) {
            uint ai = offset * (2 * local_id + 1) - 1;
            uint bi = offset * (2 * local_id + 2) - 1;

            tree[bi] += tree[ai];
        }

        offset <<= 1;
    }

    if (local_id == 0) {
        tree[parameters.base-1] = 0;
    }

    for (uint threads = 1; threads < parameters.base; threads *= 2) {
        offset >>= 1;
        barrier();

        if (local_id < threads) {
            uint ai = offset * (2 * local_id + 1) - 1;
            uint bi = offset * (2 * local_id + 2) - 1;

            uint temp = tree[ai];
            tree[ai] = tree[bi];
            tree[bi] += temp;
        }
    }

    barrier();

    if (local_id < parameters.base) {
        // workgroup_id + 1 because the first parameters.base indices are for global prefix sums
        uint global_index = local_id + (workgroup_id+1)*parameters.base;
        scan_buffer[global_index] = tree[local_id];
    }
}