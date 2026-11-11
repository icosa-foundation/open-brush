Settings = {
    description = "Snaps the brush to the surface of the last-selected guide",
    space = "canvas"
}

Parameters = {
    offset = {label = "Surface Offset", type = "float", min = -1, max = 1, default = 0},
    orientation = {
        label = "Orientation",
        type = "list",
        items = {"surface", "brush"},
        default = "surface"
    }
}

function Main()
    Brush:ForcePaintingOff(Brush.triggerPressedThisFrame)

    local guide = Sketch.guides.lastSelected
    if guide == nil then
        return Transform:New(Brush.position, Brush.rotation)
    end

    local closest = guide:ClosestPoint(Brush.position)
    local rotation = closest.rotation
    if Parameters.orientation == "brush" then
        rotation = Brush.rotation
    end

    return Transform:New(
        closest.position + closest.up * Parameters.offset,
        rotation
    )
end

function End()
    Brush:ForcePaintingOff(false)
end
