local symmetryHueShift =  {}

if not Parameters then
	Parameters = {}
end

Parameters["hueShiftFrequency"] = {label="Hue Shift Frequency", type="float", min=0.1, max=6, default=1}
Parameters["hueShiftAmount"] = {label="Hue Shift Amount", type="float", min=0, max=1, default=0.3}

function symmetryHueShift.generate(copies, initialHsv)
	if hueShiftAmount > 0 then
		colors = {}
		for i = 0, copies - 1 do
			t = i / copies
			newHue = waveform.triangle(t, hueShiftFrequency) * hueShiftAmount
			newColor = unityColor.hsvToRgb(initialHsv.x + newHue, initialHsv.y, initialHsv.z)
			table.insert(colors, newColor)
		end
		symmetry.setColors(colors)
		brush.forceNewStroke()
	end
end

return symmetryHueShift
