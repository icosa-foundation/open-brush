Settings = {
    description = "Crop existing strokes: press at the center, drag to set the radius or half-size, then release. Plane keeps the side toward the release point. Cropping cannot currently be undone.",
    space = "canvas",
    previewType = "sphere"
}

Parameters = {
    shape = {label="Shape", type="list", items={"Sphere", "Box", "Capsule", "Ellipsoid", "Plane"}, default="Sphere"},
    reversePlane = {label="Keep other side of plane", type="toggle", default=false}
}

function Main()
    -- The built-in preview cannot show a stretched sphere or an infinite plane.
    -- Disable it for those shapes rather than show a different crop boundary.
    local previews = {Sphere="sphere", Box="cube", Capsule="capsule"}
    Settings.previewType = previews[Parameters.shape] or "none"

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
        strokes:CropSphere(center, radius)
    elseif Parameters.shape == "Box" then
        strokes:CropBox(center, Vector3:New(radius * 2, radius * 2, radius * 2), angles)
    elseif Parameters.shape == "Capsule" then
        -- Full height includes the two rounded ends.
        strokes:CropCapsule(center, radius, radius * 4, angles)
    elseif Parameters.shape == "Ellipsoid" then
        strokes:CropEllipsoid(center, Vector3:New(radius * 2, radius * 4, radius * 2), angles)
    elseif Parameters.shape == "Plane" then
        local normal = drag / radius
        if Parameters.reversePlane then normal = -normal end
        strokes:CropPlane(center, normal)
    end
end
