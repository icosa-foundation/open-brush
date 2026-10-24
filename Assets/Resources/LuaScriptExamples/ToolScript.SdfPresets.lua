Settings = {
    description = "Creates a new editable SDF guide from a procedural preset",
    space = "canvas"
}

Parameters = {
    preset = {
        label = "Preset",
        type = "list",
        items = {"hollow sphere", "pipe", "rounded cross", "torus cutout"},
        default = "hollow sphere"
    },
    guideScale = {label = "Guide Scale", type = "float", min = 0.05, max = 5, default = 1},
    blend = {label = "Blend", type = "float", min = 0, max = 0.5, default = 0.08}
}

local function add(guide, primitiveType, geometry, transform, operation, blend)
    guide:AddPrimitive(primitiveType, geometry, transform, operation, blend)
end

local function buildHollowSphere(guide)
    add(guide, "sphere", Vector4:New(1, 0, 0, 0), Transform.identity, "union", 0)
    add(guide, "sphere", Vector4:New(0.72, 0, 0, 0), Transform.identity, "subtract", Parameters.blend)
end

local function buildPipe(guide)
    add(guide, "cylinder", Vector4:New(0.8, 1, 0, 0), Transform.identity, "union", 0)
    add(guide, "cylinder", Vector4:New(0.48, 1.2, 0, 0), Transform.identity, "subtract", Parameters.blend)
end

local function buildRoundedCross(guide)
    local geometry = Vector4:New(1, 0.28, 0.28, 0)
    add(guide, "cuboid", geometry, Transform.identity, "union", 0)
    add(guide, "cuboid", geometry, Transform:Rotation(0, 0, 90), "union", Parameters.blend)
    add(guide, "cuboid", geometry, Transform:Rotation(0, 90, 0), "union", Parameters.blend)
end

local function buildTorusCutout(guide)
    add(guide, "torus", Vector4:New(0.72, 0.28, 0, 0), Transform.identity, "union", 0)
    add(
        guide,
        "sphere",
        Vector4:New(0.42, 0, 0, 0),
        Transform:Position(0.72, 0, 0),
        "subtract",
        Parameters.blend
    )
end

function Main()
    if not Brush.triggerPressedThisFrame then
        return
    end

    local guide = Guide:NewCustomSDF(
        Transform:New(Brush.position, Brush.rotation, Parameters.guideScale)
    )

    if Parameters.preset == "hollow sphere" then
        buildHollowSphere(guide)
    elseif Parameters.preset == "pipe" then
        buildPipe(guide)
    elseif Parameters.preset == "rounded cross" then
        buildRoundedCross(guide)
    else
        buildTorusCutout(guide)
    end

    guide:Select()
end
