Settings = {
    description="Draws a circular camera path",
    previewType="quadx"
}

function OnTriggerReleased()

    -- Create the camera path in one go by passing in a list of transforms
    path = Path:New()
    angle = 30
    for a = 0, 360 - angle, angle do
        position = Vector2:PointOnCircle(a):Multiply(radius):OnY()
        rotation = Rotation:New(0, -i, 0)
        path.Insert(Transform:New(position, rotation, 0.325 * radius))
    end
    CameraPath:FromPath(path, true)

    -- Create the camera path knot by knot
    --radius = Tool.vector.magnitude
    --cameraPath = CameraPath:New()
    --angle = 30
    --for a = 0, 360 - angle, angle do
    --    position = Vector2:PointOnCircle(a):Multiply(radius):OnY()
    --    rotation = Rotation:New(0, -a, 0)
    --    cameraPath:Extend(position, rotation, 0.75 * radius)
    --end
    --cameraPath:Loop()
    --cameraPath.transform = Transform:New(tool.startPosition, tool.rotation)
    --cameraPath.active = true

end

