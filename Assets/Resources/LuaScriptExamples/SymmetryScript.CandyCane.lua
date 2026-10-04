Settings = {
    description = "Nested twisting strands",
    space = "canvas"
}

Parameters = {
    bundles = {label = "Large bundles", type = "int", min = 1, max = 16, default = 5},
    grossTurns = {label = "Large twist", type = "float", min = -1, max = 1, default = 0.1},
    fineTurns = {label = "Small twist", type = "float", min = -1, max = 1, default = -0.01},
    grossRadius = {label = "Large radius", type = "float", min = 0, max = 1, default = 0.425},
    fineRadius = {label = "Small radius", type = "float", min = 0, max = 1, default = 0.075},
    strandSize = {label = "Strand size", type = "float", min = 0.1, max = 2, default = 0.5}
}

local frames = require "parentBrushFrame"
local matrices = require "parentBrushMatrices"
local selectedBrush = nil
local selectedSize = nil
local root = frames.new()
local bundles = {}
local colors = {Color:New(1, 0.118, 0.118), Color:New(0.902, 0.784, 0.784),
                Color:New(0.078, 0.706, 0.078)}

function Start()
    selectedBrush = Brush.type
    selectedSize = Brush.sizeAbsolute
    Symmetry:SetBrushes({"Icing"})
end

local function twistedChild(pose, frame, distance, index, count, radius, turns, size)
    local angle = distance * turns * 360 / Math:Max(radius * size, 0.0001)
    local twist = Rotation:New(0, 0, angle)
    local phase = Rotation:New(0, 0, index * 360 / count)
    local offset = Rotation:RotateVector(phase, Vector3.right) *
        (radius * size * Brush.pressure)
    offset = Rotation:RotateVector(twist, offset)
    local action = Transform:New(offset, twist)
    return Symmetry:ApplyPoseAction(pose, frame, action)
end

function Main()
    if Brush.triggerPressedThisFrame or not Brush.triggerIsPressed then
        root = frames.new()
        bundles = {}
    end

    local pose = Symmetry.pointerPose
    local canvasSize = Brush.size * pose.scale
    local rootFrame = frames.update(root, pose.position, pose.rotation)
    local pointers = Path:New()
    local pointerColors = {}

    for bundle = 0, Parameters.bundles - 1 do
        local gross = twistedChild(pose, rootFrame, root.distance, bundle,
            Parameters.bundles, Parameters.grossRadius, Parameters.grossTurns, canvasSize)
        local bundleState = bundles[bundle]
        if bundleState == nil then
            bundleState = frames.new()
            bundles[bundle] = bundleState
        end
        local bundleFrame = frames.update(bundleState, gross.position, gross.rotation)
        for strand = 0, 2 do
            local fine = twistedChild(gross, bundleFrame, bundleState.distance, strand,
                3, Parameters.fineRadius, Parameters.fineTurns, canvasSize)
            fine.scale = pose.scale * Parameters.strandSize
            pointers:Insert(fine)
            pointerColors[#pointerColors + 1] = colors[strand + 1]
        end
    end

    Symmetry:SetColors(pointerColors)
    return matrices.fromPoses(pointers, pose)
end

function End()
    Symmetry:ClearBrushes()
    Symmetry:ClearColors()
    if selectedBrush ~= nil then Brush.type = selectedBrush end
    if selectedSize ~= nil then Brush.sizeAbsolute = selectedSize end
end
