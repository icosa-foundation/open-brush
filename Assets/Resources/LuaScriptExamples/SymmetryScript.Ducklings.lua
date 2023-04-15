Settings = {space="canvas"}

Parameters = {
    copies={label="Number of copies", type="int", min=1, max=64, default=12},
    delay={label="Delay per copy", type="int", min=1, max=10, default=4},
    amount={label="Amount", type="float", min=0, max=1, default=0.5},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    initialHsv = brush.colorHsv
end

function Main()

    if brush.triggerIsPressedThisFrame then
        brush.setBufferSize(1)
        brush.forceNewStroke()
        initialHsv = brush.colorHsv
    end

    pointers = {}
    colors = {}

    brush.setBufferSize(copies * delay)
    for i = 0, copies - 1 do
        pointer = {
            position=unityVector3.lerp(brush.position, brush.pastPosition(i * delay), amount)
        }
        table.insert(pointers, pointer)

        --Colour cycling for the extra pointers
        if hueShiftAmount > 0 then
            t = i / copies
            newHue = waveform.triangle(t, hueShiftFrequency) * hueShiftAmount
            newColor = unityColor.hsvToRgb(initialHsv.x + newHue, initialHsv.y, initialHsv.z)
            table.insert(colors, newColor)
        end
        symmetry.setColors(colors)
    end
    return pointers
end
