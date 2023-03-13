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
    points = {}
    for i = 0, steps do
        x = 2.0 * i / steps - 1
        radius=math.sqrt(1 - x * x)
        angle = (math.pi * 2 * turns * i) / steps
        y = radius * math.cos(angle)
        z = radius * math.sin(angle)
        position = { z, y, x}
        rotation = { 0, 0, 0}
        table.insert(points, { position, rotation })
    end
    return points
end
