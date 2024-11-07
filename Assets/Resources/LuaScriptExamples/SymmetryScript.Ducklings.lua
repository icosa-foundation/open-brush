Settings = {space="canvas"}

Parameters = {
    copies={label="Number of copies", type="int", min=1, max=64, default=12},
    delay={label="Delay per copy", type="int", min=1, max=10, default=4},
    mix={label="Amount", type="float", min=0, max=1, default=0.5},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    initialHsv = Brush.colorHsv
end

function Main()

    if Brush.triggerPressedThisFrame then
        Brush:SetHistorySize(1)
        Brush:ForceNewStroke()
        initialHsv = Brush.colorHsv
    end

    pointers = Path:New()
    Symmetry:ClearColors()

    Brush:SetHistorySize(Parameters.copies * Parameters.delay)
    for i = 0, Parameters.copies - 1 do
        pointer = Transform:New(Vector3:Lerp(Brush.position, Brush:GetPastPosition(i * Parameters.delay), Parameters.mix))
        pointers:Insert(pointer)

        --Colour cycling for the extra pointers
        if Parameters.hueShiftAmount > 0 then
            t = i / Parameters.copies
            newHue = Waveform:Triangle(t, Parameters.hueShiftFrequency) * Parameters.hueShiftAmount
            newColor = Color:HsvToRgb(initialHsv.x + newHue, initialHsv.y, initialHsv.z)
            Symmetry:AddColor(newColor)
        end
    end
    return pointers
end
