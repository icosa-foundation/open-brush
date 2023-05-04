Settings = {
    description="Radial copies of your stroke with optional color shifts"
}

Parameters = {
    copies={label="Number of copies", type="int", min=1, max=36, default=32},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    initialHsv = Brush.colorHsv
end

function Main()

    if Brush.triggerIsPressedThisFrame then
        symmetryHueShift.generate(copies, initialHsv)
    end

    pointers = Path:New()
    theta = 360.0 / copies

    for i = 0, copies - 1 do
        angle = i * theta
        radius = (1 + (Math:Sin(angle/360 * 16 * Math.pi)) * 0.25)
        pointer = Transform:New(
            Symmetry.brushOffset:Scale(radius, 0, 0),
            Rotation:New(0, angle, 0)
        )
        pointers:Insert(pointer)
    end
    return pointers
end

function End()
    -- TODO fix Brush.colorHsv = initialHsv
end
