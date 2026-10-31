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
    if guide == nil then
        return MatrixList:New(1)
    end

    local pointers = MatrixList:New(Parameters.copies)
    local brushInverse = Matrix:NewTRS(Brush.position, Brush.rotation, Vector3.one).inverse
    for i = 0, Parameters.copies - 1 do
        local angle = Math.pi * 2 * i / Parameters.copies
        local candidate = Brush.position + Vector3:New(
            Math:Cos(angle) * Parameters.radius,
            0,
            Math:Sin(angle) * Parameters.radius
        )
        local closest = guide:ClosestPoint(candidate)
        local target = Matrix:NewTRS(
            closest.position + closest.up * Parameters.surfaceOffset,
            closest.rotation,
            Vector3.one
        )
        pointers[i] = target * brushInverse
    end

    return pointers
end
