Settings = {
    description="Adjust brush controller geometry offset and rotation at runtime for calibration"
}

Parameters = {
    -- Offset parameters
    offsetX={label="Offset X", type="float", min=-0.5, max=0.5, default=0},
    offsetY={label="Offset Y", type="float", min=-1, max=0, default=-0.182},
    offsetZ={label="Offset Z", type="float", min=-0.5, max=1, default=0.379},
    -- Rotation parameters (euler angles)
    rotationX={label="Rotation X", type="float", min=-90, max=90, default=35},
    rotationY={label="Rotation Y", type="float", min=-90, max=90, default=0},
    rotationZ={label="Rotation Z", type="float", min=-90, max=90, default=0},
}

function Main()
    local offset = Vector3:New(Parameters.offsetX, Parameters.offsetY, Parameters.offsetZ)
    local rotation = Vector3:New(Parameters.rotationX, Parameters.rotationY, Parameters.rotationZ)

    Brush.controllerOffset = offset
    Brush.controllerRotation = rotation
end

function End()
    -- Reset to defaults when script is deactivated
    Brush:ResetControllerGeometry()
end
