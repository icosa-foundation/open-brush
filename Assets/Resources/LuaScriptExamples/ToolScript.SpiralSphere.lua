Settings = {
    description="Draws a spherical spiral",
    previewType="sphere"
}

Parameters = {
    steps={label="Steps", type="float", min=3, max=500, default=200},
    turns={label="Turns", type="float", min=1, max=40, default=10},
}

function Main()
    if Brush.triggerReleasedThisFrame then
        points = Path:New()
        for i = 0, Parameters.steps do
            z = 2.0 * i / Parameters.steps - 1
            radius = Math:Sqrt(1 - z * z)
            angle = (Math.pi * 2 * Parameters.turns * i) / Parameters.steps
            x = radius * Math:Sin(angle)
            y = radius * Math:Cos(angle)
            points:Insert(Transform:Position(x, y, z))
        end
        return points
    end
end
