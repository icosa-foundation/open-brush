Settings = {
    description = "Draws a path that walks across the last-selected guide surface",
    previewType = "line",
    space = "canvas"
}

Parameters = {
    points = {label = "Points", type = "int", min = 2, max = 500, default = 100},
    stepDistance = {label = "Step Distance", type = "float", min = 0.001, max = 0.25, default = 0.03},
    surfaceOffset = {label = "Surface Offset", type = "float", min = -0.5, max = 0.5, default = 0}
}

function Main()
    if not Brush.triggerReleasedThisFrame then
        return
    end

    local guide = Sketch.guides.lastSelected
    if guide == nil then
        print("SDF Surface Path: select a guide before drawing the path")
        return
    end

    local path = Path:New()
    local point = guide:ClosestPoint(Brush.position).position
    local direction = Brush.direction

    for i = 1, Parameters.points do
        local closest = guide:ClosestPoint(point)
        path:Insert(Transform:New(
            closest.position + closest.up * Parameters.surfaceOffset,
            closest.rotation
        ))

        local tangent = direction:ProjectOnPlane(closest.up)
        if tangent.sqrMagnitude <= 0.000001 then
            tangent = closest.forward
        end
        direction = tangent.normalized
        point = guide:NextPointOnSurface(
            point,
            Parameters.stepDistance,
            direction
        )
    end

    return path
end
