Settings = {
    description=""
}

Parameters = {
    rate={label="Rate", type="int", min=0, max=100, default=30},
    hueShiftFrequency={label="Hue Shift Frequency", type="float", min=0.1, max=6, default=1},
    hueShiftAmount={label="Hue Shift Amount", type="float", min=0, max=1, default=.5}
}

function Start()
    initialHsv = Brush.colorHsv
end

function Main()

    if App.frames % Parameters.rate == 0 then
        newHue = Waveform:Triangle(App.time, Parameters.hueShiftFrequency) * Parameters.hueShiftAmount
        newColor = Color:HsvToRgb(initialHsv.x + newHue, initialHsv.y, initialHsv.z)
        Brush.colorRgb = newColor
        Brush:ForceNewStroke()
    end

end
