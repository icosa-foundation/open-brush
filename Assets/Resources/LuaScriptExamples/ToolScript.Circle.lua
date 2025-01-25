Settings = {
    description="Draws a circle",
    previewType="quad"
}

function Main()
    if Brush.triggerReleasedThisFrame then
        points = Path:New()
        for angle = 0, 360, 10 do
            position2d = Vector2:PointOnCircle(angle)
            rotation = Rotation:New(0, 0, angle * 180)
            points:Insert(Transform:New(position2d:OnZ(), rotation))
        end
        return points
    end
end

