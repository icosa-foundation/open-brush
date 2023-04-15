Settings = {
    description="Radial copies of your stroke with optional color shifts"
}

Parameters = {
    copies={label="Number of copies", type="int", min=1, max=96, default=32},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    initialHsv = brush.colorHsv
    --symmetry.transform = {
    --    {0, 10, 6},
    --    {0, 90, 90}
    --}
end

function Main()

    if brush.triggerIsPressedThisFrame then
        symmetryHueShift.generate(copies, initialHsv)
    end

    pointers = {}
    theta = 360.0 / copies

    for i = 0, copies - 1 do
        angle = i * theta
        pointer = {
            position={
                symmetry.brushOffset.x,
                symmetry.brushOffset.y,
                symmetry.brushOffset.z
            },
            rotation={0, angle, 0}
        }
        table.insert(pointers, pointer)
    end
    return pointers
end

function End()
    -- TODO fix brush.colorHsv = initialHsv
end
