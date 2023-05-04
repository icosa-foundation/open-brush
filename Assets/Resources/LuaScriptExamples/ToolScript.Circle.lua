Settings = {
    description="Draws a circle",
    previewType="quad"
}

function OnTriggerReleased()
    points = Path:New()
    for angle = 0, 360, 10 do
        position = Vector2:PointOnCircle(angle)
        rotation = Rotation:New(0, 0, angle * 180)
        points:Insert(Transform:New(position, rotation))
    end
    return points
end

