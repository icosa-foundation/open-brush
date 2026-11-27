Settings = {
    description = "Edits a primitive in the last-selected SDF guide. Pull the trigger to apply the parameters."
}

Parameters = {
    primitiveIndex = {label = "Primitive Index", type = "int", min = 0, max = 15, default = 0},
    primitiveType = {
        label = "Primitive Type",
        type = "list",
        items = {"sphere", "torus", "cuboid", "boxframe", "cylinder"},
        default = "torus"
    },
    operation = {
        label = "Operation",
        type = "list",
        items = {"union", "subtract", "intersect"},
        default = "union"
    },
    blend = {label = "Blend", type = "float", min = 0, max = 2, default = 0},
    geometryX = {label = "Geometry X", type = "float", min = 0, max = 5, default = 0.75},
    geometryY = {label = "Geometry Y", type = "float", min = 0, max = 5, default = 0.3},
    geometryZ = {label = "Geometry Z", type = "float", min = 0, max = 5, default = 0},
    geometryW = {label = "Geometry W", type = "float", min = 0, max = 5, default = 0},
    positionX = {label = "Local Position X", type = "float", min = -5, max = 5, default = 0},
    positionY = {label = "Local Position Y", type = "float", min = -5, max = 5, default = 0},
    positionZ = {label = "Local Position Z", type = "float", min = -5, max = 5, default = 0},
    rotationX = {label = "Local Rotation X", type = "float", min = -180, max = 180, default = 0},
    rotationY = {label = "Local Rotation Y", type = "float", min = -180, max = 180, default = 0},
    rotationZ = {label = "Local Rotation Z", type = "float", min = -180, max = 180, default = 0},
    scale = {label = "Local Scale", type = "float", min = 0.01, max = 5, default = 1}
}

-- Geometry values are interpreted by primitive type:
-- sphere: x = radius
-- torus: x = major radius, y = minor radius
-- cuboid: x/y/z = half-extents
-- boxframe: x/y/z = half-extents, w = frame thickness
-- cylinder: x = radius, y = half-height

function Main()
    if not Brush.triggerPressedThisFrame then
        return
    end

    local guide = Sketch.guides.lastSelected
    if guide == nil or not guide.isSdf then
        print("SDF Guide Editor: select an SDF guide before applying changes")
        return
    end

    if Parameters.primitiveIndex >= guide.primitiveCount then
        print("SDF Guide Editor: primitive index is outside this guide's primitive list")
        return
    end

    local primitive = guide:GetPrimitive(Parameters.primitiveIndex)
    local operation = Parameters.operation
    if primitive.componentIndex == 0 then
        operation = "union"
    end

    local geometry = Vector4:New(
        Parameters.geometryX,
        Parameters.geometryY,
        Parameters.geometryZ,
        Parameters.geometryW
    )
    local transform = Transform:New(
        Vector3:New(Parameters.positionX, Parameters.positionY, Parameters.positionZ),
        Rotation:New(Parameters.rotationX, Parameters.rotationY, Parameters.rotationZ),
        Parameters.scale
    )

    primitive:Update(
        Parameters.primitiveType,
        geometry,
        transform,
        operation,
        Parameters.blend
    )
end
