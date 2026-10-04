Settings = {
    description = "Adds a primitive stamp to the last-selected SDF guide at the brush position",
    space = "canvas"
}

Parameters = {
    primitiveType = {
        label = "Primitive Type",
        type = "list",
        items = {"sphere", "torus", "cuboid", "boxframe", "cylinder"},
        default = "sphere"
    },
    operation = {
        label = "Operation",
        type = "list",
        items = {"union", "subtract", "intersect"},
        default = "union"
    },
    blend = {label = "Blend", type = "float", min = 0, max = 1, default = 0.05},
    geometryX = {label = "Geometry X", type = "float", min = 0.001, max = 2, default = 0.25},
    geometryY = {label = "Geometry Y", type = "float", min = 0, max = 2, default = 0.1},
    geometryZ = {label = "Geometry Z", type = "float", min = 0, max = 2, default = 0.25},
    geometryW = {label = "Geometry W", type = "float", min = 0, max = 2, default = 0.05},
    stampScale = {label = "Stamp Scale", type = "float", min = 0.01, max = 4, default = 1}
}

function Main()
    if not Brush.triggerPressedThisFrame then
        return
    end

    local guide = Sketch.guides.lastSelected
    if guide == nil or guide.guideType ~= "SDF" then
        print("SDF Sculpt: select an SDF guide before adding a stamp")
        return
    end

    local operation = Parameters.operation
    if guide.componentCount == 0 then
        operation = "union"
    end

    local canvasTransform = Transform:New(
        Brush.position,
        Brush.rotation,
        Parameters.stampScale
    )
    local localTransform = guide:ToLocalTransform(canvasTransform)
    guide:AddPrimitive(
        Parameters.primitiveType,
        Vector4:New(
            Parameters.geometryX,
            Parameters.geometryY,
            Parameters.geometryZ,
            Parameters.geometryW
        ),
        localTransform,
        operation,
        Parameters.blend
    )
end
