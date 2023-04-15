Parameters = {
    spacing={label="Point Spacing", type="float", min=0.1, max=1, default=0.1},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    initialHsv = brush.colorHsv
end

function Main()

    -- Update the path if we changed the spacing
    if (spacing ~= lastSpacing) then
        UpdatePath()
        lastSpacing = spacing
    end

    return symmetry.transformsToPolar(points)
end

function End()
    brush.colorHsv = initialHsv
end

function UpdatePath()
    svgPath = "m 0 0 l -98.6 92.5 l -98.6 -92.5 c -22.9 -24.5 -21.7 -63 2.8 -85.9 l 2.2 -2.1 c 24.5 -22.9 63 -21.7 85.9 2.8 l 7.6 7.9 l 7.6 -7.9 c 22.9 -24.5 61.4 -25.8 85.9 -2.8 c 26.7 25 28 63.5 5.1 88"
    points = path.fromSvg(svgPath, 1) -- Convert the SVG path to a list of transforms
    points = path.rotate(points, {0, 0, 90}) -- Rotate 90 degrees
    points = path.normalized(points) -- Scale it to fit inside a 1x1 square
    points = path.scale(points, {2, 2, 2}) -- Double so the square is 2x2 (so each edge is 1 unit from the origin)
    points = path.resample(points, spacing) -- Make the points evenly spaced
    lowest = path.findMinimum(points, 0) -- Find the point with the lowest y value
    points = path.startingFrom(points, lowest) -- Make it the new start point
    symmetryHueShift.generate(#points, initialHsv)
end

