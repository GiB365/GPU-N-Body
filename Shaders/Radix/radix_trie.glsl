#[compute]
#version 450
#define MAX_BASE 256
#define THREADS 256

layout(local_size_x = THREADS) in;

layout(set = 0, binding = 0, std430) restrict buffer MortonBuffer { 
    uint morton_buffer[];
};

layout(set = 0, binding = 1, std430) restrict buffer RadixTrieChildrenBuffer {
    uint radix_trie_indices_buffer[]; // parent, left, right
};

layout(push_constant, std430) uniform Parameters {
    uint particle_count;
} parameters;

int count_common_bits(int a, int b) {
    if (b < 0 || b >= parameters.particle_count) return -1;

    uint morton_a = morton_buffer[a];
    uint morton_b = morton_buffer[b];

    if (morton_a == morton_b) return 32 + 31 - findMSB(uint(a) ^ uint(b));
    return 31 - findMSB(morton_a ^ morton_b);
}

void main() {
    ivec2 bounds;
    
    bounds.x = int(gl_GlobalInvocationID.x);
    if (bounds.x >= int(parameters.particle_count) - 1) return;

    int missing_bounds_direction = count_common_bits(bounds.x, bounds.x + 1) > count_common_bits(bounds.x, bounds.x - 1) ? 1 : -1;
    int min_common_digits = count_common_bits(bounds.x, bounds.x - missing_bounds_direction);
    
    bounds.y = bounds.x;

    while (count_common_bits(bounds.x, bounds.y + missing_bounds_direction) > min_common_digits) bounds.y += missing_bounds_direction;

    int bounds_common_digits = count_common_bits(bounds.x, bounds.y);
    int middle = bounds.x;
    while (count_common_bits(bounds.x, middle + missing_bounds_direction) > bounds_common_digits) middle += missing_bounds_direction;

    if (missing_bounds_direction < 0) {
        middle--;
    }

    bool left_child_leaf = min(bounds.x, bounds.y) == middle;
    bool right_child_leaf = max(bounds.x, bounds.y) == middle+1;

    uint left_child = uint(middle) << 1 | (left_child_leaf ? 1u : 0u);
    uint right_child = uint(middle+1) << 1 | (right_child_leaf ? 1u : 0u);

    radix_trie_indices_buffer[3*bounds.x+1] = left_child;
    radix_trie_indices_buffer[3*bounds.x+2] = right_child;

    if (left_child_leaf) {
    }
    else {
        radix_trie_indices_buffer[3 * middle] = uint(bounds.x);
    }

    if (right_child_leaf) {
    }
    else {
        radix_trie_indices_buffer[3 * (middle+1)] = uint(bounds.x);
    }
}