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

    -- Update the path if we changed the spacing
    if copies ~= lastCopies then
        updatePath()
        lastCopies = copies
    end
    return path
end

function updatePath()
    if stroke == nil then
        App.Error("Please draw a stroke and then restart this plugin")
        path = Path:New()
    else
        path = stroke.path
        path:SampleByCount(copies)
        path:Center()
        symmetryHueShift.generate(copies, initialHsv)
    end
end
