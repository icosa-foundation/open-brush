Settings = {
    description="Creates a missile that follows a ballistic trajectory",
    space="canvas"
}

Parameters = {
    initialSpeed={label="Initial Speed", type="float", min=0.1, max=5, default=2},
    gravity={label="Gravity", type="float", min=0.01, max=1, default=0.1},
}

function Main()
    if Brush.triggerPressedThisFrame then
        -- Store initial position, rotation, and velocity
        currentPos = Brush.position
        currentRotation = Brush.rotation
        velocity = Brush.direction * Parameters.initialSpeed
    elseif Brush.triggerIsPressed then
        -- Apply gravity to velocity
        velocity = velocity - Vector3.up * Parameters.gravity
        
        -- Update position based on velocity
        currentPos = currentPos + velocity
        
        -- Update rotation to face the direction of movement
        currentRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up)
        
        return Transform:New(currentPos, currentRotation)
    end
end