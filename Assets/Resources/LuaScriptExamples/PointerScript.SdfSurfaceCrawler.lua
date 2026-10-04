Settings = {
    description = "Moves the brush along the surface of the last-selected guide",
    space = "canvas"
}

Parameters = {
    stepDistance = {label = "Step Per Frame", type = "float", min = 0.001, max = 0.25, default = 0.02},
    surfaceOffset = {label = "Surface Offset", type = "float", min = -0.5, max = 0.5, default = 0}
}

local currentPoint = nil

function Main()
    local guide = Sketch.guides.lastSelected
    if guide == nil then
        currentPoint = nil
        Brush:ForcePaintingOff(false)
        return Transform:New(Brush.position, Brush.rotation)
    end

    if currentPoint == nil or Brush.triggerPressedThisFrame or not Brush.triggerIsPressed then
        currentPoint = guide:ClosestPoint(Brush.position).position
    else
        local surface = guide:ClosestPoint(currentPoint)
        local tangent = Brush.direction:ProjectOnPlane(surface.up)
        if tangent.sqrMagnitude > 0.000001 then
            currentPoint = guide:NextPointOnSurface(
                currentPoint,
                Parameters.stepDistance,
                tangent.normalized
            )
        end
    end

    Brush:ForcePaintingOff(Brush.triggerPressedThisFrame)
    local closest = guide:ClosestPoint(currentPoint)
    return Transform:New(
        closest.position + closest.up * Parameters.surfaceOffset,
        closest.rotation
    )
end

function End()
    currentPoint = nil
    Brush:ForcePaintingOff(false)
end
