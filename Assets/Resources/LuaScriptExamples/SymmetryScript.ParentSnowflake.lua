Settings = {
    description = "Six-fold reflected strokes based on the old Snowflake parent brush",
    space = "world"
}

Parameters = {
    rotations = {label = "Rotations", type = "int", min = 1, max = 24, default = 6},
    saturationShift = {label = "Reflection saturation shift", type = "float", min = 0, max = 0.5, default = 0.1},
    hueShift = {label = "Rotation hue shift", type = "float", min = 0, max = 1, default = 0.2}
}

local origin = nil
local initialHsv = nil

function Start()
    Symmetry:SetBrushes({"Icing"})
end

function Main()
    local pose = Transform:New(Brush.position, Brush.rotation)
    if origin == nil or Brush.triggerPressedThisFrame or not Brush.triggerIsPressed then
        origin = pose
        initialHsv = Brush.colorHsv
    end

    local reflected = Symmetry:ReflectPose(pose, origin)
    local reflectedOrigin = Symmetry:ReflectPose(origin, origin)
    local pointers = Path:New()
    local colors = {}

    for side = 0, 1 do
        local sidePose = side == 0 and pose or reflected
        local sideOrigin = side == 0 and origin or reflectedOrigin
        local saturation = initialHsv.y
        if side == 1 then
            saturation = Math:Clamp01(saturation + Parameters.saturationShift *
                (saturation > 0.5 and -1 or 1))
        end
        for i = 0, Parameters.rotations - 1 do
            local wrapped = i + (i * 2 > Parameters.rotations and -Parameters.rotations or 0)
            local angle = wrapped * 360 / Parameters.rotations
            local action = Transform:New(Vector3.zero, Rotation:New(0, 0, angle))
            pointers:Insert(Symmetry:ApplyPoseAction(sidePose, sideOrigin, action))
            local hue = Math:Repeater(initialHsv.x + Parameters.hueShift * angle / 360, 1)
            colors[#colors + 1] = Color:HsvToRgb(hue, saturation, initialHsv.z)
        end
    end

    Symmetry:SetColors(colors)
    return pointers
end

function End()
    Symmetry:ClearBrushes()
    Symmetry:ClearColors()
end
