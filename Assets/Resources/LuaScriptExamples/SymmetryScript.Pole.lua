Settings = {
    description="Multiple copies of your brush spaced between your left and right hand positions",
}

Parameters = {copies={label="Copies", type="int", min=1, max=32, default=12}}

function Start()
    Colors = {
        {1, 0, 0},
        {0, 1, 0},
    }
end

function Main()
    transforms = {}
    for i = 0.0, copies do
        position = {
            unityMathf.lerp(symmetry.brushOffset.x, symmetry.wandOffset.x, i/copies),
            unityMathf.lerp(symmetry.brushOffset.y, symmetry.wandOffset.y, i/copies),
            unityMathf.lerp(symmetry.brushOffset.z, symmetry.wandOffset.z, i/copies),
        }
        table.insert(transforms, { position })
    end
    return transforms
end
