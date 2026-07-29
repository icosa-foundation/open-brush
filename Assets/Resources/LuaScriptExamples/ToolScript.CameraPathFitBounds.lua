Settings = {
    description = "Draws the active camera path fitted inside a sphere or box you define by dragging",
    space = "canvas",
    previewType = "sphere"
}

Parameters = {
    bounds = {
        label = "Bounds",
        type = "list",
        items = {"Sphere", "Box"},
        default = "Sphere"
    },
    sampleStep = {label = "Sample Step", type = "float", min = 0.02, max = 1, default = 0.1},
    keepAspect = {label = "Keep Aspect", type = "toggle", default = true},
    minSize = {label = "Min Size", type = "float", min = 0.01, max = 1, default = 0.05}
}

function getActiveCameraPath()
    if Sketch.cameraPaths.count == 0 then
        return nil
    end

    local cameraPath = Sketch.cameraPaths.active
    if cameraPath == nil then
        cameraPath = Sketch.cameraPaths.last
    end
    return cameraPath
end

function absVector(v)
    return Vector3:New(Math:Abs(v.x), Math:Abs(v.y), Math:Abs(v.z))
end

function clampSize(value)
    return Math:Max(value, Parameters.minSize)
end

function Main()
    if Brush.triggerPressedThisFrame then
        startPoint = Brush.position
    end

    if Brush.triggerReleasedThisFrame then
        local cameraPath = getActiveCameraPath()
        if cameraPath == nil then
            return nil
        end

        local drag = Brush.position - startPoint
        local path = cameraPath:AsPath(Parameters.sampleStep)

        if Parameters.bounds == "Sphere" then
            local radius = clampSize(drag.magnitude)
            path:FitInsideSphere(startPoint, radius)
        else
            local halfSize = absVector(drag)
            local size = Vector3:New(
                clampSize(halfSize.x * 2),
                clampSize(halfSize.y * 2),
                clampSize(halfSize.z * 2)
            )
            path:FitInside(Bounds:New(startPoint, size), Parameters.keepAspect)
        end

        -- Draw manually so the example uses the current brush settings and the new CameraPath APIs.
        return path:Draw()
    end
end
