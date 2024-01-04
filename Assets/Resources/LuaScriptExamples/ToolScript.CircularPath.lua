Settings = {
    description="Draws a circular camera path",
    previewType="quad", previewAxis="y"
}

Parameters = {
    sides = {label="Sides", type="float", min=3, max=16, default=4},
}

function Main()
    if Brush.triggerReleasedThisFrame then

        -- Create the camera path in one go by passing in a list of transforms
        path = Path:New()
        radius = Tool.vector.magnitude
        angle = 360.0 / Parameters.sides
        for a = 0, 360 - angle, angle do
            position2d = Vector2:PointOnCircle(a) * radius
            rotation = Rotation:New(0, -a, 0)
            path:Insert(Transform:New(position2d:OnY(), rotation, ((Math.pi * 0.5) / Parameters.sides) * radius))
        end
        path:TransformBy(Transform:New(Tool.startPoint.position, Tool.rotation));
        CameraPath:FromPath(path, true)

        ---- Create the camera path knot by knot
        -- This code is here as an example and therefore has been commented out
        --radius = Tool.vector.magnitude
        --cameraPath = CameraPath:New()
        --angle = 30
        --for a = 0, 360 - angle, angle do
        --    position = Vector2:PointOnCircle(a):Multiply(radius):OnY()
        --    rotation = Rotation:New(0, -a, 0)
        --    cameraPath:Extend(position, rotation, 0.75 * radius)
        --end
        --cameraPath:Loop()
        --cameraPath.transform = Transform:New(Tool.startPoint.position, Tool.rotation)
        --cameraPath.active = true

    end
end

