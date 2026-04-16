Settings = {
    description="Draws a circle",
    previewType="quad",
    previewMode="stroke"
}

local function buildCircle()
    local points = Path:New()
    for angle = 0, 360, 10 do
        local position2d = Vector2:PointOnCircle(angle)
        local rotation = Rotation:New(0, 0, angle * 180)
        points:Insert(Transform:New(position2d:OnZ(), rotation))
    end
    points:Insert(points[0]) -- Close the loop

    return points
end

function Main()
    if Brush.triggerIsPressed or Brush.triggerReleasedThisFrame then
        return buildCircle()
    end
end
