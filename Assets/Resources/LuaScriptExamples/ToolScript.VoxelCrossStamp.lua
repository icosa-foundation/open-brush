Settings = {
    description="Hold the trigger and touch a voxel model to stamp a small cross in the selected color. Release before stamping again. The pattern is clipped to the model's grid bounds.",
    space="canvas"
}

Parameters = {
    radius={label="Touch Radius", type="float", min=0, max=0.2, default=0.02},
}

local stamped = false

function Start()
    stamped = false
end

function Main()
    if Tool.isPreview then return end
    if not Brush.triggerIsPressed then
        stamped = false
        return
    end
    if stamped then return end

    local hit = Vox:FindHit(Brush.position, Parameters.radius)
    if hit == nil or not hit.model.isValid then return end
    stamped = true

    local model = hit.model
    local doc = model.document
    local cell = hit.cell
    local color = Brush.colorRgb

    -- Batch mesh updates while all seven cells are painted. PaintAt uses a diffuse
    -- palette color, and returns false for a full palette or an out-of-bounds cell.
    doc:SetAutoVisuals(false)
    local function paint(x, y, z)
        local position = model:VoxelToCanvas(Vector3:New(cell.x + x, cell.y + y, cell.z + z))
        model:PaintAt(position, color)
    end
    paint(0, 0, 0)
    paint(-1, 0, 0)
    paint(1, 0, 0)
    paint(0, -1, 0)
    paint(0, 1, 0)
    paint(0, 0, -1)
    paint(0, 0, 1)
    doc:SetAutoVisuals(true)
end
