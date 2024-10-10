local symmetryHueShift = {}

if not Parameters then
	Parameters = {}
end

Parameters["hueShiftFrequency"] = {label="Hue Shift Frequency", type="float", min=0.1, max=6, default=1} --[[@as number]]
Parameters["hueShiftAmount"] = {label="Hue Shift Amount", type="float", min=0, max=1, default=0.3} --[[@as number]]

function symmetryHueShift.generate(copies, initialHsv)
	Symmetry:ClearColors()
	if Parameters.hueShiftAmount > 0 then
		for i = 0, copies - 1 do
			t = i / copies
			newHue = Waveform:Triangle(t, Parameters.hueShiftFrequency) * Parameters.hueShiftAmount
			newColor = Color:HsvToRgb(initialHsv.x + newHue, initialHsv.y, initialHsv.z)
			Symmetry:AddColor(newColor)
		end
		Brush:ForceNewStroke()
	end
end

return symmetryHueShift
