Settings = {
    description="Hold the trigger to paint voxels on an imported VOX widget, or start a new widget in empty space. Editable widget saving is still under development.",
    space="canvas"
}

Parameters = {
    modelSize={label="Model Size", type="int", min=16, max=256, default=128},
    gridSize={label="Voxel Size", type="float", min=0.02, max=1.0, default=0.1},
    mode={label="Mode", type="list", items={"Add", "Erase", "Paint"}, default="Add"},
    autoVisuals={label="Update While Drawing", type="toggle", default=true},
    optimizedMesh={label="Optimized Mesh", type="toggle", default=true},
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
        if doc ~= nil then doc:Refresh() end
        doc = nil
        model = nil
        origin = nil
        lastCell = nil
        lastMode = nil
        return
    end

    local position = Brush.position
    if model == nil then
        model = Vox:FindModelAt(position)
        if model == nil then
            if Parameters.mode ~= "Add" then return end
            doc = Vox:NewWidget(Parameters.modelSize, Parameters.modelSize, Parameters.modelSize)
            model = doc:Model()
            origin = position
            model:PlaceAt(origin, Parameters.gridSize)
        else
            doc = model.document
            Parameters.gridSize = model.voxelSize
        end
    end
    doc:SetAutoVisuals(Parameters.autoVisuals, Parameters.optimizedMesh, false)

    local cell = model:CanvasToVoxel(position)
    if lastCell ~= nil and cell:Equals(lastCell) and lastMode == Parameters.mode then
        return
    end

    if Parameters.mode == "Erase" then
        model:EraseAt(position)
    elseif Parameters.mode == "Paint" then
        model:RecolorAt(position, Brush.colorRgb)
    else
        model:PaintAt(position, Brush.colorRgb)
    end
    lastCell = cell
    lastMode = Parameters.mode
end

function End()
    if doc ~= nil then doc:Refresh() end
end
