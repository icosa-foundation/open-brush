Settings = {
    description="Hold the trigger and move to paint voxels. First press on an existing voxel to edit its model, or in empty space to start a new grid. Voxels are saved with the sketch.",
    space="canvas"
}

Parameters = {
    modelSize={label="Model Size", type="int", min=16, max=255, default=128},
    gridSize={label="Voxel Size", type="float", min=0.02, max=1.0, default=0.1},
    mode={label="Mode", type="list", items={"Add", "Erase", "Toggle"}, default="Add"},
    autoVisuals={label="Update While Drawing", type="toggle", default=true},
    optimizedMesh={label="Optimized Mesh", type="toggle", default=true},
    collider={label="Collider", type="toggle", default=false},
}

function Start()
    doc = nil
    model = nil
    origin = nil
    lastCell = nil
    lastMode = nil
end

function Main()
    if Brush.triggerReleasedThisFrame or not Brush.triggerIsPressed then
        if origin ~= nil then doc:Refresh() end
        lastCell = nil
        return
    end

    local position = Brush.position
    if model == nil then
        model = Vox:FindModelAt(position)
        if model == nil then
            if Parameters.mode == "Erase" then return end
            doc = Vox:New(Parameters.modelSize, Parameters.modelSize, Parameters.modelSize)
            model = doc.models[0]
            origin = position
        else
            doc = model.document
            origin = model:VoxelToCanvas(model.centerVoxel)
            Parameters.gridSize = model.voxelSize
        end
    end
    model:PlaceAt(origin, Parameters.gridSize)
    doc:SetAutoVisuals(Parameters.autoVisuals, Parameters.optimizedMesh, Parameters.collider)

    local cell = model:CanvasToVoxel(position)
    if lastCell ~= nil and cell:Equals(lastCell) and lastMode == Parameters.mode then
        return
    end

    if Parameters.mode == "Erase" then
        model:EraseAt(position)
    elseif Parameters.mode == "Toggle" then
        model:ToggleAt(position, Brush.colorRgb)
    else
        model:PaintAt(position, Brush.colorRgb)
    end
    lastCell = cell
    lastMode = Parameters.mode
end

function End()
    if origin ~= nil then doc:Refresh() end
end
