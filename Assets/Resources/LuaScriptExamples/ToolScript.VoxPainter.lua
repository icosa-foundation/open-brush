Settings = {
    description="Interactive voxel painting tool using runtime Vox API",
    space="canvas"
}

modeItems = {
    "Add",
    "Erase",
    "Toggle",
}

Parameters = {
    modelSize={label="Model Size", type="int", min=16, max=255, default=128},
    gridSize={label="Grid Size", type="float", min=0.02, max=1.0, default=0.1},
    mode={label="Mode", type="list", items=modeItems, default="Add"},
    autoVisuals={label="Auto Visuals", type="toggle", default=true},
    optimizedMesh={label="Optimized Mesh", type="toggle", default=true},
    collider={label="Collider", type="toggle", default=false},
}

function Start()
    Vox:ClearSpawned()

    doc = Vox:New(Parameters.modelSize, Parameters.modelSize, Parameters.modelSize)
    model = doc.models[0]

    -- Spawn at current brush position (snapped to grid) so painting maps 1:1
    local spawnX = Math:Round(Brush.position.x / Parameters.gridSize) * Parameters.gridSize
    local spawnY = Math:Round(Brush.position.y / Parameters.gridSize) * Parameters.gridSize
    local spawnZ = Math:Round(Brush.position.z / Parameters.gridSize) * Parameters.gridSize
    doc:SpawnAt(spawnX, spawnY, spawnZ)

    -- Fill palette with a spectrum of colors (indices 1-255)
    fillPalette()
    
    -- Store palette colors for distance calculations
    paletteColors = {}
    for i = 1, 255 do
        paletteColors[i] = getPaletteColor(i)
    end

    lastCellKey = ""
    lastVisualKey = ""
    syncVisualOptions()
end

function Main()
    syncVisualOptions()

    if Brush.triggerReleasedThisFrame then
        lastCellKey = ""
        return
    end

    if not Brush.triggerIsPressed then
        return
    end

    local x, y, z = brushToVoxel(Brush.position)
    local cellKey = x .. "," .. y .. "," .. z
    if cellKey == lastCellKey then
        return
    end

    applyCellEdit(x, y, z)
    lastCellKey = cellKey
end

function syncVisualOptions()
    local key = tostring(Parameters.optimizedMesh) .. "|" .. tostring(Parameters.collider)
    if key == lastVisualKey then
        return
    end

    doc:SetAutoVisuals(true, Parameters.optimizedMesh, Parameters.collider)
    doc:Spawn(Parameters.optimizedMesh, Parameters.collider)

    lastVisualKey = key
end

function applyCellEdit(x, y, z)
    if Parameters.mode == "Erase" then
        model:Remove(x, y, z)
        return
    end

    if Parameters.mode == "Toggle" then
        if model:Remove(x, y, z) then
            return
        end
    end

    model:Set(x, y, z, findNearestPaletteIndex(Brush.color))
end

function brushToVoxel(pos)
    -- Transform world position to VOX model local space
    -- The VOX model is rotated -90° on X axis when displayed:
    --   VOX X → Unity X
    --   VOX Y → Unity Z  
    --   VOX Z → Unity -Y
    -- So to map Unity brush position to VOX voxel:
    --   Unity X → VOX X
    --   Unity Z → VOX Y
    --   Unity -Y → VOX Z
    local gx = Math:Round(pos.x / Parameters.gridSize)
    local gy = Math:Round(pos.z / Parameters.gridSize)
    local gz = Math:Round(-pos.y / Parameters.gridSize)
    return gx, gy, gz
end

function End()
    lastCellKey = ""
end

-- Fill palette with a full spectrum of colors
function fillPalette()
    -- First 32 colors: grayscale (black to white)
    for i = 0, 31 do
        local val = i * 8
        doc:SetPalette(i + 1, val, val, val)
    end
    
    -- Remaining 223 colors: HSL spectrum
    for i = 0, 222 do
        local hue = (i / 223) * 360
        local sat = 0.8
        local light = 0.5
        local r, g, b = hslToRgb(hue, sat, light)
        doc:SetPalette(i + 33, r, g, b)
    end
end

-- Convert HSL to RGB
function hslToRgb(h, s, l)
    h = h / 360
    local r, g, b
    
    if s == 0 then
        r, g, b = l, l, l
    else
        local function hue2rgb(p, q, t)
            if t < 0 then t = t + 1 end
            if t > 1 then t = t - 1 end
            if t < 1/6 then return p + (q - p) * 6 * t end
            if t < 1/2 then return q end
            if t < 2/3 then return p + (q - p) * (2/3 - t) * 6 end
            return p
        end
        
        local q = l < 0.5 and l * (1 + s) or l + s - l * s
        local p = 2 * l - q
        r = hue2rgb(p, q, h + 1/3)
        g = hue2rgb(p, q, h)
        b = hue2rgb(p, q, h - 1/3)
    end
    
    return Math:Round(r * 255), Math:Round(g * 255), Math:Round(b * 255)
end

-- Get palette color (returns r, g, b table)
function getPaletteColor(index)
    -- Since we can't read back from the doc, we recalculate
    if index <= 32 then
        local val = (index - 1) * 8
        return {r = val, g = val, b = val}
    else
        local hue = ((index - 33) / 223) * 360
        local r, g, b = hslToRgb(hue, 0.8, 0.5)
        return {r = r, g = g, b = b}
    end
end

-- Find nearest palette index to a given RGB color
function findNearestPaletteIndex(color)
    local minDist = 999999
    local nearestIndex = 1
    
    for i = 1, 255 do
        local p = paletteColors[i]
        local dr = color.r - p.r
        local dg = color.g - p.g
        local db = color.b - p.b
        local dist = dr * dr + dg * dg + db * db
        
        if dist < minDist then
            minDist = dist
            nearestIndex = i
        end
    end
    
    return nearestIndex
end
