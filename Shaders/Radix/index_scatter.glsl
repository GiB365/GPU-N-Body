#[compute]
#version 450
#define THREADS 256
#define MAX_BASE 256

layout(local_size_x = THREADS) in;

layout(set = 0, binding = 0, std430) restrict buffer MortonBufferA {
    uint morton_bufferA[];
};

layout(set = 0, binding = 1, std430) restrict buffer MortonBufferB {
    uint morton_bufferB[];
};

layout(set = 0, binding = 2, std430) restrict buffer RadixIndexA {
    uint radix_indexA[];
};

layout(set = 0, binding = 3, std430) restrict buffer RadixIndexB {
    uint radix_indexB[];
};

layout(set = 0, binding = 4, std430) restrict buffer ScanBuffer {
    uint scan_buffer[];
};

layout(set = 0, binding = 5, std430) restrict buffer ThreadHistogram {
    uint thread_histogram[];
};

layout(push_constant, std430) uniform Parameters {
    uint base;
    uint particle_count;
    uint keys_per_thread;
    uint shift;
    uint groups;
    uint indices_stored_in_B;
    uint properties_stored_in_B;
} parameters;

uint exctract_bits(uint i) {
    return uint((i >> parameters.shift) & (parameters.base - 1));
}

void main() {
    uint local_id = gl_LocalInvocationID.x;
    uint workgroup_id = gl_WorkGroupID.x;
    uint local_rank[MAX_BASE];

    for (int i = 0; i < parameters.base; i++) {
        thread_histogram[i + parameters.base*local_id + workgroup_id*THREADS*parameters.base] = 0;
    }

    barrier();

    for (int key = 0; key < parameters.keys_per_thread; key++) {
        uint current_index = local_id*parameters.keys_per_thread + key + workgroup_id*THREADS*parameters.keys_per_thread;

        if (current_index < parameters.particle_count) {
            if (parameters.indices_stored_in_B == 1) {
                current_index = radix_indexB[current_index];
            }
            else {
                current_index = radix_indexA[current_index];
            }

            uint digit;
            
            if (parameters.properties_stored_in_B == 1) {
                digit = exctract_bits(morton_bufferB[current_index]);
            }
            else {
                digit = exctract_bits(morton_bufferA[current_index]);
            }

            thread_histogram[digit + local_id*parameters.base + workgroup_id*THREADS*parameters.base]++;
        }
    }

    barrier();

    uint previous_value = thread_histogram[local_id + workgroup_id*THREADS*parameters.base];
    thread_histogram[local_id + workgroup_id*THREADS*parameters.base] = 0;

    for (int i = 1; i < THREADS; i++) {
        uint current_index = local_id + i*parameters.base + workgroup_id*THREADS*parameters.base;
        uint current_value = thread_histogram[current_index];
        thread_histogram[current_index] = thread_histogram[current_index-parameters.base] + previous_value;
        previous_value = current_value;
    }

    barrier();

    for (int i = 0; i < parameters.base; i++) {
        local_rank[i] = 0;
    }

    for (int key = 0; key < parameters.keys_per_thread; key++) {
        uint current_index = local_id*parameters.keys_per_thread + key + workgroup_id*THREADS*parameters.keys_per_thread;

        if (current_index < parameters.particle_count) {
            uint digit;

            if (parameters.indices_stored_in_B == 1) {
                current_index = radix_indexB[current_index];
            }
            else {
                current_index = radix_indexA[current_index];
            }
            
            if (parameters.properties_stored_in_B == 1) {
                digit = exctract_bits(morton_bufferB[current_index]);
            }
            else {
                digit = exctract_bits(morton_bufferA[current_index]);
            }

            uint destination = scan_buffer[digit] + scan_buffer[digit + (workgroup_id+1) * parameters.base] + thread_histogram[digit + local_id*parameters.base + workgroup_id*THREADS*parameters.base] + local_rank[digit];
            local_rank[digit]++;

            if (parameters.indices_stored_in_B == 1) {
                radix_indexA[destination] = current_index;
            }
            else {
                radix_indexB[destination] = current_index;
            }
        }
    }
}