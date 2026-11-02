Settings = {
    description = "Growing trunk, branches, fronds and lights",
    space = "canvas"
}

Parameters = {
    branches = {label = "Maximum branches", type = "int", min = 1, max = 12, default = 12},
    branchSpacing = {label = "Branch spacing", type = "float", min = 0.05, max = 2, default = 0.3},
    frondSpacing = {label = "Frond spacing", type = "float", min = 0.05, max = 2, default = 0.4},
    branchGrowth = {label = "Branch growth", type = "float", min = 0.1, max = 2, default = 0.7},
    frondGrowth = {label = "Frond growth", type = "float", min = 0.1, max = 2, default = 0.7}
}

local frames = require "parentBrushFrame"
local matrices = require "parentBrushMatrices"
local root = frames.new()
local branches = {}
local branchCount = 0
local lastBranchDistance = 0
local configuredBranches = -1
local selectedBrush = nil
local selectedSize = nil

local trunkColor = Color:New(0.272, 0.175, 0.03)
local branchColor = Color:New(0.691, 0.403, 0.142)
local frondColor = Color:New(0.036, 0.537, 0.205)

local function reset()
    root = frames.new()
    branches = {}
    branchCount = 0
    lastBranchDistance = 0
end

local function emit(pointers, modes, pose, active)
    pointers:Insert(pose)
    modes[#modes + 1] = active and SymmetryPointerPaintMode.Inherit or
        SymmetryPointerPaintMode.ForcedOff
end

local function configurePointers()
    if configuredBranches == Parameters.branches then return end
    local brushes = {"Icing"}
    local colors = {trunkColor}
    for branch = 1, Parameters.branches do
        brushes[#brushes + 1] = "Icing"
        colors[#colors + 1] = branchColor
        for event = 1, 4 do
            for side = 1, 2 do
                brushes[#brushes + 1] = "Icing"
                colors[#colors + 1] = frondColor
                brushes[#brushes + 1] = "LightWire"
                colors[#colors + 1] = Color.white
            end
        end
    end
    Symmetry:SetBrushes(brushes)
    Symmetry:SetColors(colors)
    configuredBranches = Parameters.branches
end

function Start()
    selectedBrush = Brush.type
    selectedSize = Brush.sizeAbsolute
    configuredBranches = -1
end

function Main()
    if Brush.triggerPressedThisFrame or not Brush.triggerIsPressed then
        reset()
    end
    configurePointers()

    local pose = Symmetry.pointerPose
    local canvasSize = Brush.size * pose.scale
    local rootFrame = frames.update(root, pose.position, pose.rotation)
    if Brush.triggerIsPressed and branchCount < Parameters.branches and
        root.distance - lastBranchDistance >= Parameters.branchSpacing then
        branchCount = branchCount + 1
        lastBranchDistance = root.distance
        branches[branchCount] = {
            anchor = rootFrame, frame = frames.new(), events = 0,
            lastFrondDistance = 0, fronds = {}
        }
    end

    local pointers = Path:New()
    local modes = {}
    emit(pointers, modes, pose, true)

    for index = 1, Parameters.branches do
        local branch = branches[index]
        local branchPose = pose
        local branchFrame = rootFrame
        if branch ~= nil then
            local growth = (Parameters.branches + 1 - index) / Parameters.branches
            local rotation = Rotation:New(0, 0, index * 137.5)
            rotation = rotation:Multiply(Rotation:New(120, 0, 0))
            local action = Transform:New(Vector3.zero, rotation,
                Parameters.branchGrowth * growth)
            branchPose = Symmetry:ApplyPoseAction(pose, branch.anchor, action)
            branchPose.scale = pose.scale
            branchFrame = frames.update(branch.frame, branchPose.position,
                branchPose.rotation)
            if branch.events < 4 and branch.frame.distance -
                branch.lastFrondDistance >= Parameters.frondSpacing then
                branch.events = branch.events + 1
                branch.lastFrondDistance = branch.frame.distance
                branch.fronds[branch.events] = branchFrame
            end
        end
        emit(pointers, modes,
            Transform:New(branchPose.position, branchPose.rotation, pose.scale * 0.4),
            branch ~= nil)

        for event = 1, 4 do
            local active = branch ~= nil and branch.fronds[event] ~= nil
            for side = 1, 2 do
                local frondPose = branchPose
                local lightPose = branchPose
                if active then
                    local angle = side == 1 and -30 or 30
                    local action = Transform:New(Vector3.zero,
                        Rotation:New(0, angle, 0), Parameters.frondGrowth)
                    frondPose = Symmetry:ApplyPoseAction(branchPose,
                        branch.fronds[event], action)
                    frondPose.scale = pose.scale * 0.16

                    local twist = branch.frame.distance * 1800 * 0.1
                    local rotation = Rotation:New(0, 0, twist)
                    local offset = Rotation:RotateVector(rotation,
                        Vector3:New(canvasSize * 0.1, 0, 0))
                    lightPose = Symmetry:ApplyPoseAction(frondPose, branch.fronds[event],
                        Transform:New(offset, rotation))
                    lightPose.scale = pose.scale * 0.16
                end
                emit(pointers, modes, frondPose, active)
                emit(pointers, modes, lightPose, active)
            end
        end
    end

    Symmetry:SetPointerPaintModes(modes)
    return matrices.fromPoses(pointers, pose)
end

function End()
    Symmetry:SetPointerPaintModes({})
    Symmetry:ClearBrushes()
    Symmetry:ClearColors()
    if selectedBrush ~= nil then Brush.type = selectedBrush end
    if selectedSize ~= nil then Brush.sizeAbsolute = selectedSize end
end
