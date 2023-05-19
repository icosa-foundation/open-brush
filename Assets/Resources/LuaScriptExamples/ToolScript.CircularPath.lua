Settings = {
    description="Draws a circular camera path",
    previewType="quad", previewAxis="y"
}

Parameters = {
    sides = {label="Sides", type="float", min=3, max=16, default=4},
}

function OnTriggerReleased()

    -- Create the camera path in one go by passing in a list of transforms
    path = Path:New()
    radius = Tool.vector.magnitude
    angle = 360.0 / sides
    for a = 0, 360 - angle, angle do
        position = Vector2:PointOnCircle(a):Multiply(radius):OnY()
        rotation = Rotation:New(0, -a, 0)
        path.Insert(Transform:New(position, rotation, ((Math.pi * 0.5) / sides) * radius))
    end
    path:Transform(TransformBy:New(Tool.startPosition, Tool.rotation));
    CameraPath:FromPath(path, true)

    -- Create the camera path knot by knot
    radius = Tool.vector.magnitude
    cameraPath = CameraPath:New()
    angle = 30
    for a = 0, 360 - angle, angle do
        position = Vector2:PointOnCircle(a):Multiply(radius):OnY()
        rotation = Rotation:New(0, -a, 0)
        cameraPath:Extend(position, rotation, 0.75 * radius)
    end
    cameraPath:Loop()
    cameraPath.transform = Transform:New(tool.startPosition, tool.rotation)
    cameraPath.active = true

end

