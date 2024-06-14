Settings = {space="pointer"}

function Main()
    return Path:New({
        -- Top bar
        Vector3:New(0, 0, 0),
        Vector3:New(1, 0, 0),
        Vector3:New(2, 0, 0),
        ----Sides
        Vector3:New(0, 1, 0),
        Vector3:New(2, 1, 0),
        ---- Bottom bar
        Vector3:New(0, 2, 0),
        Vector3:New(1, 2, 0),
        Vector3:New(2, 2, 0)
    })
end
