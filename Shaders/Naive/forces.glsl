#[compute]
#version 450

layout(local_size_x = 256) in;

layout(set = 0, binding = 0, std430) restrict buffer PositionBuffer { 
    vec2 position_buffer[];
};

layout(set = 0, binding = 1, std430) restrict buffer VelocityBuffer { 
    vec2 velocity_buffer[];
};

layout(set = 0, binding = 2, std430) restrict buffer AccelerationBuffer {
    vec2 acceleration_buffer[];
};

layout(set = 0, binding = 3, std430) restrict buffer MassBuffer { 
    float mass_buffer[];
};

layout(push_constant, std430) uniform Parameters {
    float delta;
    float gravitational_constant;
    float epsilon_squared;
    uint particle_count;
} parameters;

void main() {
    uint affected_index = gl_GlobalInvocationID.x;

    if (affected_index >= parameters.particle_count) return;

    vec2 affected_position = position_buffer[affected_index];
    vec2 net_acceleration = vec2(0.0f);

    for (uint affecting_index = 0; affecting_index < parameters.particle_count; affecting_index++) {
        if (affected_index == affecting_index) continue;
    
        vec2 difference = position_buffer[affecting_index] - affected_position;
        float distance_squared = dot(difference, difference);
        float inverse_distance = inversesqrt(distance_squared + parameters.epsilon_squared);

        net_acceleration += difference * (mass_buffer[affecting_index] * inverse_distance * inverse_distance * inverse_distance * parameters.gravitational_constant);
    }

    acceleration_buffer[affected_index] = net_acceleration;
    velocity_buffer[affected_index] += 0.5f * net_acceleration * parameters.delta;
}