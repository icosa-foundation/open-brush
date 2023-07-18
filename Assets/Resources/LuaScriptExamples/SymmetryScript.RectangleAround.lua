Settings = {
    description="Radial copies of your stroke with optional color shifts"
}

Parameters = {
    numPointsWidth={label="Number of points along width", type="int", min=2, max=32, default=5},
    numPointsHeight={label="Number of points along height", type="int", min=2, max=32, default=5},
    spacing={label="Spacing", type="float", min=0.001 , max=1, default=.2},
    exteriorOnly={label="Exterior Only", type="int", min=0, max=1, default=1},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    initialHsv = Brush.colorHsv
end

function Main()

    if Brush.triggerPressedThisFrame then
        symmetryHueShift.generate(numPointsWidth * numPointsHeight * 2, initialHsv)
    end

    if exteriorOnly==1 then
        points = calculateRectangleExteriorPoints(numPointsWidth, numPointsHeight, spacing)
    else
        points = calculateRectanglePoints(numPointsWidth, numPointsHeight, spacing)
    end

    return Symmetry:PathToPolar(points)
end


function calculateRectangleExteriorPoints(numPointsWidth, numPointsHeight, spacing)
    local points = Path:New()
    local width = (numPointsWidth - 1) * spacing
    local height = (numPointsHeight - 1) * spacing

    for i = 0, numPointsHeight - 1 do
        for j = 0, numPointsWidth - 1 do
            if i == 0 or i == numPointsHeight - 1 or j == 0 or j == numPointsWidth - 1 then
                local x = -width / 2 + j * spacing
                local y = -height / 2 + i * spacing
                points:Insert(Vector2.New(x, y):OnZ())
            end
        end
    end

    return points
end

function calculateRectanglePoints(numPointsWidth, numPointsHeight, spacing)
    local points = Path:New()
    local width = (numPointsWidth - 1) * spacing
    local height = (numPointsHeight - 1) * spacing

    for i = 0, numPointsHeight - 1 do
        for j = 0, numPointsWidth - 1 do
            local x = -width / 2 + j * spacing
            local y = -height / 2 + i * spacing
            points:Insert(Vector2.New(x, y):OnZ())
        end
    end
    return points
end


function End()
    -- TODO fix Brush.colorHsv = initialHsv
end
