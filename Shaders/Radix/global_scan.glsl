#[compute]
#version 450

layout(local_size_x = 256) in;

layout(set = 0, binding = 0, std430) restrict buffer HistogramBuffer {
    uint histogram_buffer[];
};

layout(set = 0, binding = 1, std430) restrict buffer ScanBuffer {
    uint scan_buffer[];
};

layout(push_constant, std430) uniform Parameters {
    uint base;
    uint histogram_groups;
} parameters;

void main() {
    uint local_id = gl_LocalInvocationID.x;
    
    if (local_id >= parameters.base) return;

    scan_buffer[local_id] = scan_buffer[local_id + parameters.base];
    scan_buffer[local_id + parameters.base] = 0;
 
    for (uint i = 1; i < parameters.histogram_groups; i++) {
        uint current_index = local_id + i*parameters.base;

        scan_buffer[local_id] += scan_buffer[current_index + parameters.base];
        scan_buffer[current_index + parameters.base] = scan_buffer[current_index] + histogram_buffer[current_index-parameters.base];
    }
}