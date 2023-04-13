Settings = {
    description="Radial copies of your stroke with optional color shifts"
}

Parameters = {
    numPointsWidth={label="Number of points along width", type="int", min=2, max=32, default=5},
    numPointsHeight={label="Number of points along height", type="int", min=2, max=32, default=5},
    spacing={label="Spacing", type="float", min=0.001 , max=1, default=.2},
    exteriorOnly={label="Exterior Only", type="int", min=0, max=1, default=1},
    hueShiftFrequency={label="Hue Shift Frequency", type="float", min=0.1, max=6, default=1},
    hueShiftAmount={label="Hue Shift Amount", type="float", min=0, max=1, default=0.3}
}

function Start()
    initialHsv = brush.colorHsv
end

function Main()

    if brush.triggerIsPressedThisFrame then
        if hueShiftAmount > 0 then
            colors = {}
            copies = numPointsWidth * numPointsHeight * 2
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

    if (exteriorOnly==1) then
        points = calculateRectangleExteriorPoints(numPointsWidth, numPointsHeight, spacing)
    else
        points = calculateRectanglePoints(numPointsWidth, numPointsHeight, spacing)
    end

    return symmetry.pointsToPolar(points)
end


function calculateRectangleExteriorPoints(numPointsWidth, numPointsHeight, spacing)
    local points = {}
    local width = (numPointsWidth - 1) * spacing
    local height = (numPointsHeight - 1) * spacing

    for i = 0, numPointsHeight - 1 do
        for j = 0, numPointsWidth - 1 do
            if i == 0 or i == numPointsHeight - 1 or j == 0 or j == numPointsWidth - 1 then
                local x = -width / 2 + j * spacing
                local y = -height / 2 + i * spacing
                table.insert(points, {x, y})
            end
        end
    end

    return points
end

function calculateRectanglePoints(numPointsWidth, numPointsHeight, spacing)
    local points = {}
    local width = (numPointsWidth - 1) * spacing
    local height = (numPointsHeight - 1) * spacing

    for i = 0, numPointsHeight - 1 do
        for j = 0, numPointsWidth - 1 do
            local x = -width / 2 + j * spacing
            local y = -height / 2 + i * spacing
            table.insert(points, {x, y})
        end
    end
    return points
end


function End()
    --TODO
    --color.setHsv(color.HsvToRgb(brush.colorHsv))
end
