Settings = {
    description="Draws a spherical spiral",
    previewType="sphere"
}

Parameters = {
    steps={label="Steps", type="float", min=3, max=2000, default=500},
    turns={label="Turns", type="float", min=1, max=40, default=10},
}

function OnTriggerReleased()

    radius = 1.0
    points = Path:New()

    for i = 0, steps do

        radius = Math:Sqrt(1 - x * x)
        angle = (Math.pi * 2 * turns * i) / steps

        x = radius * Math:Sin(angle)
        y = radius * Math:Cos(angle)
        z = 2.0 * i / steps - 1

        points:Insert(Transform:New(x, y, z))
    end
    return points
end
