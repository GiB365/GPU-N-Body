#[compute]
#version 450
#define THREADS 256
#define MAX_BASE 256

layout(local_size_x = 256) in;

layout(set = 0, binding = 0, std430) restrict buffer PositionBufferA {
    vec2 position_bufferA[];
};

layout(set = 0, binding = 1, std430) restrict buffer PositionBufferB {
    vec2 position_bufferB[];
};

layout(set = 0, binding = 2, std430) restrict buffer VelocityBufferA {
    vec2 velocity_bufferA[];
};

layout(set = 0, binding = 3, std430) restrict buffer VelocityBufferB {
    vec2 velocity_bufferB[];
};

layout(set = 0, binding = 4, std430) restrict buffer AccelerationBufferA {
    vec2 acceleration_bufferA[];
};

layout(set = 0, binding = 5, std430) restrict buffer AccelerationBufferB {
    vec2 acceleration_bufferB[];
};

layout(set = 0, binding = 6, std430) restrict buffer MassBufferA {
    float mass_bufferA[];
};

layout(set = 0, binding = 7, std430) restrict buffer MassBufferB {
    float mass_bufferB[];
};

layout(set = 0, binding = 8, std430) restrict buffer ColorBufferA {
    vec4 color_bufferA[];
};

layout(set = 0, binding = 9, std430) restrict buffer ColorBufferB {
    vec4 color_bufferB[];
};

layout(set = 0, binding = 10, std430) restrict buffer MortonBufferA {
    uint morton_bufferA[];
};

layout(set = 0, binding = 11, std430) restrict buffer MortonBufferB {
    uint morton_bufferB[];
};

layout(set = 0, binding = 12, std430) restrict buffer RadixIndexA {
    uint radix_indexA[];
};

layout(set = 0, binding = 13, std430) restrict buffer RadixIndexB {
    uint radix_indexB[];
};

layout(push_constant, std430) uniform Parameters {
    uint particle_count;
    uint keys_per_thread;
    uint properties_stored_in_B;
    uint indices_stored_in_B;
} parameters;

void main() {
    uint local_id = gl_LocalInvocationID.x;
    uint workgroup_id = gl_WorkGroupID.x;
    uint local_rank[MAX_BASE];

    for (int key = 0; key < parameters.keys_per_thread; key++) {
        uint current_index = local_id*parameters.keys_per_thread + key + workgroup_id*THREADS*parameters.keys_per_thread;

        if (current_index < parameters.particle_count) {
            uint source;

            if (parameters.indices_stored_in_B == 1) {
                source = radix_indexB[current_index];
            }
            else {
                source = radix_indexA[current_index];
            }

            if (parameters.properties_stored_in_B == 1) {
                position_bufferA[current_index] = position_bufferB[source];
                velocity_bufferA[current_index] = velocity_bufferB[source];
                acceleration_bufferA[current_index] = acceleration_bufferB[source];
                mass_bufferA[current_index] = mass_bufferB[source];
                color_bufferA[current_index] = color_bufferB[source];
                morton_bufferA[current_index] = morton_bufferB[source];
            }
            else {
                position_bufferB[current_index] = position_bufferA[source];
                velocity_bufferB[current_index] = velocity_bufferA[source];
                acceleration_bufferB[current_index] = acceleration_bufferA[source];
                mass_bufferB[current_index] = mass_bufferA[source];
                color_bufferB[current_index] = color_bufferA[source];
                morton_bufferB[current_index] = morton_bufferA[source];
            }
        }
    }

    barrier();

    for (int key = 0; key < parameters.keys_per_thread; key++) {
            uint current_index = local_id*parameters.keys_per_thread + key + workgroup_id*THREADS*parameters.keys_per_thread;

            if (current_index < parameters.particle_count) {
                radix_indexA[current_index] = current_index;
                radix_indexB[current_index] = current_index;
            }
    }

}