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

    if Parameters.exteriorOnly==1 then
        points = calculateRectangleExteriorPoints()
    else
        points = calculateRectanglePoints()
    end

    if Brush.triggerPressedThisFrame then
        symmetryHueShift.generate(points.count, initialHsv)
    end

    return Symmetry:PathToPolar(points)
end


function calculateRectangleExteriorPoints()
    local points = Path:New()
    local width = (Parameters.numPointsWidth - 1) * Parameters.spacing
    local height = (Parameters.numPointsHeight - 1) * Parameters.spacing

    for i = 0, Parameters.numPointsHeight - 1 do
        for j = 0, Parameters.numPointsWidth - 1 do

            if i == 0
                or i == Parameters.numPointsHeight - 1
                or j == 0
                or j == Parameters.numPointsWidth - 1
            then

                local x = -width / 2 + j * Parameters.spacing
                local y = -height / 2 + i * Parameters.spacing
                local pos = Vector2.New(x, y):OnZ()
                points:Insert(Transform:New(pos))

            end
        end
    end

    return points

end

function calculateRectanglePoints()

    local points = Path:New()
    local width = (Parameters.numPointsWidth - 1) * Parameters.spacing
    local height = (Parameters.numPointsHeight - 1) * Parameters.spacing

    for i = 0, Parameters.numPointsHeight - 1 do
        for j = 0, Parameters.numPointsWidth - 1 do
            local x = -width / 2 + j * Parameters.spacing
            local y = -height / 2 + i * Parameters.spacing
            points:Insert(Vector2.New(x, y):OnZ())
        end
    end
    return points
end


function End()
    -- TODO fix Brush.colorHsv = initialHsv
end
