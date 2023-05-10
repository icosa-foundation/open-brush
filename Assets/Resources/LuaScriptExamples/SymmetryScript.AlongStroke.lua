Settings = {space="pointer"}

Parameters = {
    spacing={label="Point Spacing", type="float", min=0.1, max=1, default=0.1},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    initialHsv = Brush.colorHsv
    updatePath()
end

function Main()

    -- Update the path if we changed the spacing
    if (spacing ~= lastSpacing) then
        updatePath()
        lastSpacing = spacing
        symmetryHueShift.generate(path.count, initialHsv)
    end
    return path
end

function updatePath()
    stroke = Sketch.strokes.last
    if (stroke == nil) then
        App.Error("Please draw a stroke and then restart this plugin")
        path = Path:New()
    else
        path = stroke.path
        path:Resample(spacing)
        path:Center()
    end
end
