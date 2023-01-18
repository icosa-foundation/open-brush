Settings = {
    previewType="cube"
}

function OnTriggerReleased()

    points = {}

    -- front face
    table.insert(points, {{-1, 1, 1}, {0, 0, 0}}) -- top left
    table.insert(points, {{1, 1, 1}, {0, 0, 0}}) -- top right
    table.insert(points, {{1, -1, 1}, {0, 0, 0}}) -- bottom right
    table.insert(points, {{-1, -1, 1}, {0, 0, 0}}) -- bottom left
    table.insert(points, {{-1, 1, 1}, {0, 0, 0}}) -- top left (to close the loop)

    -- right face
    table.insert(points, {{1, 1, 1}, {0, 0, 90}}) -- top right
    table.insert(points, {{1, 1, -1}, {0, 0, 90}}) -- top back
    table.insert(points, {{1, -1, -1}, {0, 0, 90}}) -- bottom back
    table.insert(points, {{1, -1, 1}, {0, 0, 90}}) -- bottom right
    table.insert(points, {{1, 1, 1}, {0, 0, 90}}) -- top right (to close the loop)

    -- back face
    table.insert(points, {{1, 1, -1}, {0, 0, 180}}) -- top back
    table.insert(points, {{-1, 1, -1}, {0, 0, 180}}) -- top left
    table.insert(points, {{-1, -1, -1}, {0, 0, 180}}) -- bottom left
    table.insert(points, {{1, -1, -1}, {0, 0, 180}}) -- bottom back
    table.insert(points, {{1, 1, -1}, {0, 0, 180}}) -- top back (to close the loop)

    -- left face
    table.insert(points, {{-1, 1, -1}, {0, 0, 270}}) -- top left
    table.insert(points, {{-1, 1, 1}, {0, 0, 270}}) -- top front
    table.insert(points, {{-1, -1, 1}, {0, 0, 270}}) -- bottom front
    table.insert(points, {{-1, -1, -1}, {0, 0, 270}}) -- bottom left
    table.insert(points, {{-1, 1, -1}, {0, 0, 270}}) -- top left (to close the loop)

    return points
end
