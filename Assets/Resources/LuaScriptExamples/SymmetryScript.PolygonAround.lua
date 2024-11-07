Settings = {
    description="Radial copies of your stroke with optional color shifts"
}

Parameters = {
    copies={label="Number of copies", type="int", min=1, max=96, default=32},
    sides={label="Sides", type="int", min=3 , max=12, default=5},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    initialHsv = Brush.colorHsv
end

function Main()

    if Brush.triggerPressedThisFrame then
        symmetryHueShift.generate(Parameters.copies, initialHsv)
    end

    pointers = Path:New()
    theta = (Math.pi * 2.0) / Parameters.copies

    for i = 0, Parameters.copies - 1 do

        angle = (Symmetry.current.rotation.y * Math.deg2Rad) + i * theta
        radius = Symmetry:Polygon(angle, Parameters.sides)

        pointer = Transform:New(
            Symmetry.brushOffset:ScaleBy(radius, 1, radius),
            Rotation:New(0, angle * Math.rad2Deg, 0)
        )
        pointers:Insert(pointer)
    end
    return pointers
end

function End()
    -- TODO fix Brush.colorHsv = initialHsv
end
