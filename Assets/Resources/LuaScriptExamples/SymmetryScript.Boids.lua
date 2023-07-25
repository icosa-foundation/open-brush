Settings = {space = "pointer"}

Parameters = {
    copies = {label = "Number of copies", type="int", min=1, max=24, default=8},
    separationForce = {label = "Separation Amount", type="float", min=0, max=10, default = 1.5},
    alignmentForce = {label = "Alignment Amount", type="float", min=0, max=10, default = 5.0},
    --cohesionForce = {label = "Cohesion Amount", type="float", min=0, max=10, default = 1.0},
    originForce = {label = "Pointer Attraction", type="float", min=0, max=10, default = 5.0},
}

cohesionForce = 1.0 -- We need more room for parameters!

symmetryHueShift = require "symmetryHueShift"

local Boid = {}
Boid.__index = Boid

function Boid.new(position, velocity)
    local self = setmetatable({}, Boid)
    self.position = position
    self.velocity = velocity
    self.orientation = Rotation.lookRotation(self.velocity, Vector3.up)
    return self
end

function Boid:update(dt, boids)

    local sep, align, coh = self:calculateForces(boids)

    -- Calculate a force vector pointing towards the origin
    local attractionToOrigin = Vector3.zero:Subtract(self.position)
    attractionToOrigin = attractionToOrigin.normalized

    self.velocity = self.velocity:Add(sep:Multiply(separationForce))
    self.velocity = self.velocity:Add(align:Multiply(alignmentForce))
    self.velocity = self.velocity:Add(coh:Multiply(cohesionForce))

    -- Apply the attraction to origin force
    self.velocity = self.velocity:Add(attractionToOrigin:Multiply(originForce))

    self.velocity = self.velocity:ClampMagnitude(5)
    self.position = self.position:Add(self.velocity:Multiply(dt))
    self.orientation = Rotation.lookRotation(self.velocity, Vector3.up)
end

function Boid:calculateForces(boids)
    local separation = Vector3.zero
    local alignment = Vector3.zero
    local cohesion = Vector3.zero
    local count = 0

    for _, other in ipairs(boids) do
        if other ~= self then
            local direction = self.position:Subtract(other.position)
            local distanceSquared = direction.sqrMagnitude
            if distanceSquared < 25 and distanceSquared > 0 then
                separation = separation:Add(direction:Divide(distanceSquared))
                alignment = alignment:Add(other.velocity)
                cohesion = cohesion:Add(other.position)
                count = count + 1
            end
        end
    end

    if count > 0 then
        separation = separation:Divide(count)
        alignment = alignment:Divide(count).normalized
        cohesion = cohesion:Divide(count):Subtract(self.position).normalized
    end

    return separation, alignment, cohesion
end

local boids = {}

function Start()
    initialHsv = Brush.colorHsv
end

function randomFloat(x)
    -- Returns a random float in the range [-x, x]
    return -x + (2 * x) * Random.value
end

function Main()
    if Brush.triggerPressedThisFrame then
        -- Initialize boids with random positions and velocities
        boids = {}
        for i = 1, copies do
            local position = Random.insideUnitSphere:Multiply(0.25)
            local velocity = Random.insideUnitSphere:Multiply(0.1)
            boids[i] = Boid.new(position, velocity)
        end
        symmetryHueShift.generate(copies, initialHsv)
    end
    return updateBoids(0.01)
end

function updateBoids(dt)
    pointers = Path:New()
    for _, boid in ipairs(boids) do
        boid:update(dt, boids)
        pointers.Insert(Transform:New(boid.position, boid.orientation))
    end
    return pointers
end

