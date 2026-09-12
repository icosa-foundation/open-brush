Settings = {
    description="Draws a spherical spiral",
    previewType="stroke",
    previewInterval=0.1
}

Parameters = {
    steps={label="Steps", type="float", min=3, max=500, default=200},
    turns={label="Turns", type="float", min=1, max=40, default=10},
}

function Main()
    if Brush.triggerIsPressed or Brush.triggerReleasedThisFrame then
        points = Path:New()
        local totalSteps = Parameters.steps
        if Tool.isPreview then
            totalSteps = Math:Min(totalSteps, 200)
        end
        for i = 0, totalSteps do
            z = 2.0 * i / totalSteps - 1
            radius = Math:Sqrt(1 - z * z)
            angle = (Math.pi * 2 * Parameters.turns * i) / totalSteps
            x = radius * Math:Sin(angle)
            y = radius * Math:Cos(angle)
            points:Insert(Transform:Position(x, y, z))
        end
        return points
    end
end
