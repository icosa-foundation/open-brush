function Main()
    return {
        -- Top bar
        { position = { symmetry.brushOffset.x + 0, symmetry.brushOffset.y + 0, symmetry.brushOffset.z }},
        { position = { symmetry.brushOffset.x + 1, symmetry.brushOffset.y + 0, symmetry.brushOffset.z }},
        { position = { symmetry.brushOffset.x + 2, symmetry.brushOffset.y + 0, symmetry.brushOffset.z }},
        ----Sides
        { position = { symmetry.brushOffset.x + 0, symmetry.brushOffset.y + 1, symmetry.brushOffset.z }},
        { position = { symmetry.brushOffset.x + 2, symmetry.brushOffset.y + 1, symmetry.brushOffset.z }},
        ---- Bottom bar
        { position = { symmetry.brushOffset.x + 0, symmetry.brushOffset.y + 2, symmetry.brushOffset.z }},
        { position = { symmetry.brushOffset.x + 1, symmetry.brushOffset.y + 2, symmetry.brushOffset.z }},
        { position = { symmetry.brushOffset.x + 2, symmetry.brushOffset.y + 2, symmetry.brushOffset.z }},
    }
end
