Settings = {
    description="Radial copies of your stroke with optional color shifts"
}

Parameters = {
    copies={label="Number of copies", type="int", min=1, max=96, default=32},
    eccentricity={label="Eccentricity", type="float", min=0.1, max=3, default=2},
    axisConsistency={label="Axis Consistency", type="float", min=0, max=2, default=1},
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
        radius = Symmetry:Ellipse(angle, Parameters.eccentricity)
        newZ = Math:Lerp(Symmetry.brushOffset.z, Symmetry.brushOffset.z * radius, Parameters.axisConsistency)
        position = Vector3:New(
            Symmetry.brushOffset.x * radius,
            Symmetry.brushOffset.y,
            newZ
        )
        rotation = Rotation:New(0, angle * Math.rad2Deg, 0)
        pointer = Transform:New(position, rotation)
        pointers:Insert(pointer)
    end
    return pointers
end

function End()
    -- TODO fix brush.colorHsv = initialHsv
end
