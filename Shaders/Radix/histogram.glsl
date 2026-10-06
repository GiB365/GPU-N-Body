#[compute]
#version 450
#define MAX_BASE 256
#define THREADS 256

layout(local_size_x = THREADS) in;

layout(set = 0, binding = 0, std430) restrict buffer MortonBufferA { 
    uint morton_bufferA[];
};

layout(set = 0, binding = 1, std430) restrict buffer MortonBufferB { 
    uint morton_bufferB[];
};

layout(set = 0, binding = 2, std430) restrict buffer RadixIndexBufferA {
    uint radix_indexA[];
};

layout(set = 0, binding = 3, std430) restrict buffer RadixIndexBufferB {
    uint radix_indexB[];
};

layout(set = 0, binding = 4, std430) restrict buffer HistogramBuffer { 
    uint histogram_buffer[];
};

layout(push_constant, std430) uniform Parameters {
    uint base;
    uint keys_per_thread;
    uint particle_count;
    uint shift;
    uint indices_stored_in_B;
    uint properties_stored_in_B;
} parameters;

shared uint local_histogram[MAX_BASE];

uint exctract_bits(uint i) {
    return uint((i >> parameters.shift) & (parameters.base - 1));
}

void main() {
    uint local_id = gl_LocalInvocationID.x;
    uint workgroup_id = gl_WorkGroupID.x;
    
    if (local_id < parameters.base) {
        local_histogram[local_id] = 0;
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

            atomicAdd(local_histogram[digit], 1);
        }
    }

    barrier();

    if (local_id < parameters.base) {
        uint index = local_id + workgroup_id*parameters.base;
        histogram_buffer[index] = local_histogram[local_id];
    }
}