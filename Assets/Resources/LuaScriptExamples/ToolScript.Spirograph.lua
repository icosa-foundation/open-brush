Settings = {
    description = "Generates a spirograph pattern.",
    previewType = "quad"
}

Parameters = {
    innerRadius = {label = "Inner Radius", type = "int", min = 1, max = 64, default = 12},
    outerRadius = {label = "Outer Radius", type = "int", min = 1, max = 64, default = 32},
    penOffset = {label = "Pen Offset", type = "float", min = 0, max = 20, default = 5},
    cycles={label = "Cycles", type = "float", min = 0.5, max = 3, default = 1.1},
    depth={label = "Depth", type = "float", min = 0, max = 32, default = 6},
    points = {label = "Points", type = "int", min = 50, max = 3000, default = 1000}
}

function Main()
    if Brush.triggerReleasedThisFrame then
        local path = Path:New()
        local R = Parameters.outerRadius -- Fixed outer circle radius
        local r = Parameters.innerRadius -- Rolling circle radius
        local d = Parameters.penOffset -- Pen offset
        local totalPoints = Parameters.points -- Number of points

        -- Ensure parameters are valid
        if r >= R then
            -- swap
            r, R = R, r
        end

        -- Calculate the number of cycles for a complete pattern
        local gcd = function(a, b)
            while b ~= 0 do
                local temp = b
                b = a % b
                a = temp
            end
            return a
        end
        local cycles = R / gcd(R, r)
        local t_end = Math.pi * cycles * Parameters.cycles

        -- Generate the spirograph pattern
        for i = 0, totalPoints do
            local t = i * t_end / totalPoints -- Spread points evenly over full cycles
            local x = (R - r) * Math:Cos(t) + d * Math:Cos((R - r) / r * t)
            local y = (R - r) * Math:Sin(t) - d * Math:Sin((R - r) / r * t)
            -- weave in a third dimension in an mathemarically interesting way
            local z = Math:Sin(t) * Math:Cos(t) * Math:Cos((R - r) / r * t) * Parameters.depth

            
            
            local position = Vector3:New(x, y, z)
            path:Insert(Transform:New(position))
        end

        -- Normalize path to fit within the canvas
        path:Normalize(2)
        return path
    end
end
