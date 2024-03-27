Settings = {
    description="Multiple copies of your brush spinning around your actual brush position",
    space="pointer"
}

Parameters = {
    copies={label="Copies", type="int", min=1, max=8, default=2},
    speed={label="Speed", type="float", min=0, max=1000, default=500},
    radius={label="Radius", type="float", min=0.1, max=2, default=0.25},
}

function Main()
    pointers = Path:New()
    for i = 1.0, Parameters.copies do
        angle = (App.time * Parameters.speed) + (360 * (i / Parameters.copies))
        position2d = Vector2:PointOnCircle(angle) * Parameters.radius
        pointers:Insert(Transform:New(position2d:OnZ()))
    end
    return pointers
end
