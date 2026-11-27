Settings = {
    description="Release the trigger to stamp a random, bilaterally symmetrical voxel space creature. Each creature remains editable and is saved with the sketch.",
    space="canvas"
}

Parameters = {
    voxelSize={label="Voxel Size", type="float", min=0.04, max=0.3, default=0.12},
    density={label="Body Density", type="float", min=0.25, max=0.85, default=0.55},
}

local function setMirrored(model, x, y, z, paletteIndex)
    model:SetVoxel(x, y, z, paletteIndex)
    model:SetVoxel(7 - x, y, z, paletteIndex)
end

local function makeCreature(position)
    local doc = Vox:NewWidget(8, 8, 4)
    local model = doc:Model()
    doc:SetAutoVisuals(false, true)

    local red = Random:Range(64, 255)
    local green = Random:Range(64, 255)
    local blue = Random:Range(64, 255)
    doc:SetPalette(1, red, green, blue, 255)
    doc:SetPalette(2, 255 - red, 255 - green, 255 - blue, 255)
    doc:SetPalette(3, 255, 255, 210, 255)

    -- Generate one half of a layered body, then mirror it across the X axis.
    for y = 2, 6 do
        for x = 0, 3 do
            local chance = Parameters.density
            if x == 3 then chance = chance + 0.2 end
            if y == 6 then chance = chance - 0.15 end

            if Random.value < chance then
                setMirrored(model, x, y, 0, 1)
                setMirrored(model, x, y, 1, 1)
                if Random.value < 0.35 then
                    setMirrored(model, x, y, 2, 2)
                end
            end
        end
    end

    -- A small fixed scaffold keeps every random result recognizably creature-like.
    for y = 3, 5 do
        setMirrored(model, 3, y, 0, 1)
        setMirrored(model, 3, y, 1, 1)
    end
    setMirrored(model, 2, 7, 0, 2)
    setMirrored(model, 0, 4, 0, 2)
    setMirrored(model, 1, 1, 0, 1)
    setMirrored(model, 3, 1, 0, 1)
    setMirrored(model, 1, 0, 0, 2)
    setMirrored(model, 2, 5, 2, 3)
    setMirrored(model, 2, 5, 3, 3)

    model:PlaceAt(position, Parameters.voxelSize)
    doc:Refresh()
end

function Main()
    if Brush.triggerReleasedThisFrame then
        makeCreature(Brush.position)
    end
end
