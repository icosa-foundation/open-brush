Settings = {space="canvas" }
Widgets = {copies={label="Copies", type="int", min=1, max=32, default=6}}

function Start()
    Colors = {
        {1, 0, 0},
        {0, 1, 0},
    }
end

function Main()
    rot = {0, 0, 0}
    transforms = {}
    for i = 0.0, copies do
        pos = {
            Mathf.Lerp(brush.position.x, wand.position.x, i/copies) - brush.position.x,
            Mathf.Lerp(brush.position.y, wand.position.y, i/copies) - brush.position.y,
            Mathf.Lerp(brush.position.z, wand.position.z, i/copies) - brush.position.z,
        }
        table.insert(transforms, {pos, rot})
    end
    return transforms
end
