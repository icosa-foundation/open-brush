Settings = {
    description = "Crop existing strokes: press at the center, drag to set the radius or half-size, then release. Plane keeps the side toward the release point. Undo restores the crop.",
    space = "canvas",
    previewType = "sphere"
}

Parameters = {
    shape = {label="Shape", type="list", items={"Sphere", "Box", "Capsule", "Ellipsoid", "Plane"}, default="Sphere"},
    keepInside = {label="Keep inside (off keeps outside)", type="toggle", default=true}
}

function Main()
    local previews = {
        Sphere="sphere", Box="cube", Capsule="capsule",
        Ellipsoid="ellipsoid", Plane="plane"
    }
    Settings.previewType = previews[Parameters.shape]

    if not Brush.triggerReleasedThisFrame then return end

    -- Tool points are relative to the active layer; cropping uses scene positions.
    local layerTransform = Transform.identity
    local layers = Sketch.layers
    for i = 0, layers.count - 1 do
        if layers[i].active then
            layerTransform = layers[i].transform
            break
        end
    end

    local start = layerTransform * Tool.startPoint
    local finish = layerTransform * Tool.endPoint
    local center = start.position
    local drag = finish.position - center
    local radius = drag.magnitude
    if radius < 0.001 then return end

    local strokes = Sketch.strokes
    local rotation = finish.rotation
    local angles = Vector3:New(rotation.x, rotation.y, rotation.z)

    if Parameters.shape == "Sphere" then
        strokes:CropSphere(center, radius, Parameters.keepInside)
    elseif Parameters.shape == "Box" then
        strokes:CropBox(center, Vector3:New(radius * 2, radius * 2, radius * 2), angles, Parameters.keepInside)
    elseif Parameters.shape == "Capsule" then
        -- Full height includes the two rounded ends.
        strokes:CropCapsule(center, radius, radius * 4, angles, Parameters.keepInside)
    elseif Parameters.shape == "Ellipsoid" then
        strokes:CropEllipsoid(center, Vector3:New(radius * 2, radius * 4, radius * 2), angles, Parameters.keepInside)
    elseif Parameters.shape == "Plane" then
        local normal = drag / radius
        strokes:CropPlane(center, normal, Parameters.keepInside)
    end
end
