Settings = {
    description="Demonstrates runtime VOX creation/editing/export with the new Vox Lua API"
}

local hasRun = false

function Main()
    if hasRun then
        return
    end

    Vox:ClearSpawned()

    local doc = Vox:NewScene(16, 16, 16, true, true)
    local model = doc:FindModel("model_0")

    -- Optional second model to demonstrate list access and AddModel.
    local detail = doc:AddModel(16, 16, 16, "detail")

    doc:SetPalette(5, 255, 96, 64, 255)
    doc:SetPalette(6, 96, 180, 255, 255)

    model:SetVoxel(1, 0, 1, 5)
    model:SetVoxel(2, 0, 1, 5)
    model:SetVoxel(3, 0, 1, 5)
    model:SetVoxel(3, 1, 1, 6)
    model:SetVoxel(3, 2, 1, 6)

    model:MoveVoxel(2, 0, 1, 2, 1, 1, true)
    model:RemoveVoxel(1, 0, 1)

    detail:SetVoxel(0, 0, 0, 6)

    local stats = model:MeshStats(true)
    print("Vox model stats mode=" .. stats.mode ..
          " vertices=" .. stats.vertexCount ..
          " indices=" .. stats.triangleIndexCount)

    local b64 = doc:ExportBase64()
    print("Exported VOX base64 length: " .. string.len(b64))

    hasRun = true
end
