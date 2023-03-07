Widgets = {
    copies={label="Number of copies", type="int", min=1, max=36, default=6},
    hueShiftFrequency={label="Hue Shift Frequency", type="float", min=0.1, max=6, default=1},
    hueShiftAmount={label="Hue Shift Amount", type="float", min=0, max=1, default=0}
}

function Start()
    initialHsv = brush.colorHsv
end

function Main()

    pointers = {}
    theta = 360.0 / copies
    Colors = {}

    for i = 0, copies - 1 do
        --Rotate the extra pointers around our centre
        table.insert(pointers, {position={0, 0, 0}, rotation={0, i * theta, 0}})

        --Colour cycling for the extra pointers
        if hueShiftAmount > 0 then
            t = i / copies
            newHue = waveform.triangle(t, hueShiftFrequency) * hueShiftAmount
            newColor = color.HsvToRgb(initialHsv.x + newHue, initialHsv.y, initialHsv.z)
            table.insert(Colors, newColor)
        end

    end
    return pointers
end

function End()
    --TODO
    --color.setHsv(color.HsvToRgb(brush.colorHsv))
end
