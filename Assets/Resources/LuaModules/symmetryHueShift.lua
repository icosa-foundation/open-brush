local symmetryHueShift =  {}

if not Parameters then
	Parameters = {}
end

Parameters["hueShiftFrequency"] = {label="Hue Shift Frequency", type="float", min=0.1, max=6, default=1}
Parameters["hueShiftAmount"] = {label="Hue Shift Amount", type="float", min=0, max=1, default=0.3}

function symmetryHueShift.generate(copies, initialHsv)
	Symmetry.ClearColors()
	if hueShiftAmount > 0 then
		for i = 0, copies - 1 do
			t = i / copies
			newHue = Waveform:Triangle(t, hueShiftFrequency) * hueShiftAmount
			newColor = Color.HsvToRgb(initialHsv.x + newHue, initialHsv.y, initialHsv.z)
			Symmetry.AddColor(newColor)
		end
		Brush.ForceNewStroke()
	end
end

return symmetryHueShift
