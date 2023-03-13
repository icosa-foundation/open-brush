Settings = {
    description="Multiple copies of your brush spinning around your actual brush position",
    space="pointer"
}


Parameters = {
    copies={label="Copies", type="int", min=1, max=8, default=2},
    speed={label="Speed", type="float", min=0.01, max=16, default=8},
    radius={label="Radius", type="float", min=0.01, max=2, default=.5},
}

function Main()
    transforms = {}
    rotation = {0, 0, 0}
    for i = 1.0, copies do
        angle = (app.time * speed) + ((math.pi * 2.0) * (i/copies))
        position = {x=math.sin(angle) * radius, y=math.cos(angle) * radius, z=0}
        table.insert(transforms, {position, rotation})
    end
    return transforms
end
