Settings = {
    description="Radial copies of your stroke with optional color shifts"
}

Parameters = {
    copies={label="Number of copies", type="int", min=1, max=36, default=32},
    hueShiftFrequency={label="Hue Shift Frequency", type="float", min=0.1, max=6, default=1},
    hueShiftAmount={label="Hue Shift Amount", type="float", min=0, max=1, default=0.3}
}

function Start()
    initialHsv = brush.colorHsv
    --symmetry.transform = {
    --    {0, 10, 6},
    --    {0, 90, 90}
    --}
end

function Main()

    pointers = {}
    theta = 360.0 / copies
    Colors = {}

    for i = 0, copies - 1 do
        angle = i * theta
        pointer = {
            position={
                symmetry.brushOffset.x * (1 + (math.sin(angle/360 * 16 * math.pi)) * 0.25),
                symmetry.brushOffset.y,
                symmetry.brushOffset.z
            },
            rotation={0, angle, 0}
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
