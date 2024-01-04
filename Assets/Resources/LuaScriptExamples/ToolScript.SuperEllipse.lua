Settings = {
    description="Draws a superellipse (otherwise known as a squircle)",
    previewType="quad"
}

Parameters = {
    n={label="n", type="float", min=0.01, max=5, default=3.5},
}

function Main()
    if Brush.triggerReleasedThisFrame then
        points = Path:New()
        for i = 0, 360, 10 do
            angle = i * Math.pi / 180
            x = Math:Pow(Math:Abs(Math:Cos(angle)), 2/Parameters.n) * Math:Sign(Math:Cos(angle))
            y = Math:Pow(Math:Abs(Math:Sin(angle)), 2/Parameters.n) * Math:Sign(Math:Sin(angle))
            position = Vector3:New(x, y, 0)
            rotation = Rotation:New(0, 0, angle * 180)
            points:Insert(Transform:New(position, rotation))
        end
        return points
    end
end
