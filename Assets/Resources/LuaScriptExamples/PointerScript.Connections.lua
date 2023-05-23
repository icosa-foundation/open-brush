Settings = {
    description="Connects the two most recent strokes with a series of lines"
}

Parameters = {
    parts={label="Number of sections", type="int", min=2, max=100, default=20},
}

function OnTriggerReleased()
    if Sketch.strokes.count > 0 then
        lastPath = Brush.currentPath
        lastButOnePath = Sketch.strokes.last.path
        lastPath:Subdivide(parts)
        lastButOnePath:Subdivide(parts)
        for i = 0, parts do

            -- Straight line version
            --local path = Path:New({lastPath[i], lastButOnePath[i]})
            --path:Subdivide(8)

            -- Use splines
            local startPoint = lastPath[i].position
            local endPoint = lastButOnePath[i].position
            local startTangent = lastPath.GetTangent(i)
            local endTangent = lastButOnePath.GetTangent(i)
            path = Path:Hermite(startPoint, endPoint, startTangent, endTangent, 8, 2)
            path:Draw()
        end
    end
    return Transform.zero
end
