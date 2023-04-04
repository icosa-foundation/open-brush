Settings = {
    description="Linear copies of your stroke with optional color shifts"
}

Parameters = {
    copies={label="Number of copies", type="int", min=1, max=36, default=12},
    distance={label="Distance", type="float", min=0, max=20, default=5},
    hueShiftFrequency={label="Hue Shift Frequency", type="float", min=0.1, max=6, default=1},
    hueShiftAmount={label="Hue Shift Amount", type="float", min=0, max=1, default=0.5}
}

function Start()
    initialHsv = brush.colorHsv
end

function Main()

    pointers = {}
    Colors = {}

    for i = 0, copies - 1 do

        t = i / copies

        --Shift the extra pointers
        pos = {{
           symmetry.brushOffset.x - distance * t,
           symmetry.brushOffset.y,
           symmetry.brushOffset.z
       }}
        table.insert(pointers, pos)

        --Colour cycling for the extra pointers
        if hueShiftAmount > 0 then
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
