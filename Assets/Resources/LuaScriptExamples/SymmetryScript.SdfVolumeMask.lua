Settings = {
    description = "Keeps only symmetry copies that fall in the chosen region of the last-selected guide",
    space = "canvas"
}

Parameters = {
    copies = {label = "Candidate Copies", type = "int", min = 1, max = 96, default = 24},
    radius = {label = "Ring Radius", type = "float", min = 0, max = 5, default = 1},
    region = {
        label = "Keep Region",
        type = "list",
        items = {"inside", "outside", "near surface"},
        default = "inside"
    },
    surfaceWidth = {label = "Surface Width", type = "float", min = 0.001, max = 1, default = 0.05}
}

local function shouldKeep(distance)
    if Parameters.region == "inside" then
        return distance <= 0
    elseif Parameters.region == "outside" then
        return distance >= 0
    end
    return Math:Abs(distance) <= Parameters.surfaceWidth
end

function Main()
    local guide = Sketch.guides.lastSelected
    if guide == nil then
        Symmetry:SetPointerPaintMode(0, SymmetryPointerPaintMode.Inherit)
        return MatrixList:New(1)
    end

    local pointers = MatrixList:New(Parameters.copies)
    for i = 0, Parameters.copies - 1 do
        local angle = Math.pi * 2 * i / Parameters.copies
        local position = Brush.position + Vector3:New(
            Math:Cos(angle) * Parameters.radius,
            0,
            Math:Sin(angle) * Parameters.radius
        )
        -- Keep one slot per candidate so crossing the boundary cannot reconnect
        -- another candidate's stroke. Positions and the mask update every frame.
        local paintMode = SymmetryPointerPaintMode.ForcedOff
        if shouldKeep(guide:SignedDistance(position)) then
            paintMode = SymmetryPointerPaintMode.Inherit
        end
        Symmetry:SetPointerPaintMode(i, paintMode)
        pointers[i] = Matrix:NewTranslation(position - Brush.position)
    end

    return pointers
end
