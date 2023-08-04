
---Properties for class App

---@class App
t = Class()

---@type number
App.time = nil

---@type number
App.frames = nil

---@type number
App.currentScale = nil

---@type string
App.environment = nil

---@type string
App.clipboardText = nil


---Methods for type App

---@param active boolean
---@return boolean
function App:Physics(active) end


function App:Undo() end


function App:Redo() end

---@param url string
function App:AddListener(url) end


function App:ResetPanels() end


function App:ShowScriptsFolder() end


function App:ShowExportFolder() end


function App:ShowSketchesFolder() end

---@param active boolean
function App:StraightEdge(active) end

---@param active boolean
function App:AutoOrient(active) end

---@param active boolean
function App:ViewOnly(active) end

---@param active boolean
function App:AutoSimplify(active) end

---@param active boolean
function App:Disco(active) end

---@param active boolean
function App:Profiling(active) end

---@param active boolean
function App:PostProcessing(active) end


function App:DraftingVisible() end


function App:DraftingTransparent() end


function App:DraftingHidden() end

---@param active boolean
function App:Watermark(active) end

---@param path string
---@return string
function App:ReadFile(path) end

---@param message string
function App:Error(message) end

---@param fontData string
function App:SetFont(fontData) end

---@param tr Transform
---@param filename string
---@param width number
---@param height number
---@param superSampling number
function App:TakeSnapshot(tr, filename, width, height, superSampling) end

---@param tr Transform
---@param filename string
---@param width number
function App:Take360Snapshot(tr, filename, width) end

---Properties for class Brush

---@class Brush
t = Class()

---@type number
Brush.timeSincePressed = nil

---@type number
Brush.timeSinceReleased = nil

---@type boolean
Brush.triggerIsPressed = nil

---@type boolean
Brush.triggerPressedThisFrame = nil

---@type boolean
Brush.triggerReleasedThisFrame = nil

---@type number
Brush.distanceMoved = nil

---@type number
Brush.distanceDrawn = nil

---@type Vector3
Brush.position = nil

---@type Rotation
Brush.rotation = nil

---@type Vector3
Brush.direction = nil

---@type number
Brush.size = nil

---@type number
Brush.pressure = nil

---@type string
Brush.type = nil

---@type string[]
Brush.types = nil

---@type number
Brush.speed = nil

---@type Color
Brush.colorRgb = nil

---@type Vector3
Brush.colorHsv = nil

---@type string
Brush.colorHtml = nil

---@type Color
Brush.lastColorPicked = nil

---@type Vector3
Brush.LastColorPickedHsv = nil

---@type Path
Brush.currentPath = nil


---Methods for type Brush


function Brush:JitterColor() end

---@param size number
function Brush:ResizeHistory(size) end

---@param size number
function Brush:SetHistorySize(size) end

---@param back number
---@return Vector3
function Brush:GetPastPosition(back) end

---@param back number
---@return Rotation
function Brush:GetPastRotation(back) end

---@param active boolean
function Brush:ForcePaintingOn(active) end

---@param active boolean
function Brush:ForcePaintingOff(active) end


function Brush:ForceNewStroke() end

---Properties for class CameraPath

---@class CameraPath
t = Class()

---@type number
CameraPath.index = nil

---@type Layer
CameraPath.layer = nil

---@type Group
CameraPath.group = nil

---@type boolean
CameraPath.active = nil

---@type Transform
CameraPath.transform = nil

---@type Vector3
CameraPath.position = nil

---@type Rotation
CameraPath.rotation = nil

---@type number
CameraPath.scale = nil


---Methods for type CameraPath


function CameraPath:RenderActivePath() end


function CameraPath:ShowAll() end


function CameraPath:HideAll() end

---@param active boolean
function CameraPath:PreviewActivePath(active) end


function CameraPath:Delete() end


---@return CameraPath
function CameraPath:New() end

---@param path Path
---@param looped boolean
---@return CameraPath
function CameraPath:FromPath(path, looped) end

---@param step number
---@return Path
function CameraPath:AsPath(step) end


---@return CameraPath
function CameraPath:Duplicate() end

---@param position Vector3
---@param rotation Rotation
---@param smoothing number
---@return number
function CameraPath:InsertPosition(position, rotation, smoothing) end

---@param t number
---@param rotation Rotation
---@param smoothing number
---@return number
function CameraPath:InsertPositionAtTime(t, rotation, smoothing) end

---@param position Vector3
---@param rotation Rotation
---@return number
function CameraPath:InsertRotation(position, rotation) end

---@param t number
---@param rotation Rotation
---@return number
function CameraPath:InsertRotationAtTime(t, rotation) end

---@param position Vector3
---@param fov number
---@return number
function CameraPath:InsertFov(position, fov) end

---@param t number
---@param fov number
---@return number
function CameraPath:InsertFovAtTime(t, fov) end

---@param position Vector3
---@param speed number
---@return number
function CameraPath:InsertSpeed(position, speed) end

---@param t number
---@param speed number
---@return number
function CameraPath:InsertSpeedAtTime(t, speed) end

---@param position Vector3
---@param rotation Rotation
---@param smoothing number
---@param atStart boolean
function CameraPath:Extend(position, rotation, smoothing, atStart) end


function CameraPath:Loop() end


function CameraPath:RecordActivePath() end

---@param time number
---@param loop boolean
---@param pingpong boolean
---@return Transform
function CameraPath:Sample(time, loop, pingpong) end

---@param tolerance number
---@param smoothing number
---@return CameraPath
function CameraPath:Simplify(tolerance, smoothing) end

---Properties for class Color

---@class Color
t = Class()

---@type number
Color.Item = nil

---@type number
Color.r = nil

---@type number
Color.g = nil

---@type number
Color.b = nil

---@type number
Color.a = nil

---@type number
Color.grayscale = nil

---@type Color
Color.gamma = nil

---@type Color
Color.linear = nil

---@type number
Color.maxColorComponent = nil

---@type string
Color.html = nil

---@type number
Color.greyscale = nil

---@type Vector3
Color.hsv = nil

---@type Color
Color.black = nil

---@type Color
Color.blue = nil

---@type Color
Color.cyan = nil

---@type Color
Color.gray = nil

---@type Color
Color.green = nil

---@type Color
Color.grey = nil

---@type Color
Color.magenta = nil

---@type Color
Color.red = nil

---@type Color
Color.white = nil

---@type Color
Color.yellow = nil


---Methods for type Color

---@param r number
---@param g number
---@param b number
---@return Color
function Color:New(r, g, b) end

---@param html string
---@return Color
function Color:New(html) end

---@param a Color
---@param b Color
---@param t number
---@return Color
function Color:Lerp(a, b, t) end

---@param a Color
---@param b Color
---@param t number
---@return Color
function Color:LerpUnclamped(a, b, t) end

---@param h number
---@param s number
---@param v number
---@return Color
function Color:HsvToRgb(h, s, v) end

---@param hsv Vector3
---@return Color
function Color:HsvToRgb(hsv) end

---@param other Color
---@return Color
function Color:Add(other) end

---@param r number
---@param g number
---@param b number
---@return Color
function Color:Add(r, g, b) end

---@param other Color
---@return Color
function Color:Subtract(other) end

---@param r number
---@param g number
---@param b number
---@return Color
function Color:Subtract(r, g, b) end

---@param value number
---@return Color
function Color:Multiply(value) end

---@param r number
---@param g number
---@param b number
---@return Color
function Color:Multiply(r, g, b) end

---@param value number
---@return Color
function Color:Divide(value) end

---@param other Color
---@return boolean
function Color:NotEquals(other) end

---@param r number
---@param g number
---@param b number
---@return boolean
function Color:NotEquals(r, g, b) end
---Methods for type Easing

---@param t number
---@return number
function Easing:Linear(t) end

---@param t number
---@return number
function Easing:InQuad(t) end

---@param t number
---@return number
function Easing:OutQuad(t) end

---@param t number
---@return number
function Easing:InOutQuad(t) end

---@param t number
---@return number
function Easing:InCubic(t) end

---@param t number
---@return number
function Easing:OutCubic(t) end

---@param t number
---@return number
function Easing:InOutCubic(t) end

---@param t number
---@return number
function Easing:InQuart(t) end

---@param t number
---@return number
function Easing:OutQuart(t) end

---@param t number
---@return number
function Easing:InOutQuart(t) end

---@param t number
---@return number
function Easing:InQuint(t) end

---@param t number
---@return number
function Easing:OutQuint(t) end

---@param t number
---@return number
function Easing:InOutQuint(t) end

---@param t number
---@return number
function Easing:InSine(t) end

---@param t number
---@return number
function Easing:OutSine(t) end

---@param t number
---@return number
function Easing:InOutSine(t) end

---@param t number
---@return number
function Easing:InExpo(t) end

---@param t number
---@return number
function Easing:OutExpo(t) end

---@param t number
---@return number
function Easing:InOutExpo(t) end

---@param t number
---@return number
function Easing:InCirc(t) end

---@param t number
---@return number
function Easing:OutCirc(t) end

---@param t number
---@return number
function Easing:InOutCirc(t) end

---@param t number
---@return number
function Easing:InElastic(t) end

---@param t number
---@return number
function Easing:OutElastic(t) end

---@param t number
---@return number
function Easing:InOutElastic(t) end

---@param t number
---@return number
function Easing:InBack(t) end

---@param t number
---@return number
function Easing:OutBack(t) end

---@param t number
---@return number
function Easing:InOutBack(t) end

---@param t number
---@return number
function Easing:InBounce(t) end

---@param t number
---@return number
function Easing:OutBounce(t) end

---@param t number
---@return number
function Easing:InOutBounce(t) end

---Properties for class Group

---@class Group
t = Class()

---@type ImageList
Group.images = nil

---@type VideoList
Group.videos = nil

---@type ModelList
Group.models = nil

---@type GuideList
Group.guides = nil

---@type CameraPathList
Group.cameraPaths = nil


---Methods for type Group


---@return Group
function Group:New() end

---@param widget Image
function Group:Add(widget) end

---@param widget Video
function Group:Add(widget) end

---@param widget Model
function Group:Add(widget) end

---@param widget Guide
function Group:Add(widget) end

---@param widget CameraPath
function Group:Add(widget) end

---@param widget GrabWidget
function Group:_Add(widget) end

---@param widget GrabWidget
function Group:Add(widget) end

---Properties for class Guide

---@class Guide
t = Class()

---@type number
Guide.index = nil

---@type Layer
Guide.layer = nil

---@type Group
Guide.group = nil

---@type Transform
Guide.transform = nil

---@type Vector3
Guide.position = nil

---@type Rotation
Guide.rotation = nil

---@type number
Guide.scale = nil


---Methods for type Guide

---@param transform Transform
---@return Guide
function Guide:NewCube(transform) end

---@param transform Transform
---@return Guide
function Guide:NewSphere(transform) end

---@param transform Transform
---@return Guide
function Guide:NewCapsule(transform) end

---@param transform Transform
---@return Guide
function Guide:NewCone(transform) end

---@param transform Transform
---@return Guide
function Guide:NewEllipsoid(transform) end

---@param transform Transform
---@param model Model
---@return Guide
function Guide:NewCustom(transform, model) end


function Guide:Select() end


function Guide:Delete() end

---@param scale Vector3
function Guide:Scale(scale) end
---Methods for type Headset

---@param size number
function Headset:ResizeHistory(size) end

---@param size number
function Headset:SetHistorySize(size) end

---@param back number
---@return Vector3
function Headset:PastPosition(back) end

---@param back number
---@return Rotation
function Headset:PastRotation(back) end

---Properties for class Image

---@class Image
t = Class()

---@type number
Image.index = nil

---@type Layer
Image.layer = nil

---@type Group
Image.group = nil

---@type Transform
Image.transform = nil

---@type Vector3
Image.position = nil

---@type Rotation
Image.rotation = nil

---@type number
Image.scale = nil


---Methods for type Image

---@param location string
---@return Image
function Image:Import(location) end


function Image:Select() end


function Image:Delete() end

---@param depth number
---@param color Color
function Image:Extrude(depth, color) end


---@return string
function Image:FormEncode() end

---@param base64 string
---@param filename string
---@return string
function Image:SaveBase64(base64, filename) end

---Properties for class ImageList

---@class ImageList
t = Class()

---@type Image
ImageList.lastSelected = nil

---@type Image
ImageList.last = nil

---@type Image
ImageList.Item = nil

---@type number
ImageList.count = nil



---Properties for class Layer

---@class Layer
t = Class()

---@type StrokeList
Layer.strokes = nil

---@type ImageList
Layer.images = nil

---@type VideoList
Layer.videos = nil

---@type ModelList
Layer.models = nil

---@type GuideList
Layer.guides = nil

---@type CameraPathList
Layer.cameraPaths = nil

---@type System.Collections.Generic.List`1[Group]
Layer.groups = nil

---@type number
Layer.index = nil

---@type boolean
Layer.active = nil

---@type Transform
Layer.transform = nil

---@type Vector3
Layer.position = nil

---@type Rotation
Layer.rotation = nil

---@type number
Layer.scale = nil


---Methods for type Layer


---@return Layer
function Layer:New() end


function Layer:CenterPivot() end


function Layer:ShowPivot() end


function Layer:HidePivot() end


function Layer:Clear() end


function Layer:Delete() end


---@return Layer
function Layer:Squash() end

---@param destinationLayer Layer
function Layer:SquashTo(destinationLayer) end


function Layer:Show() end


function Layer:Hide() end

---@param desc BrushDescriptor
---@return System.Collections.Generic.IEnumerable`1[Batch]
function Layer:_GetBatches(desc) end

---@param brushType string
---@return BrushDescriptor
function Layer:_GetDesc(brushType) end

---@param brushType string
---@param parameter string
---@param value number
function Layer:SetShaderFloat(brushType, parameter, value) end

---@param brushType string
---@param parameter string
---@param color Color
function Layer:SetShaderColor(brushType, parameter, color) end

---@param brushType string
---@param parameter string
---@param image Image
function Layer:SetShaderTexture(brushType, parameter, image) end

---@param brushType string
---@param parameter string
---@param x number
---@param y number
---@param z number
---@param w number
function Layer:SetShaderVector(brushType, parameter, x, y, z, w) end

---Properties for class Math

---@class Math
t = Class()

---@type number
Math.deg2Rad = nil

---@type number
Math.epsilon = nil

---@type number
Math.positiveInfinity = nil

---@type number
Math.negativeInfinity = nil

---@type number
Math.pi = nil

---@type number
Math.rad2Deg = nil


---Methods for type Math

---@param f number
---@return number
function Math:Abs(f) end

---@param f number
---@return number
function Math:Acos(f) end

---@param a number
---@param b number
---@return boolean
function Math:Approximately(a, b) end

---@param f number
---@return number
function Math:Asin(f) end

---@param f number
---@return number
function Math:Atan(f) end

---@param y number
---@param x number
---@return number
function Math:Atan2(y, x) end

---@param f number
---@return number
function Math:Ceil(f) end

---@param f number
---@param min number
---@param max number
---@return number
function Math:Clamp(f, min, max) end

---@param value number
---@return number
function Math:Clamp01(value) end

---@param value number
---@return number
function Math:ClosestPowerOfTwo(value) end

---@param f number
---@return number
function Math:Cos(f) end

---@param a number
---@param b number
---@return number
function Math:DeltaAngle(a, b) end

---@param power number
---@return number
function Math:Exp(power) end

---@param f number
---@return number
function Math:Floor(f) end

---@param min number
---@param max number
---@param t number
---@return number
function Math:InverseLerp(min, max, t) end

---@param value number
---@return boolean
function Math:IsPowerOfTwo(value) end

---@param min number
---@param max number
---@param t number
---@return number
function Math:Lerp(min, max, t) end

---@param min number
---@param max number
---@param a number
---@return number
function Math:LerpAngle(min, max, a) end

---@param min number
---@param max number
---@param t number
---@return number
function Math:LerpUnclamped(min, max, t) end

---@param f number
---@param b number
---@return number
function Math:Log(f, b) end

---@param f number
---@return number
function Math:Log10(f) end

---@param a number
---@param b number
---@return number
function Math:Max(a, b) end

---@param values number[]
---@return number
function Math:Max(values) end

---@param a number
---@param b number
---@return number
function Math:Min(a, b) end

---@param values number[]
---@return number
function Math:Min(values) end

---@param current number
---@param target number
---@param maxDelta number
---@return number
function Math:MoveTowards(current, target, maxDelta) end

---@param value number
---@return number
function Math:NextPowerOfTwo(value) end

---@param x number
---@param y number
---@return number
function Math:PerlinNoise(x, y) end

---@param t number
---@param length number
---@return number
function Math:PingPong(t, length) end

---@param f number
---@param p number
---@return number
function Math:Pow(f, p) end

---@param t number
---@param length number
---@return number
function Math:Repeater(t, length) end

---@param f number
---@return number
function Math:Round(f) end

---@param f number
---@return number
function Math:Sign(f) end

---@param f number
---@return number
function Math:Sin(f) end

---@param f number
---@return number
function Math:Sqrt(f) end

---@param from number
---@param to number
---@param t number
---@return number
function Math:SmoothStep(from, to, t) end

---@param f number
---@return number
function Math:Tan(f) end

---@param f number
---@return number
function Math:Sinh(f) end

---@param f number
---@return number
function Math:Cosh(f) end

---@param f number
---@return number
function Math:Tanh(f) end

---Properties for class Model

---@class Model
t = Class()

---@type number
Model.index = nil

---@type Layer
Model.layer = nil

---@type Group
Model.group = nil

---@type Transform
Model.transform = nil

---@type Vector3
Model.position = nil

---@type Rotation
Model.rotation = nil

---@type number
Model.scale = nil


---Methods for type Model

---@param filename string
---@return Model
function Model:Import(filename) end


function Model:Select() end


function Model:Delete() end

---Properties for class MultiPath

---@class MultiPath
t = Class()

---@type number
MultiPath.count = nil

---@type number
MultiPath.pointCount = nil


---Methods for type MultiPath


---@return MultiPath
function MultiPath:New() end

---@param pathList Path[]
---@return MultiPath
function MultiPath:New(pathList) end


function MultiPath:Draw() end

---@param text string
---@return MultiPath
function MultiPath:FromText(text) end

---@param path Path
function MultiPath:Insert(path) end

---@param path Path
---@param index number
function MultiPath:Insert(path, index) end

---@param transform Transform
function MultiPath:InsertPoint(transform) end

---@param transform Transform
---@param pathIndex number
---@param pointIndex number
function MultiPath:InsertPoint(transform, pathIndex, pointIndex) end

---@param transform Transform
function MultiPath:TransformBy(transform) end

---@param amount Vector3
function MultiPath:TranslateBy(amount) end

---@param rotation Rotation
function MultiPath:RotateBy(rotation) end

---@param scale Vector3
function MultiPath:ScaleBy(scale) end


function MultiPath:Center() end

---@param size number
function MultiPath:Normalize(size) end

---@param spacing number
function MultiPath:Resample(spacing) end


---@return Path
function MultiPath:Join() end


---@return Path
function MultiPath:Longest() end

---Properties for class Path

---@class Path
t = Class()

---@type number
Path.count = nil

---@type Transform
Path.Item = nil

---@type Transform
Path.last = nil


---Methods for type Path


---@return Path
function Path:New() end

---@param transformList Transform[]
---@return Path
function Path:New(transformList) end

---@param positionList Vector3[]
---@return Path
function Path:New(positionList) end

---@param index number
---@return Vector3
function Path:GetDirection(index) end

---@param index number
---@return Vector3
function Path:GetNormal(index) end

---@param index number
---@return Vector3
function Path:GetTangent(index) end


function Path:Draw() end

---@param transform Transform
function Path:Insert(transform) end

---@param transform Transform
---@param index number
function Path:Insert(transform, index) end

---@param transform Transform
function Path:TransformBy(transform) end

---@param amount Vector3
function Path:TranslateBy(amount) end

---@param amount Rotation
function Path:RotateBy(amount) end

---@param scale Vector3
function Path:ScaleBy(scale) end


function Path:Center() end

---@param index number
function Path:StartingFrom(index) end

---@param point Vector3
---@return number
function Path:FindClosest(point) end


---@return number
function Path:FindMinimumX() end


---@return number
function Path:FindMinimumY() end


---@return number
function Path:FindMinimumZ() end


---@return number
function Path:FindMaximumX() end


---@return number
function Path:FindMaximumY() end


---@return number
function Path:FindMaximumZ() end

---@param size number
function Path:Normalize(size) end

---@param spacing number
function Path:Resample(spacing) end

---@param parts number
function Path:Subdivide(parts) end

---@param startTransform Transform
---@param endTransform Transform
---@param startTangent Vector3
---@param endTangent Vector3
---@param resolution number
---@param tangentStrength number
---@return Path
function Path:Hermite(startTransform, endTransform, startTangent, endTangent, resolution, tangentStrength) end

---Properties for class Path2d

---@class Path2d
t = Class()

---@type number
Path2d.count = nil

---@type Transform
Path2d.Item = nil

---@type Transform
Path2d.last = nil


---Methods for type Path2d


---@return Path2d
function Path2d:New() end

---@param positionList Vector2[]
---@return Path2d
function Path2d:New(positionList) end

---@param positionList Vector3[]
---@return Path2d
function Path2d:New(positionList) end

---@param point Vector2
function Path2d:Insert(point) end

---@param point Vector2
---@param index number
function Path2d:Insert(point, index) end


---@return Path
function Path2d:OnX() end


---@return Path
function Path2d:OnY() end


---@return Path
function Path2d:OnZ() end

---@param transform Transform
function Path2d:TransformBy(transform) end

---@param amount Vector2
function Path2d:TranslateBy(amount) end

---@param amount Rotation
function Path2d:RotateBy(amount) end

---@param scale Vector2
function Path2d:ScaleBy(scale) end


function Path2d:Center() end

---@param index number
function Path2d:StartingFrom(index) end

---@param point Vector2
---@return number
function Path2d:FindClosest(point) end


---@return number
function Path2d:FindMinimumX() end


---@return number
function Path2d:FindMinimumY() end


---@return number
function Path2d:FindMaximumX() end


---@return number
function Path2d:FindMaximumY() end

---@param size number
function Path2d:Normalize(size) end

---@param sides number
---@return Path2d
function Path2d:Polygon(sides) end

---@param spacing number
function Path2d:Resample(spacing) end

---Properties for class Pointer

---@class Pointer
t = Class()

---@type boolean
Pointer.isDrawing = nil

---@type Layer
Pointer.layer = nil

---@type Color
Pointer.color = nil

---@type string
Pointer.brush = nil

---@type number
Pointer.size = nil

---@type number
Pointer.pressure = nil

---@type Transform
Pointer.transform = nil

---@type Vector3
Pointer.position = nil

---@type Rotation
Pointer.rotation = nil


---Methods for type Pointer


---@return Pointer
function Pointer:New() end

---Properties for class Random

---@class Random
t = Class()

---@type Vector2
Random.insideUnitCircle = nil

---@type Vector3
Random.insideUnitSphere = nil

---@type Vector3
Random.onUnitSphere = nil

---@type Rotation
Random.rotation = nil

---@type Rotation
Random.rotationUniform = nil

---@type number
Random.value = nil

---@type Color
Random.color = nil


---Methods for type Random

---@param hueMin number
---@param hueMax number
---@param saturationMin number
---@param saturationMax number
---@param valueMin number
---@param valueMax number
---@return Color
function Random:ColorHSV(hueMin, hueMax, saturationMin, saturationMax, valueMin, valueMax) end

---@param seed number
function Random:InitState(seed) end

---@param min number
---@param max number
---@return number
function Random:Range(min, max) end

---@param min number
---@param max number
---@return number
function Random:Range(min, max) end

---Properties for class Rotation

---@class Rotation
t = Class()

---@type number
Rotation.Item = nil

---@type number
Rotation.x = nil

---@type number
Rotation.y = nil

---@type number
Rotation.z = nil

---@type Rotation
Rotation.zero = nil

---@type Rotation
Rotation.left = nil

---@type Rotation
Rotation.right = nil

---@type Rotation
Rotation.up = nil

---@type Rotation
Rotation.down = nil

---@type Rotation
Rotation.anticlockwise = nil

---@type Rotation
Rotation.clockwise = nil

---@type Rotation
Rotation.normalized = nil

---@type number
Rotation.angle = nil

---@type Vector3
Rotation.axis = nil


---Methods for type Rotation

---@param x number
---@param y number
---@param z number
---@return Rotation
function Rotation:New(x, y, z) end

---@param fromDirection Vector3
---@param toDirection Vector3
---@return Rotation
function Rotation:SetFromToRotation(fromDirection, toDirection) end

---@param view Vector3
---@return Rotation
function Rotation:SetLookRotation(view) end

---@param view Vector3
---@param up Vector3
---@return Rotation
function Rotation:SetLookRotation(view, up) end

---@param a Rotation
---@param b Rotation
---@return number
function Rotation:Angle(a, b) end

---@param angle number
---@param axis Vector3
---@return Rotation
function Rotation:AngleAxis(angle, axis) end

---@param a Rotation
---@param b Rotation
---@return number
function Rotation:Dot(a, b) end

---@param from Vector3
---@param to Vector3
---@return Rotation
function Rotation:FromToRotation(from, to) end

---@param a Rotation
---@return Rotation
function Rotation:Inverse(a) end

---@param a Rotation
---@param b Rotation
---@param t number
---@return Rotation
function Rotation:Lerp(a, b, t) end

---@param a Rotation
---@param b Rotation
---@param t number
---@return Rotation
function Rotation:LerpUnclamped(a, b, t) end

---@param forward Vector3
---@return Rotation
function Rotation:LookRotation(forward) end

---@param forward Vector3
---@param up Vector3
---@return Rotation
function Rotation:LookRotation(forward, up) end

---@param a Rotation
---@return Rotation
function Rotation:Normalize(a) end

---@param from Rotation
---@param to Rotation
---@param maxDegreesDelta number
---@return Rotation
function Rotation:RotateTowards(from, to, maxDegreesDelta) end

---@param a Rotation
---@param b Rotation
---@param t number
---@return Rotation
function Rotation:Slerp(a, b, t) end

---@param a Rotation
---@param b Rotation
---@param t number
---@return Rotation
function Rotation:SlerpUnclamped(a, b, t) end

---@param other Rotation
---@return Rotation
function Rotation:Multiply(other) end
---Methods for type Selection


function Selection:Duplicate() end


function Selection:Group() end


function Selection:Invert() end


function Selection:Flip() end


function Selection:Recolor() end


function Selection:Rebrush() end


function Selection:Resize() end

---@param count number
function Selection:Trim(count) end


function Selection:SelectAll() end

---Properties for class Sketch

---@class Sketch
t = Class()

---@type CameraPathList
Sketch.cameraPaths = nil

---@type StrokeList
Sketch.strokes = nil

---@type LayerList
Sketch.layers = nil

---@type Layer
Sketch.mainLayer = nil

---@type System.Collections.Generic.List`1[Group]
Sketch.groups = nil

---@type ImageList
Sketch.images = nil

---@type VideoList
Sketch.videos = nil

---@type ModelList
Sketch.models = nil

---@type GuideList
Sketch.guides = nil

---@type EnvironmentList
Sketch.environments = nil

---@type Color
Sketch.ambientLightColor = nil

---@type Color
Sketch.mainLightColor = nil

---@type Color
Sketch.secondaryLightColor = nil

---@type Rotation
Sketch.mainLightRotation = nil

---@type Rotation
Sketch.secondaryLightRotation = nil


---Methods for type Sketch

---@param name string
function Sketch:Open(name) end

---@param overwrite boolean
function Sketch:Save(overwrite) end

---@param name string
function Sketch:SaveAs(name) end


function Sketch:Export() end


function Sketch:NewSketch() end

---@param filename string
function Sketch:ImportSkybox(filename) end

---Properties for class Spectator

---@class Spectator
t = Class()

---@type boolean
Spectator.canSeeWidgets = nil

---@type boolean
Spectator.canSeeStrokes = nil

---@type boolean
Spectator.canSeeSelection = nil

---@type boolean
Spectator.canSeeHeadset = nil

---@type boolean
Spectator.canSeePanels = nil

---@type boolean
Spectator.canSeeUi = nil

---@type boolean
Spectator.canSeeUsertools = nil

---@type boolean
Spectator.active = nil

---@type Vector3
Spectator.position = nil

---@type Rotation
Spectator.rotation = nil

---@type boolean
Spectator.lockedToScene = nil


---Methods for type Spectator

---@param position Vector3
function Spectator:LookAt(position) end


function Spectator:Stationary() end


function Spectator:SlowFollow() end


function Spectator:Wobble() end


function Spectator:Circular() end

---Properties for class Stroke

---@class Stroke
t = Class()

---@type Path
Stroke.path = nil

---@type string
Stroke.brushType = nil

---@type number
Stroke.brushSize = nil

---@type Color
Stroke.brushColor = nil

---@type Layer
Stroke.layer = nil

---@type Group
Stroke.group = nil

---@type Transform
Stroke.Item = nil

---@type number
Stroke.count = nil


---Methods for type Stroke

---@param brushName string
function Stroke:ChangeMaterial(brushName) end


function Stroke:Delete() end


function Stroke:Select() end

---@param from number
---@param to number
function Stroke:SelectRange(from, to) end

---@param from number
---@param to number
---@return Stroke
function Stroke:JoinRange(from, to) end


---@return Stroke
function Stroke:JoinToPrevious() end

---@param stroke2 Stroke
---@return Stroke
function Stroke:Join(stroke2) end

---@param name string
function Stroke:MergeFrom(name) end
---Methods for type Svg

---@param svgPath string
---@return MultiPath
function Svg:ParsePathString(svgPath) end

---@param svg string
---@param offsetPerPath number
---@param includeColors boolean
---@return MultiPath
function Svg:ParseDocument(svg, offsetPerPath, includeColors) end

---@param svg string
---@param tr Transform
function Svg:DrawPathString(svg, tr) end

---@param svg string
---@param tr Transform
function Svg:DrawDocument(svg, tr) end

---Properties for class Symmetry

---@class Symmetry
t = Class()

---@type Vector3
Symmetry.brushOffset = nil

---@type Vector3
Symmetry.wandOffset = nil

---@type SymmetrySettings
Symmetry.settings = nil


---Methods for type Symmetry


function Symmetry:SummonWidget() end

---@param angle number
---@param minorRadius number
---@return number
function Symmetry:Ellipse(angle, minorRadius) end

---@param angle number
---@return number
function Symmetry:Square(angle) end

---@param angle number
---@param n number
---@param a number
---@param b number
---@return number
function Symmetry:Superellipse(angle, n, a, b) end

---@param angle number
---@param halfSideLength number
---@param cornerRadius number
---@return number
function Symmetry:Rsquare(angle, halfSideLength, cornerRadius) end

---@param angle number
---@param numSides number
---@param radius number
---@return number
function Symmetry:Polygon(angle, numSides, radius) end

---@param colors Color[]
function Symmetry:ClearColors(colors) end

---@param color Color
function Symmetry:AddColor(color) end

---@param colors Color[]
function Symmetry:SetColors(colors) end


---@return Color[]
function Symmetry:GetColors() end

---@param brush string
function Symmetry:AddBrush(brush) end

---@param brushes string[]
function Symmetry:ClearBrushes(brushes) end

---@param brushes string[]
function Symmetry:SetBrushes(brushes) end


---@return string[]
function Symmetry:GetBrushNames() end


---@return string[]
function Symmetry:GetBrushGuids() end

---@param path IPath
---@return Path
function Symmetry:PathToPolar(path) end
---Methods for type Timer

---@param fn function
---@param interval number
---@param delay number
---@param repeats number
function Timer:Set(fn, interval, delay, repeats) end

---@param fn function
function Timer:Unset(fn) end

---Properties for class Transform

---@class Transform
t = Class()

---@type Transform
Transform.inverse = nil

---@type Vector3
Transform.up = nil

---@type Vector3
Transform.down = nil

---@type Vector3
Transform.right = nil

---@type Vector3
Transform.left = nil

---@type Vector3
Transform.forward = nil

---@type Vector3
Transform.back = nil

---@type Vector3
Transform.position = nil

---@type Rotation
Transform.rotation = nil

---@type number
Transform.scale = nil

---@type Transform
Transform.identity = nil


---Methods for type Transform

---@param transform Transform
---@return Transform
function Transform:TransformBy(transform) end

---@param translation Vector3
---@return Transform
function Transform:TranslateBy(translation) end

---@param rotation Rotation
---@return Transform
function Transform:RotateBy(rotation) end

---@param scale number
---@return Transform
function Transform:ScaleBy(scale) end

---@param translation Vector3
---@param rotation Rotation
---@param scale number
---@return Transform
function Transform:New(translation, rotation, scale) end

---@param translation Vector3
---@param rotation Rotation
---@return Transform
function Transform:New(translation, rotation) end

---@param translation Vector3
---@return Transform
function Transform:New(translation) end

---@param translation Vector3
---@param scale number
---@return Transform
function Transform:New(translation, scale) end

---@param x number
---@param y number
---@param z number
---@return Transform
function Transform:New(x, y, z) end

---@param other Transform
---@return Transform
function Transform:Multiply(other) end

---Properties for class User

---@class User
t = Class()

---@type Vector3
User.position = nil

---@type Rotation
User.rotation = nil



---Properties for class Vector2

---@class Vector2
t = Class()

---@type number
Vector2.Item = nil

---@type number
Vector2.x = nil

---@type number
Vector2.y = nil

---@type number
Vector2.magnitude = nil

---@type number
Vector2.sqrMagnitude = nil

---@type Vector2
Vector2.normalized = nil

---@type Vector2
Vector2.down = nil

---@type Vector2
Vector2.left = nil

---@type Vector2
Vector2.negativeInfinity = nil

---@type Vector2
Vector2.one = nil

---@type Vector2
Vector2.positiveInfinity = nil

---@type Vector2
Vector2.right = nil

---@type Vector2
Vector2.up = nil

---@type Vector2
Vector2.zero = nil


---Methods for type Vector2

---@param x number
---@param y number
---@return Vector2
function Vector2:New(x, y) end

---@param other Vector2
---@return number
function Vector2:Angle(other) end

---@param maxLength number
---@return Vector2
function Vector2:ClampMagnitude(maxLength) end

---@param other Vector2
---@return number
function Vector2:Distance(other) end

---@param a Vector2
---@param b Vector2
---@return number
function Vector2:Dot(a, b) end

---@param a Vector2
---@param b Vector2
---@param t number
---@return Vector2
function Vector2:Lerp(a, b, t) end

---@param a Vector2
---@param b Vector2
---@param t number
---@return Vector2
function Vector2:LerpUnclamped(a, b, t) end

---@param a Vector2
---@param b Vector2
---@return Vector2
function Vector2:Max(a, b) end

---@param a Vector2
---@param b Vector2
---@return Vector2
function Vector2:Min(a, b) end

---@param target Vector2
---@param maxDistanceDelta number
---@return Vector2
function Vector2:MoveTowards(target, maxDistanceDelta) end

---@param normal Vector2
---@return Vector2
function Vector2:Reflect(normal) end

---@param other Vector2
---@return Vector2
function Vector2:Scale(other) end

---@param other Vector2
---@return number
function Vector2:SignedAngle(other) end

---@param a Vector2
---@param b Vector2
---@param t number
---@return Vector2
function Vector2:Slerp(a, b, t) end

---@param a Vector2
---@param b Vector2
---@param t number
---@return Vector2
function Vector2:SlerpUnclamped(a, b, t) end

---@param degrees number
---@return Vector2
function Vector2:PointOnCircle(degrees) end


---@return Vector3
function Vector2:OnX() end


---@return Vector3
function Vector2:OnY() end


---@return Vector3
function Vector2:OnZ() end

---@param other Vector2
---@return Vector2
function Vector2:Add(other) end

---@param x number
---@param y number
---@return Vector2
function Vector2:Add(x, y) end

---@param other Vector2
---@return Vector2
function Vector2:Subtract(other) end

---@param x number
---@param y number
---@return Vector2
function Vector2:Subtract(x, y) end

---@param value number
---@return Vector2
function Vector2:Multiply(value) end

---@param other Vector2
---@return Vector2
function Vector2:ScaleBy(other) end

---@param x number
---@param y number
---@return Vector2
function Vector2:ScaleBy(x, y) end

---@param value number
---@return Vector2
function Vector2:Divide(value) end

---@param other Vector2
---@return boolean
function Vector2:NotEquals(other) end

---@param x number
---@param y number
---@return boolean
function Vector2:NotEquals(x, y) end

---Properties for class Vector3

---@class Vector3
t = Class()

---@type number
Vector3.Item = nil

---@type number
Vector3.x = nil

---@type number
Vector3.y = nil

---@type number
Vector3.z = nil

---@type number
Vector3.magnitude = nil

---@type number
Vector3.sqrMagnitude = nil

---@type Vector3
Vector3.normalized = nil

---@type Vector3
Vector3.back = nil

---@type Vector3
Vector3.down = nil

---@type Vector3
Vector3.forward = nil

---@type Vector3
Vector3.left = nil

---@type Vector3
Vector3.negativeInfinity = nil

---@type Vector3
Vector3.one = nil

---@type Vector3
Vector3.positiveInfinity = nil

---@type Vector3
Vector3.right = nil

---@type Vector3
Vector3.up = nil

---@type Vector3
Vector3.zero = nil


---Methods for type Vector3

---@param x number
---@param y number
---@param z number
---@return Vector3
function Vector3:New(x, y, z) end

---@param other Vector3
---@return number
function Vector3:Angle(other) end

---@param maxLength number
---@return Vector3
function Vector3:ClampMagnitude(maxLength) end

---@param a Vector3
---@param b Vector3
---@return Vector3
function Vector3:Cross(a, b) end

---@param other Vector3
---@return number
function Vector3:Distance(other) end

---@param a Vector3
---@param b Vector3
---@param t number
---@return Vector3
function Vector3:Lerp(a, b, t) end

---@param a Vector3
---@param b Vector3
---@param t number
---@return Vector3
function Vector3:LerpUnclamped(a, b, t) end

---@param a Vector3
---@param b Vector3
---@return Vector3
function Vector3:Max(a, b) end

---@param a Vector3
---@param b Vector3
---@return Vector3
function Vector3:Min(a, b) end

---@param target Vector3
---@param maxDistanceDelta number
---@return Vector3
function Vector3:MoveTowards(target, maxDistanceDelta) end

---@param other Vector3
---@return Vector3
function Vector3:Project(other) end

---@param planeNormal Vector3
---@return Vector3
function Vector3:ProjectOnPlane(planeNormal) end

---@param normal Vector3
---@return Vector3
function Vector3:Reflect(normal) end

---@param target Vector3
---@param maxRadiansDelta number
---@param maxMagnitudeDelta number
---@return Vector3
function Vector3:RotateTowards(target, maxRadiansDelta, maxMagnitudeDelta) end

---@param other Vector3
---@return Vector3
function Vector3:ScaleBy(other) end

---@param other Vector3
---@param axis Vector3
---@return number
function Vector3:SignedAngle(other, axis) end

---@param a Vector3
---@param b Vector3
---@param t number
---@return Vector3
function Vector3:Slerp(a, b, t) end

---@param a Vector3
---@param b Vector3
---@param t number
---@return Vector3
function Vector3:SlerpUnclamped(a, b, t) end

---@param other Vector3
---@return Vector3
function Vector3:Add(other) end

---@param x number
---@param y number
---@param z number
---@return Vector3
function Vector3:Add(x, y, z) end

---@param other Vector3
---@return Vector3
function Vector3:Subtract(other) end

---@param x number
---@param y number
---@param z number
---@return Vector3
function Vector3:Subtract(x, y, z) end

---@param value number
---@return Vector3
function Vector3:Multiply(value) end

---@param x number
---@param y number
---@param z number
---@return Vector3
function Vector3:ScaleBy(x, y, z) end

---@param value number
---@return Vector3
function Vector3:Divide(value) end

---@param other Vector3
---@return boolean
function Vector3:NotEquals(other) end

---@param x number
---@param y number
---@param z number
---@return boolean
function Vector3:NotEquals(x, y, z) end

---Properties for class Video

---@class Video
t = Class()

---@type number
Video.index = nil

---@type Layer
Video.layer = nil

---@type Group
Video.group = nil

---@type Transform
Video.transform = nil

---@type Vector3
Video.position = nil

---@type Rotation
Video.rotation = nil

---@type number
Video.scale = nil


---Methods for type Video

---@param location string
---@return Video
function Video:Import(location) end


function Video:Select() end


function Video:Delete() end

---Properties for class Visualizer

---@class Visualizer
t = Class()

---@type number
Visualizer.sampleRate = nil

---@type number
Visualizer.duration = nil


---Methods for type Visualizer


function Visualizer:EnableScripting() end


function Visualizer:DisableScripting() end

---@param data number[]
function Visualizer:SetWaveform(data) end

---@param data1 number[]
---@param data2 number[]
---@param data3 number[]
---@param data4 number[]
function Visualizer:SetFft(data1, data2, data3, data4) end

---@param x number
---@param y number
---@param z number
---@param w number
function Visualizer:SetBeats(x, y, z, w) end

---@param x number
---@param y number
---@param z number
---@param w number
function Visualizer:SetBeatAccumulators(x, y, z, w) end

---@param peak number
function Visualizer:SetBandPeak(peak) end

---Properties for class Wand

---@class Wand
t = Class()

---@type Vector3
Wand.position = nil

---@type Rotation
Wand.rotation = nil

---@type Vector3
Wand.direction = nil

---@type number
Wand.pressure = nil

---@type Vector3
Wand.speed = nil

---@type boolean
Wand.triggerIsPressed = nil

---@type boolean
Wand.triggerPressedThisFrame = nil


---Methods for type Wand

---@param size number
function Wand:ResizeHistory(size) end

---@param size number
function Wand:SetHistorySize(size) end

---@param back number
---@return Vector3
function Wand:PastPosition(back) end

---@param back number
---@return Rotation
function Wand:PastRotation(back) end
---Methods for type Waveform

---@param time number
---@param frequency number
---@return number
function Waveform:Sine(time, frequency) end

---@param time number
---@param frequency number
---@return number
function Waveform:Cosine(time, frequency) end

---@param time number
---@param frequency number
---@return number
function Waveform:Triangle(time, frequency) end

---@param time number
---@param frequency number
---@return number
function Waveform:Sawtooth(time, frequency) end

---@param time number
---@param frequency number
---@return number
function Waveform:Square(time, frequency) end

---@param time number
---@param frequency number
---@param pulseWidth number
---@return number
function Waveform:Pulse(time, frequency, pulseWidth) end

---@param time number
---@param frequency number
---@return number
function Waveform:Exponent(time, frequency) end

---@param time number
---@param frequency number
---@param power number
---@return number
function Waveform:Power(time, frequency, power) end

---@param time number
---@param frequency number
---@return number
function Waveform:Parabolic(time, frequency) end

---@param time number
---@param frequency number
---@param exponent number
---@return number
function Waveform:ExponentialSawtooth(time, frequency, exponent) end

---@param time number
---@param frequency number
---@return number
function Waveform:PerlinNoise(time, frequency) end


---@return number
function Waveform:WhiteNoise() end

---@param previous number
---@return number
function Waveform:BrownNoise(previous) end

---@param previous number
---@return number
function Waveform:BlueNoise(previous) end

---@param time number
---@param frequency number
---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:Sine(time, frequency, duration, sampleRate, amplitude) end

---@param time number
---@param frequency number
---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:Cosine(time, frequency, duration, sampleRate, amplitude) end

---@param time number
---@param frequency number
---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:Triangle(time, frequency, duration, sampleRate, amplitude) end

---@param time number
---@param frequency number
---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:Sawtooth(time, frequency, duration, sampleRate, amplitude) end

---@param time number
---@param frequency number
---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:Square(time, frequency, duration, sampleRate, amplitude) end

---@param time number
---@param frequency number
---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:Exponent(time, frequency, duration, sampleRate, amplitude) end

---@param time number
---@param frequency number
---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:Parabolic(time, frequency, duration, sampleRate, amplitude) end

---@param time number
---@param frequency number
---@param pulseWidth number
---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:Pulse(time, frequency, pulseWidth, duration, sampleRate, amplitude) end

---@param time number
---@param frequency number
---@param power number
---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:Power(time, frequency, power, duration, sampleRate, amplitude) end

---@param time number
---@param frequency number
---@param exponent number
---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:ExponentialSawtoothWave(time, frequency, exponent, duration, sampleRate, amplitude) end

---@param time number
---@param frequency number
---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:PerlinNoise(time, frequency, duration, sampleRate, amplitude) end

---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:WhiteNoise(duration, sampleRate, amplitude) end

---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:BrownNoise(duration, sampleRate, amplitude) end

---@param duration number
---@param sampleRate number
---@param amplitude number
---@return number[]
function Waveform:BlueNoise(duration, sampleRate, amplitude) end
---Methods for type WebRequest

---@param url string
---@param onSuccess function
---@param onError function
---@param headers table
---@param context table
function WebRequest:Get(url, onSuccess, onError, headers, context) end

---@param url string
---@param postData table
---@param onSuccess function
---@param onError function
---@param headers table
---@param context table
function WebRequest:Post(url, postData, onSuccess, onError, headers, context) end
