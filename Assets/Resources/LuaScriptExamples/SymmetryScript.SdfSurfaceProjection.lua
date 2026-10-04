Settings = {
    description = "Projects a ring of symmetry pointers onto the last-selected guide",
    space = "canvas"
}

Parameters = {
    copies = {label = "Copies", type = "int", min = 1, max = 64, default = 12},
    radius = {label = "Ring Radius", type = "float", min = 0, max = 3, default = 0.5},
    surfaceOffset = {label = "Surface Offset", type = "float", min = -0.5, max = 0.5, default = 0}
}

function Main()
    local guide = Sketch.guides.lastSelected
    local pointers = Path:New()

    if guide == nil then
        pointers:Insert(Transform:New(Brush.position, Brush.rotation))
        return pointers
    end

    for i = 0, Parameters.copies - 1 do
        local angle = Math.pi * 2 * i / Parameters.copies
        local candidate = Brush.position + Vector3:New(
            Math:Cos(angle) * Parameters.radius,
            0,
            Math:Sin(angle) * Parameters.radius
        )
        local closest = guide:ClosestPoint(candidate)
        pointers:Insert(Transform:New(
            closest.position + closest.up * Parameters.surfaceOffset,
            closest.rotation
        ))
    end

    return pointers
end
