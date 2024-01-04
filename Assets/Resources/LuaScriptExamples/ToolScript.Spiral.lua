Settings = {
    description="Draws a conical spiral",
    previewType="cube"
}

Parameters = {
    turns={label="Number of turns", type="float", min=0, max=20, default=6},
    steps={label="Number of steps per turn", type="int", min=1, max=32, default=12},
}

function Main()
    if Brush.triggerReleasedThisFrame then
        points = Path:New();
        totalSteps = Parameters.turns * Parameters.steps
        for i = 0, 1, 1/totalSteps do
            angle = Math.pi * 2 * Parameters.turns * i
            position = Vector3:New(Math:Cos(angle) * i, Math:Sin(angle) * i, -(i * 2) + 1)
            rotation = Rotation:New(0, angle * Math.rad2Deg, 0)
            points:Insert(Transform:New(position, rotation))
        end
        return points
    end
end
