Settings = {
    space="pointer"
}

Widgets = {
    copies={label="Copies", type="int", min=1, max=8, default=2},
    speed={label="Speed", type="float", min=0.01, max=16, default=8},
    radius={label="Radius", type="float", min=0.01, max=2, default=.5},
}

function Main()
    rot = {0, 0, 0}
    transforms = {}
    for i = 0.0, copies do
        angle = (brush.distanceDrawn * speed) + ((math.pi * 2.0) / i)
        pos = {math.sin(angle) * radius, math.cos(angle) * radius, 0}
        table.insert(transforms, {pos, rot})
    end
    return transforms
end
