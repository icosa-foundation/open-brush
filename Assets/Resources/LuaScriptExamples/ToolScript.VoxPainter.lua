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
    modelSize={label="Model Size", type="int", min=16, max=255, default=96},
    gridSize={label="Grid Size", type="float", min=0.02, max=1.0, default=0.1},
    paletteIndex={label="Palette Index", type="int", min=1, max=255, default=5},
    mode={label="Mode", type="list", items=modeItems, default="Add"},
    centerX={label="Center X", type="int", min=0, max=255, default=48},
    centerY={label="Center Y", type="int", min=0, max=255, default=48},
    centerZ={label="Center Z", type="int", min=0, max=255, default=48},
    autoVisuals={label="Auto Visuals", type="toggle", default=true},
    optimizedMesh={label="Optimized Mesh", type="toggle", default=true},
    collider={label="Collider", type="toggle", default=false},
}

function Start()
    Vox:ClearSpawned()

    doc = Vox:New(Parameters.modelSize, Parameters.modelSize, Parameters.modelSize)
    model = doc.models[0]

    -- Simple vivid defaults so new voxels are easy to see.
    doc:SetPalette(1, 255, 80, 80)
    doc:SetPalette(2, 80, 220, 255)
    doc:SetPalette(3, 120, 255, 120)
    doc:SetPalette(4, 255, 230, 80)
    doc:SetPalette(5, 255, 120, 220)

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
    if not isInsideBounds(x, y, z) then
        return
    end

    local cellKey = x .. "," .. y .. "," .. z
    if cellKey == lastCellKey then
        return
    end

    applyCellEdit(x, y, z)
    lastCellKey = cellKey
end

function syncVisualOptions()
    local key = tostring(Parameters.autoVisuals) .. "|" .. tostring(Parameters.optimizedMesh) .. "|" .. tostring(Parameters.collider)
    if key == lastVisualKey then
        return
    end

    if Parameters.autoVisuals then
        doc:SetAutoVisuals(true, Parameters.optimizedMesh, Parameters.collider)
    else
        doc:SetAutoVisuals(false, Parameters.optimizedMesh, Parameters.collider)
        doc:Spawn(Parameters.optimizedMesh, Parameters.collider)
    end

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

    model:Set(x, y, z, Parameters.paletteIndex)
end

function brushToVoxel(pos)
    local gx = Math:Round(pos.x / Parameters.gridSize) + Parameters.centerX
    local gy = Math:Round(pos.y / Parameters.gridSize) + Parameters.centerY
    local gz = Math:Round(pos.z / Parameters.gridSize) + Parameters.centerZ
    return gx, gy, gz
end

function isInsideBounds(x, y, z)
    local maxIndex = Parameters.modelSize - 1
    return x >= 0 and y >= 0 and z >= 0 and x <= maxIndex and y <= maxIndex and z <= maxIndex
end

function End()
    lastCellKey = ""
end
