Settings = {
    description="Multiple copies of your brush spaced between your left and right hand positions",
    space="canvas"
}

Widgets = {copies={label="Copies", type="int", min=1, max=32, default=6}}

function Start()
    --Colors = {
    --    {1, 0, 0},
    --    {0, 1, 0},
    --}
end

function Main()
    rotation = { 0, 0, 0}
    transforms = {}
    for i = 0.0, copies do
        position = {
            unityMathf.lerp(brush.position.x, wand.position.x, i/copies) - brush.position.x,
            unityMathf.lerp(brush.position.y, wand.position.y, i/copies) - brush.position.y,
            unityMathf.lerp(brush.position.z, wand.position.z, i/copies) - brush.position.z,
        }
        table.insert(transforms, { position, rotation })
    end
    return transforms
end
