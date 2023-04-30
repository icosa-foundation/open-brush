vector3 = {}
vector3.__index = vector3

local function newvector3( x, y, z )
    return setmetatable( { x = x or 0, y = y or 0, z = z or 0 }, vector3 )
end

function isvector( vTbl )
    return getmetatable( vTbl ) == vector3
end

function vector3.__unm( vTbl )
    return newvector3( -vTbl.x, -vTbl.y, -vTbl.z )
end

function vector3.__add( a, b )
    return newvector3( a.x + b.x, a.y + b.y, a.z + b.z )
end

function vector3.__sub( a, b )
    return newvector3( a.x - b.x, a.y - b.y, a.z - b.z )
end

function vector3.__mul( a, b )
    if type( a ) == "number" then
        return newvector3( a * b.x, a * b.y, a * b.z )
    elseif type( b ) == "number" then
        return newvector3( a.x * b, a.y * b, a.z * b )
    else
        return newvector3( a.x * b.x, a.y * b.y, a.z * b.z )
    end
end

function vector3.__div( a, b )
    return newvector3( a.x / b, a.y / b, a.z / b )
end

function vector3.__eq( a, b )
    return a.x == b.x and a.y == b.y and a.z == b.z
end

function vector3:__tostring()
    return "(" .. self.x .. ", " .. self.y .. ", " .. self.z .. ")"
end


function vector3:angle(a, b)
    return __Vector3.Angle(a, b)
end
function vector3:clampMagnitude(v, maxLength)
    return __Vector3.ClampMagnitude(v, maxLength)
end
function vector3:cross(a, b)
    return __Vector3.Cross(a, b)
end
function vector3:distance(a, b)
    return __Vector3.Distance(a, b)
end
function vector3:magnitude()
    return __Vector3.Magnitude(self)
end
function vector3:sqrMagnitude()
    return __Vector3.SqrMagnitude(self)
end
function vector3:dot(a, b)
    return __Vector3.Dot(a, b)
end
function vector3:lerp(a, b, t)
    return __Vector3.Lerp(a, b, t)
end
function vector3:lerpUnclamped(a, b, t)
    return __Vector3.LerpUnclamped(a, b, t)
end
function vector3:max(a, b)
    return __Vector3.Max(a, b)
end
function vector3:min(a, b)
    return __Vector3.Min(a, b)
end
function vector3:moveTowards(current, target, maxDistanceDelta)
    return __Vector3.MoveTowards(current, target, maxDistanceDelta)
end
function vector3:normalize()
    return __Vector3.Normalize(self)
end
function vector3:project(a, b)
    return __Vector3.Project(a, b)
end
function vector3:projectOnPlane(vector, planeNormal)
    return __Vector3.ProjectOnPlane(vector, planeNormal)
end
function vector3:reflect(a, b)
    return __Vector3.Reflect(a, b)
end
function vector3:rotateTowards(current, target, maxRadiansDelta, maxMagnitudeDelta)
    Vector3.RotateTowards(current, target, maxMagnitudeDelta, maxMagnitudeDelta)
end
function vector3:scale(a, b)
    return __Vector3.Scale(a, b)
end
function vector3:signedAngle(from, to, axis)
    return __Vector3.SignedAngle(from, to, axis)
end
function vector3:slerp(a, b, t)
    return __Vector3.Lerp(a, b, t)
end
function vector3:slerpUnclamped(a, b, t)
    return __Vector3.Lerp(a, b, t)
end
function vector3:smoothDamp(current, target, currentVelocity, smoothTime, maxSpeed, deltaTime)
    return __Vector3.SmoothDamp(current, target, currentVelocity, smoothTime, maxSpeed, deltaTime)
end

function vector3:back()
    return __Vector3.back;
end
function vector3:down()
    return __Vector3.down;
end
function vector3:forward()
    return __Vector3.forward;
end
function vector3:left()
    return __Vector3.left;
end
function vector3:negativeInfinity()
    return __Vector3.negativeInfinity;
end
function vector3:one()
    return __Vector3.one;
end
function vector3:positiveInfinity()
    return __Vector3.positiveInfinity;
end
function vector3:right()
    return __Vector3.right;
end
function vector3:up()
    return __Vector3.up;
end
function vector3:zero()
    return __Vector3.zero;
end

return setmetatable( vector3, { __call = function( _, ... ) return newvector3( ... ) end } )