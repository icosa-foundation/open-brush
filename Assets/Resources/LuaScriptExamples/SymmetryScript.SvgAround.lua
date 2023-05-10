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

    return Symmetry.PathToPolar(path)
end

function End()
    Brush.colorHsv = initialHsv
end

function updatePath()
    svgPath = "m 0 0 l -98.6 92.5 l -98.6 -92.5 c -22.9 -24.5 -21.7 -63 2.8 -85.9 l 2.2 -2.1 c 24.5 -22.9 63 -21.7 85.9 2.8 l 7.6 7.9 l 7.6 -7.9 c 22.9 -24.5 61.4 -25.8 85.9 -2.8 c 26.7 25 28 63.5 5.1 88"
    multipaths = Svg:ParsePathString(svgPath) -- Convert the SVG path to a list of paths
    path = multipaths:Longest() -- Get the longest path
    path:Rotate(Rotation.clockwise) -- Rotate 90 degrees
    path:Normalize(2) -- Scale and center inside a 2x2 square
    path:Resample(spacing) -- Evenly space all the points
    lowest = path:FindMinimumX(path) -- Find the point with the lowest x value
    path:StartingFrom(lowest) -- Make it the new start point
    symmetryHueShift.generate(path.count, initialHsv)
end

