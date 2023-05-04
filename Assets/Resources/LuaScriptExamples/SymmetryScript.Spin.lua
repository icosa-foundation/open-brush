Settings = {
    description="Multiple copies of your brush spinning around your actual brush position",
    space="pointer"
}

Parameters = {
    copies={label="Copies", type="int", min=1, max=8, default=2},
    speed={label="Speed", type="float", min=0.01, max=16, default=8},
    radius={label="Radius", type="float", min=0.1, max=200, default=50},
}

function Main()
    pointers = Path:New()
    for i = 1.0, copies do
        angle = (App.time * speed) + ((Math.pi * 2.0) * (i / copies))
        position = Vector2:PointOnCircle(angle):Multiply(radius)
        pointers:Insert(position.x, position.y, 0)
    end
    return transforms
end
