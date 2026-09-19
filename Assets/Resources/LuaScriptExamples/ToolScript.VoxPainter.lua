Settings = {
    description="Paint the current VOX widget. Point at another occupied widget to switch targets, or use New Model before placing another widget.",
    space="canvas"
}

Parameters = {
    modelSize={label="Model Size", type="int", min=16, max=256, default=128},
    gridSize={label="Voxel Size", type="float", min=0.02, max=1.0, default=0.1},
    mode={label="Mode", type="list", items={"Add", "Erase", "Paint"}, default="Add"},
    autoVisuals={label="Update While Drawing", type="toggle", default=true},
    optimizedMesh={label="Optimized Mesh", type="toggle", default=true},
    newModel={label="New Model", type="button", onclick="BeginNewModel"},
}

function ResetGesture()
    lastCell = nil
    lastMode = nil
end

function BeginNewModel()
    if doc ~= nil then doc:Refresh() end
    doc = nil
    model = nil
    createNewOnNextAdd = true
    ResetGesture()
end

function Start()
    doc = nil
    model = nil
    createNewOnNextAdd = false
    ResetGesture()
end

function Main()
    local position = Brush.position

    if doc ~= nil and not doc.isValid then
        doc = nil
        model = nil
        createNewOnNextAdd = false
        ResetGesture()
    end

    if Brush.triggerReleasedThisFrame then
        if doc ~= nil then doc:Refresh() end
        ResetGesture()
        return
    end

    if not Brush.triggerIsPressed then
        return
    end

    if Brush.triggerPressedThisFrame and not createNewOnNextAdd then
        local pointedModel = Vox:FindModelAt(position)
        if pointedModel ~= nil then
            model = pointedModel
            doc = model.document
            Parameters.gridSize = model.voxelSize
            ResetGesture()
        end
    end

    if model == nil then
        if Parameters.mode ~= "Add" then return end
        doc = Vox:NewWidget(Parameters.modelSize, Parameters.modelSize, Parameters.modelSize)
        model = doc:Model()
        model:PlaceAt(position, Parameters.gridSize)
        createNewOnNextAdd = false
    elseif not model:ContainsAt(position) then
        return
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
    doc = nil
    model = nil
end
