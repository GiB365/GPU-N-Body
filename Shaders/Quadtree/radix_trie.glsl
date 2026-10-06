#[compute]
#version 450
#define MAX_BASE 256
#define THREADS 256

layout(local_size_x = THREADS) in;

layout(set = 0, binding = 0, std430) restrict buffer MortonBuffer { 
    uint morton_buffer[];
};

layout(set = 0, binding = 0, std430) restrict buffer QuadnodeParents { 
    uint quadnode_parents[];
};

layout(set = 0, binding = 0, std430) restrict buffer QuadnodeChildren { 
    uvec2 quadnode_children[];
};

layout(push_constant, std430) uniform Parameters {
    uint particle_count;
} parameters;

shared uint local_histogram[MAX_BASE];

uint count_common_bits(uint a, uint b) {
    uint shift = 31;

    uint morton_a = morton_buffer[a];
    uint morton_b = morton_buffer[b];

    uint digit_a = (morton_a >> shift) & 1;
    uint digit_b = (morton_b >> shift) & 1;

    while (digit_a == digit_b) {
        shift--;
        digit_a = (morton_a >> shift) & 1;
        digit_b = (morton_b >> shift) & 1;
    }

    return 31 - shift;
}

void main() {
    uint local_id = gl_LocalInvocationID.x;

    uint left_bound;
    uint right_bound;

    if (local_id == 0) {
        left_bound = 0;
        right_bound = particle_count-1;
    }
    else if (local_id < particle_count) {
        uint left_common_bits = count_common_bits(local_id, local_id-1);
        uint right_common_bits = local_id == particle_count-1 ? -1 : count_common_bits(local_id, local_id+1);

        uint missing_bounds_direction = sign(right_common_bits - left_common_bits);
        uint min_common_digits = min(left_common_bits, right_common_bits);
        uint i = 2;

        while (count_common_bits(local_id, local_id+missing_bounds_direction*i)) {
            i++;
        }

        left_bound = missing_bounds_direction > 0 ? local_id-i : local_id;
        right_bound = missing_bounds_direction > 0 ? local_id+i : local_id;    
    }
}