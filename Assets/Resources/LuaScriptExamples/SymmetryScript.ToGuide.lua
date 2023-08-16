Settings = {
    description="A line of copies between a guide and the symmetry widget",
    space="canvas"
}

Parameters = {
    copies={label="Number of copies", type="int", min=1, max=64, default=12},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    guide = Guide:NewCube(Transform:New(3, 12, 0))
    initialHsv = Brush.colorHsv
    brushInitialPosition = Brush.position
end

function Main()

    if Brush.triggerPressedThisFrame then
        symmetryHueShift.generate(copies, initialHsv)
        brushInitialPosition = Brush.position
    end

    pointers = Path:New()
    brushOffset = Brush.position:Subtract(brushInitialPosition)
    for i = 0.0, copies do
        position = Vector3.Lerp(Symmetry.current.position, guide.position, i/copies):Add(brushOffset)
        pointers:Insert(position)
    end
    return pointers
end
