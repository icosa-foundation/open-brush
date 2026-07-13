Settings = {
    description = "Draws a (p, q) torus knot with analytic ribbon-friendly orientation (tangent + torus radial)",
    previewType = "sphere"
}

Parameters = {
    p = {label = "P (Longitudinal wraps)", type = "int", min = 1, max = 32, default = 3},
    q = {label = "Q (Meridional wraps)", type = "int", min = 1, max = 32, default = 2},
    ratio = {label = "r / R", type = "float", min = 0.05, max = 0.95, default = 0.5},
    points = {label = "Points", type = "int", min = 100, max = 3000, default = 500}
}

function Main()
    if Brush.triggerReleasedThisFrame then
        local p = Parameters.p
        local q = Parameters.q
        local R = 1.0
        local r = Parameters.ratio
        local total = Parameters.points

        local path = Path:New()

        for i = 0, total do
            local t = (i / total) * (2 * Math.pi)

            local cospt = Math:Cos(p * t)
            local sinpt = Math:Sin(p * t)
            local cosqt = Math:Cos(q * t)
            local sinqt = Math:Sin(q * t)

            -- Position on (p, q) torus knot
            local x = (R + r * cosqt) * cospt
            local y = (R + r * cosqt) * sinpt
            local z = r * sinqt
            local pos = Vector3:New(x, y, z)

            -- Analytic tangent = d/dt of position
            local dx = -p * (R + r * cosqt) * sinpt - r * q * sinqt * cospt
            local dy =  p * (R + r * cosqt) * cospt - r * q * sinqt * sinpt
            local dz =  r * q * cosqt
            local forward = Vector3:New(dx, dy, dz).normalized

            -- Torus "radial" (from center circle to point), independent of scale
            -- Before normalization: r * (cosqt * cospt, cosqt * sinpt, sinqt)
            local radial = Vector3:New(cosqt * cospt, cosqt * sinpt, sinqt)

            -- Use radial projected onto plane orthogonal to tangent as the ribbon "up"
            local up = radial:ProjectOnPlane(forward)
            if up.magnitude < 1e-6 then
                -- Rare fallback if radial ≈ parallel to tangent
                up = Vector3:Cross(forward, Vector3.right)
                if up.magnitude < 1e-6 then
                    up = Vector3:Cross(forward, Vector3.up)
                end
            end
            up = up.normalized

            local rot = Rotation:LookRotation(forward, up)
            path:Insert(Transform:New(pos, rot))
        end

        -- Keep overall size fixed; shape controlled by p, q and ratio
        path:Normalize(2)
        return path
    end
end
