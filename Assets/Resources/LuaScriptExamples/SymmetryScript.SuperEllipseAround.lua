Settings = {
    description="Radial copies of your stroke with optional color shifts"
}

Parameters = {
    copies={label="Number of copies", type="int", min=1, max=96, default=32},
    n={label="n", type="float", min=3 , max=8, default=2},
    eccentricity={label="Eccentricity", type="float", min=0.1, max=3, default=2},
    axisConsistency={label="Axis Consistency", type="float", min=0, max=2, default=1},
    hueShiftFrequency={label="Hue Shift Frequency", type="float", min=0.1, max=6, default=1},
    hueShiftAmount={label="Hue Shift Amount", type="float", min=0, max=1, default=0.3}
}

function Start()
    initialHsv = brush.colorHsv
end

function Main()

    pointers = {}
    theta = (math.pi * 2.0) / copies
    Colors = {}

    for i = 0, copies - 1 do
        angle = (symmetry.rotation.y * unityMathf.deg2Rad) + i * theta
        radius = symmetry.superellipse(angle, n, eccentricity)

        pointer = {
            position={
                symmetry.brushOffset.x * radius,
                symmetry.brushOffset.y,
                unityMathf.lerp(
                    symmetry.brushOffset.z,
                symmetry.brushOffset.z * radius,
                    axisConsistency
                ),
            },
            rotation={0, angle * unityMathf.rad2Deg, 0}
        }
        table.insert(pointers, pointer)

        --Colour cycling for the extra pointers
        if hueShiftAmount > 0 then
            t = i / copies
            newHue = waveform.triangle(t, hueShiftFrequency) * hueShiftAmount
            newColor = unityColor.hsvToRgb(initialHsv.x + newHue, initialHsv.y, initialHsv.z)
            table.insert(Colors, newColor)
        end

    end
    return pointers
end

function End()
    --TODO
    --color.setHsv(color.HsvToRgb(brush.colorHsv))
end
