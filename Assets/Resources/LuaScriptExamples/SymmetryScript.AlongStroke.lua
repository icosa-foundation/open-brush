Settings = {space="pointer"}

Parameters = {
    copies={label="Copies", type="int", min=1, max=96, default=32},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    initialHsv = Brush.colorHsv
    stroke = Sketch.strokes.last
    updatePath()
end

function Main()

    -- Update the path only when we change the number of copies
    if Parameters.copies ~= previousCopies then
        updatePath()
        previousCopies = Parameters.copies
    end

    return path

end

function updatePath()

    if stroke == nil then
        App:Error("Please draw a stroke and then restart this plugin")
        path = Path:New()
    else
        path = stroke.path
        path:SampleByCount(Parameters.copies)
        path:Center()
        symmetryHueShift.generate(Parameters.copies, initialHsv)
    end

end
