Settings = {
    description="Radial copies of your stroke with optional color shifts"
}

Parameters = {
    copies={label="Number of copies", type="int", min=1, max=96, default=32},
    sides={label="Sides", type="int", min=3 , max=12, default=5},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    initialHsv = brush.colorHsv
end

function Main()

    if brush.triggerIsPressedThisFrame then
        symmetryHueShift.generate(copies, initialHsv)
    end

    pointers = {}
    theta = (math.pi * 2.0) / copies

    for i = 0, copies - 1 do
        angle = (symmetry.rotation.y * unityMathf.deg2Rad) + i * theta
        radius = symmetry.polygon(angle, sides)

        pointer = {
            position={
                symmetry.brushOffset.x * radius,
                symmetry.brushOffset.y,
                symmetry.brushOffset.z * radius
            },
            rotation={0, angle * unityMathf.rad2Deg, 0}
        }
        table.insert(pointers, pointer)
    end
    return pointers
end

function End()
    -- TODO fix brush.colorHsv = initialHsv
end
