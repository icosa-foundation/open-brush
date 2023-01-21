Settings = {space="canvas" }
Widgets = {copies={label="Copies", type="int", min=1, max=32, default=6}}

function Start()
    Colors = {
        {1, 0 , 0},
        {0, 1 , 0},
    }
end

function Main()
    rot = {0, 0, 0}
    transforms = {}
    brush = brush.position
    wand = wand.position
    for i = 0.0, copies do
        pos = {
            Mathf.Lerp(brush.x, wand.x, i/copies) - brush.x,
            Mathf.Lerp(brush.y, wand.y, i/copies) - brush.y,
            Mathf.Lerp(brush.z, wand.z, i/copies) - brush.z,
        }
        table.insert(transforms, {pos, rot})
    end
    return transforms
end
