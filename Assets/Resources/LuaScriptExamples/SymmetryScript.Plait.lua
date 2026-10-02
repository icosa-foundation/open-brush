Settings = {
    description = "Braided strands following the drawn line",
    space = "canvas"
}

Parameters = {
    strands = {label = "Strands", type = "int", min = 3, max = 9, default = 5},
    recursion = {label = "Nested plaits", type = "int", min = 0, max = 2, default = 0},
    cycles = {label = "Cycles per unit size", type = "float", min = 0.1, max = 12, default = 3},
    rotations = {label = "Rotations per cycle", type = "float", min = -3, max = 3, default = 0},
    strandSize = {label = "Strand size", type = "float", min = 0.25, max = 4, default = 1.2}
}

local frames = require "parentBrushFrame"
local matrices = require "parentBrushMatrices"
local states = {}
local selectedBrush = nil
local selectedSize = nil
local colors = {Color:New(1, 0.118, 0.118), Color:New(0.902, 0.784, 0.784),
                Color:New(0.078, 0.706, 0.078)}

function Start()
    selectedBrush = Brush.type
    selectedSize = Brush.sizeAbsolute
    Symmetry:SetBrushes({"Icing"})
end

local function strandPose(pose, frame, distance, strand, count, size, canvasSize)
    local cycle = Parameters.cycles * distance * 0.1 /
        Math:Max(canvasSize * size, 0.0001)
    local t = cycle + strand / count
    local x = Math:Sin(2 * Math.pi * t)
    local yFrequency = count % 2 == 0 and 1.5 or 2
    local y = Math:Sin(2 * Math.pi * t * yFrequency)
    local amplitude = canvasSize * size * Brush.pressure / 2
    local action = Transform:New(Vector3:New(x * amplitude, y * amplitude, 0),
        Rotation:New(0, 0, cycle * Parameters.rotations * 360))
    return Symmetry:ApplyPoseAction(pose, frame, action)
end

local function addLevel(pose, key, level, size, canvasSize, rootScale,
                        pointers, pointerColors)
    local state = states[key]
    if state == nil then
        state = frames.new()
        states[key] = state
    end
    local frame = frames.update(state, pose.position, pose.rotation)
    local count = level < Parameters.recursion and
        3 + Parameters.recursion - level or Parameters.strands

    for strand = 0, count - 1 do
        local child = strandPose(pose, frame, state.distance, strand, count, size, canvasSize)
        local childSize = level < Parameters.recursion and
            0.7 * Parameters.strandSize / count or Parameters.strandSize / count
        if level < Parameters.recursion then
            child.scale = rootScale
            addLevel(child, key .. "." .. strand, level + 1,
                size * childSize, canvasSize, rootScale, pointers, pointerColors)
        else
            child.scale = rootScale * size * childSize
            pointers:Insert(child)
            pointerColors[#pointerColors + 1] = colors[strand % #colors + 1]
        end
    end
end

function Main()
    if Brush.triggerPressedThisFrame or not Brush.triggerIsPressed then
        states = {}
    end
    local pointers = Path:New()
    local pointerColors = {}
    local pose = Symmetry.pointerPose
    addLevel(pose, "root", 0, 1, Brush.size * pose.scale, pose.scale,
        pointers, pointerColors)
    Symmetry:SetColors(pointerColors)
    return matrices.fromPoses(pointers, pose)
end

function End()
    Symmetry:ClearBrushes()
    Symmetry:ClearColors()
    if selectedBrush ~= nil then Brush.type = selectedBrush end
    if selectedSize ~= nil then Brush.sizeAbsolute = selectedSize end
end
