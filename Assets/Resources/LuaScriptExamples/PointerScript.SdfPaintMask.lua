Settings = {
    description = "Allows painting only in the chosen region of all active guides",
    space = "canvas"
}

Parameters = {
    region = {label = "Paint Region", type = "list", items = {"inside", "outside", "near surface"}, default = "inside"},
    surfaceWidth = {label = "Surface Width", type = "float", min = 0.001, max = 1, default = 0.05}
}

function Start()
    Brush:ForcePaintingOff(false)
    Brush:ForceGuideSnappingOff(true)
end

function Main()
    local blocked = false
    local guides = Sketch.guides

    if guides.count > 0 then
        local distance = guides:SignedDistance(Brush.position)
        if Parameters.region == "inside" then
            blocked = distance > 0
        elseif Parameters.region == "outside" then
            blocked = distance < 0
        else
            blocked = Math:Abs(distance) > Parameters.surfaceWidth
        end
    end

    Brush:ForcePaintingOff(blocked)
    return Transform:New(Brush.position, Brush.rotation)
end

function End()
    Brush:ForcePaintingOff(false)
    Brush:ForceGuideSnappingOff(false)
end
