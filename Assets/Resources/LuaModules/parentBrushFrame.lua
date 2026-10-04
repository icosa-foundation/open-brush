-- A transported frame for brush effects that follow a curved hand-drawn line.
local frame = {}

function frame.new()
    return {position = nil, tangent = nil, up = nil, rotation = nil, distance = 0}
end

function frame.update(state, position, pointerRotation)
    if state.position == nil then
        state.position = position
        state.rotation = pointerRotation
        state.up = Transform:New(Vector3.zero, pointerRotation).up
        return Transform:New(position, state.rotation)
    end

    local movement = position - state.position
    local length = movement.magnitude
    if length > 0.00001 then
        local tangent = movement / length
        if state.tangent ~= nil then
            local bend = Rotation:FromToRotation(state.tangent, tangent)
            state.up = Rotation:RotateVector(bend, state.up)
        end
        state.tangent = tangent
        state.rotation = Rotation:LookRotation(tangent, state.up)
        state.distance = state.distance + length
    end
    state.position = position
    return Transform:New(position, state.rotation)
end

return frame
