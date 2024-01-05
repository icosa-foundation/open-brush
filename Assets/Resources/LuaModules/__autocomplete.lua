---@meta



---@class App
---@field time number The time in seconds since Open Brush was launched
---@field frames number The number of frames that have been rendered since Open Brush was launched
---@field physics boolean Whether physics simulation is active (defaults is off)
---@field currentScale number The current scale of the scene
---@field environment string Get or set the current environment by name
---@field clipboardText string Get or set the clipboard text
App = {}

function App:Undo() end


function App:Redo() end

---@param url string The url to send the stroke data to
function App:AddListener(url) end


function App:ResetPanels() end


function App:ShowScriptsFolder() end


function App:ShowExportFolder() end


function App:ShowSketchesFolder() end

---@param active boolean True means activate, false means deactivate
function App:StraightEdge(active) end

---@param active boolean True means activate, false means deactivate
function App:AutoOrient(active) end

---@param active boolean True means activate, false means deactivate
function App:ViewOnly(active) end

---@param active boolean True means activate, false means deactivate
function App:AutoSimplify(active) end

---@param active boolean True means activate, false means deactivate
function App:Disco(active) end

---@param active boolean True means activate, false means deactivate
function App:Profiling(active) end

---@param active boolean True means activate, false means deactivate
function App:PostProcessing(active) end


function App:DraftingVisible() end


function App:DraftingTransparent() end


function App:DraftingHidden() end

---@param active boolean True means activate, false means deactivate
function App:Watermark(active) end

---@param path string The file path to read from. It must be relative to and contined within the Scripts folder
---@return string # The contents of the file as a string
function App:ReadFile(path) end

---@param message string The error message to display
function App:Error(message) end

---@param fontData string Font data in .chr format
function App:SetFont(fontData) end

---@param tr Transform Determines the position and orientation of the camera used to take the snapshot
---@param filename string The filename to use for the saved snapshot
---@param width number Image width
---@param height number Image height
---@param superSampling? number The supersampling strength to apply (between 0.125 and 4.0)
---@param renderDepth? boolean If true then render the depth buffer instead of the image
---@param removeBackground? boolean 
function App:TakeSnapshot(tr, filename, width, height, superSampling, renderDepth, removeBackground) end

---@param tr Transform Determines the position and orientation of the camera used to take the snapshot
---@param filename string The filename to use for the saved snapshot
---@param width? number The width of the image
function App:Take360Snapshot(tr, filename, width) end



---@class Brush
---@field timeSincePressed number Time in seconds since the brush trigger was last pressed
---@field timeSinceReleased number Time in seconds since the brush trigger was last released
---@field triggerIsPressed boolean Check whether the brush trigger is currently pressed
---@field triggerPressedThisFrame boolean Check whether the brush trigger was pressed during the current frame
---@field triggerReleasedThisFrame boolean Check whether the brush trigger was released during the current frame
---@field distanceMoved number The distance moved by the brush
---@field distanceDrawn number The distance drawn by the brush (i.e. distance since the trigger was last pressed)
---@field position Vector3 | number[] The 3D position of the Brush Controller's tip
---@field rotation Rotation | number[] The 3D orientation of the Brush Controller's tip
---@field direction Vector3 | number[] The vector representing the forward direction of the brush
---@field size number The current brush size
---@field pressure number Brush pressure is determined by how far the trigger is pressed in
---@field type string The current brush type
---@field types string[] All brush types available via the UI
---@field speed number How fast the brush is currently moving
---@field colorRgb Color | number[] Gets or set brush color
---@field colorHsv Vector3 | number[] Gets or set brush color using a Vector3 representing hue, saturation and brightness
---@field colorHtml string The color of the brush as a valid HTML color string (either hex values or a color name)
---@field lastColorPicked Color | number[] The last color picked by the brush.
---@field LastColorPickedHsv Vector3 | number[] The last color picked by the brush in HSV.
---@field currentPath Path | Transform[] The current path of the brush. Assumes a stroke is in progress.
Brush = {}
---@param includeTags string[] Include brushes that have any of these tags
---@param excludeTags string[] Exclude brushes that have any of these tags
---@return string[] # A filtered list of brush types
function Brush:GetTypes(includeTags, excludeTags) end


function Brush:JitterColor() end

---@param size number How many frames of position/rotation to remember
function Brush:ResizeHistory(size) end

---@param size number How many frames of position/rotation to remember
function Brush:SetHistorySize(size) end

---@param back number How many frames back in the history to look
---@return Vector3 # The position of the brush during the specified frame
function Brush:GetPastPosition(back) end

---@param back number How many frames back in the history to look
---@return Rotation # The rotation of the brush during the specified frame
function Brush:GetPastRotation(back) end

---@param active boolean True means forced painting, false is normal behaviour
function Brush:ForcePaintingOn(active) end

---@param active boolean True means painting is forced off, false is normal behaviour
function Brush:ForcePaintingOff(active) end


function Brush:ForceNewStroke() end

---@param type string The brush name
---@return string[] # A list of float property names usable with Stroke:SetShaderFloat
function Brush:GetShaderFloatParameters(type) end

---@param type string The brush name
---@return string[] # A list of color property names usable with Stroke:SetShaderColor
function Brush:GetShaderColorParameters(type) end

---@param type string The brush name
---@return string[] # A list of texture property names usable with Stroke:SetShaderTexture
function Brush:GetShaderTextureParameters(type) end

---@param type string The brush name
---@return string[] # A list of vector property names usable with Stroke:SetShaderVector
function Brush:GetShaderVectorParameters(type) end



---@class CameraPath
---@field index number Returns the index of this Camera Path in Sketch.cameraPaths
---@field layer Layer The layer the camera path is on
---@field group Group The group this camera path is part of
---@field active boolean Gets or sets whether this Camera Path is active
---@field transform Transform The transform of the camera path
---@field position Vector3 | number[] The 3D position of the Camera Path (usually but not always its first position knot)
---@field rotation Rotation | number[] The 3D orientation of the Brush Camera Path
---@field scale number The scale of the camera path
CameraPath = {}

function CameraPath:RenderActivePath() end


function CameraPath:ShowAll() end


function CameraPath:HideAll() end

---@param active boolean On is true, off is false
function CameraPath:PreviewActivePath(active) end


function CameraPath:Delete() end


---@return CameraPath # The new CameraPath
function CameraPath:New() end

---@param path Path The Path to convert
---@param looped boolean Whether the resulting CameraPath should loop
---@return CameraPath # A new CameraPath
function CameraPath:FromPath(path, looped) end

---@param step number The time step is use for each sample
---@return Path # The new Path
function CameraPath:AsPath(step) end


---@return CameraPath # The copy of the specied CameraPath
function CameraPath:Duplicate() end

---@param position Vector3 The position of the new knot
---@param rotation Rotation The rotation of the new knot
---@param smoothing number Controls the spline curvature for this knot
---@return number # The index of the new knot, or -1 if the position is too far from the path
function CameraPath:InsertPosition(position, rotation, smoothing) end

---@param t number The time along the path to insert the new knot
---@param rotation Rotation The rotation of the new knot
---@param smoothing number Controls the spline curvature for this knot
---@return number # The index of the new knot
function CameraPath:InsertPositionAtTime(t, rotation, smoothing) end

---@param position Vector3 The position of the new knot
---@param rotation Rotation The rotation of the new knot
---@return number # The index of the new knot, or -1 if the position is too far from the path
function CameraPath:InsertRotation(position, rotation) end

---@param t number The time along the path to insert the new knot
---@param rotation Rotation The rotation of the new knot
---@return number # The index of the new knot
function CameraPath:InsertRotationAtTime(t, rotation) end

---@param position Vector3 The position of the new knot
---@param fov number The field of view of the new knot
---@return number # The index of the new knot, or -1 if the position is too far from the path
function CameraPath:InsertFov(position, fov) end

---@param t number The time along the path to insert the new knot
---@param fov number The field of view of the new knot
---@return number # The index of the new knot
function CameraPath:InsertFovAtTime(t, fov) end

---@param position Vector3 The position of the new knot
---@param speed number The speed of the new knot
---@return number # The index of the new knot, or -1 if the position is too far from the path
function CameraPath:InsertSpeed(position, speed) end

---@param t number The time along the path to insert the new knot
---@param speed number The speed of the new knot
---@return number # The index of the new knot
function CameraPath:InsertSpeedAtTime(t, speed) end

---@param position Vector3 The position to extend the camera path to
---@param rotation Rotation The rotation of the camera path at the extended position
---@param smoothing number The smoothing factor applied to the new point
---@param atStart? boolean Determines whether the extension is done at the start or end of the camera path. True=start, false=end
function CameraPath:Extend(position, rotation, smoothing, atStart) end


function CameraPath:Loop() end


function CameraPath:RecordActivePath() end

---@param time number The time at which to sample the camera path
---@param loop? boolean Determines whether the camera path should loop
---@param pingpong? boolean Determines whether the camera path should pingpong (reverse direction every loop
---@return Transform # The sampled transform of the camera at the specified time
function CameraPath:Sample(time, loop, pingpong) end

---@param tolerance number The tolerance used for simplification
---@param smoothing number The smoothing factor used for simplification
---@return CameraPath # A new simplified Camera Path
function CameraPath:Simplify(tolerance, smoothing) end



---@class CameraPathList
---@field last CameraPath Returns the last Camera Path
---@field count number The number of Camera Paths
---@field active CameraPath The active Camera Path
CameraPathList = {}

function CameraPathList:ShowAll() end


function CameraPathList:HideAll() end

---@param active boolean A boolean value indicating whether to preview the active path or not
function CameraPathList:PreviewActivePath(active) end



---@class Color
---@field r number The red component
---@field g number The green component
---@field b number The blue component
---@field a number The alpha component
---@field grayscale number The grayscale value
---@field gamma Color | number[] The gamma color space representation
---@field linear Color | number[] The linear color space representation
---@field maxColorComponent number The maximum color component value
---@field html string The HTML hex string of the color (for example "A4D0FF")
---@field greyscale number The grayscale value
---@field hsv Vector3 | number[] The hue, saturation and brightess
---@field black Color | number[] The color black
---@field blue Color | number[] The color blue
---@field cyan Color | number[] The color cyan
---@field gray Color | number[] The color gray
---@field green Color | number[] The color green
---@field grey Color | number[] The color grey
---@field magenta Color | number[] The color magenta
---@field red Color | number[] The color red
---@field white Color | number[] The color white
---@field yellow Color | number[] The color yellow
Color = {}
---@param r? number The red component of the color. Default is 0
---@param g? number The green component of the color. Default is 0
---@param b? number The blue component of the color. Default is 0
---@return Color # instance of the Color
function Color:New(r, g, b) end

---@param html string The HTML string representing the color
---@return Color # Returns the color. Invalid html inputs return bright magenta (r=1, g=0, b=1)
function Color:New(html) end

---@param a Color The start color
---@param b Color The end color
---@param t number The interpolation value. Should be between 0 and 1
---@return Color # The interpolated color
function Color:Lerp(a, b, t) end

---@param a Color The start color
---@param b Color The end color
---@param t number The interpolation value
---@return Color # color
function Color:LerpUnclamped(a, b, t) end

---@param h number The hue value. Should be between 0 and 1
---@param s number The saturation value. Should be between 0 and 1
---@param v number The value value. Should be between 0 and 1
---@return Color # color
function Color:HsvToRgb(h, s, v) end

---@param hsv Vector3 A Vector3 with xyz representing hsv. All values between 0 and 1
---@return Color # color
function Color:HsvToRgb(hsv) end

---@param other Color The color to add
---@return Color # color
function Color:Add(other) end

---@param r number The red component value to add
---@param g number The green component value to add
---@param b number The blue component value to add
---@return Color # color
function Color:Add(r, g, b) end

---@param other Color The color to subtract
---@return Color # color
function Color:Subtract(other) end

---@param r number The red component value to subtract
---@param g number The green component value to subtract
---@param b number The blue component value to subtract
---@return Color # color
function Color:Subtract(r, g, b) end

---@param value number The value to multiply
---@return Color # color
function Color:Multiply(value) end

---@param r number The red component value to multiply
---@param g number The green component value to multiply
---@param b number The blue component value to multiply
---@return Color # color
function Color:Multiply(r, g, b) end

---@param value number The value to divide
---@return Color # color
function Color:Divide(value) end

---@param other Color The color to compare
---@return boolean # true if this color is not equal to the specified color; otherwise, false
function Color:NotEquals(other) end

---@param r number The red component value to compare
---@param g number The green component value to compare
---@param b number The blue component value to compare
---@return boolean # true if this color is not equal to the specified RGB values; otherwise, false
function Color:NotEquals(r, g, b) end



---@class Environment
---@field current Environment The current environment settings
---@field gradientColorA Color | number[] The sky color at the top
---@field gradientColorB Color | number[] The skybox color at the horizon
---@field gradientOrientation Rotation | number[] The sky gradient orientation
---@field fogColor Color | number[] The fog color
---@field fogDensity number The fog density
---@field ambientColor Color | number[] The ambient light color
---@field mainLightColor Color | number[] The main light color
---@field mainLightDirection Rotation | number[] The main light direction
---@field secondaryLightColor Color | number[] The secondary light color
---@field secondaryLightDirection Rotation | number[] The secondary light direction
Environment = {}


---@class EnvironmentList
---@field last Environment Returns the last environment
---@field current Environment Returns the current environment
---@field count number The number of available environments
EnvironmentList = {}
---@param name string The name of the environment to get
---@return Environment # The environment, or nil if no environment has that name
function EnvironmentList:ByName(name) end



---@class Easing
Easing = {}
---@param t number The input value between 0 and 1
---@return number # The input is returned unchanged
function Easing:Linear(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the in direction only
function Easing:InQuad(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the out direction only
function Easing:OutQuad(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in and out
function Easing:InOutQuad(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the in direction only
function Easing:InCubic(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the out direction only
function Easing:OutCubic(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in and out
function Easing:InOutCubic(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the in direction only
function Easing:InQuart(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the out direction only
function Easing:OutQuart(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in and out
function Easing:InOutQuart(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the in direction only
function Easing:InQuint(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the out direction only
function Easing:OutQuint(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in and out
function Easing:InOutQuint(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the in direction only
function Easing:InSine(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the out direction only
function Easing:OutSine(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in and out
function Easing:InOutSine(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the in direction only
function Easing:InExpo(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the out direction only
function Easing:OutExpo(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in and out
function Easing:InOutExpo(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the in direction only
function Easing:InCirc(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the out direction only
function Easing:OutCirc(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in and out
function Easing:InOutCirc(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the in direction only
function Easing:InElastic(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the out direction only
function Easing:OutElastic(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in and out
function Easing:InOutElastic(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the in direction only
function Easing:InBack(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the out direction only
function Easing:OutBack(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in and out
function Easing:InOutBack(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the in direction only
function Easing:InBounce(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in the out direction only
function Easing:OutBounce(t) end

---@param t number The input value between 0 and 1
---@return number # The value smoothed in and out
function Easing:InOutBounce(t) end



---@class Group
---@field images ImageList | Image[] All the images in this group
---@field videos VideoList | Video[] All the videos in this group
---@field models ModelList | Model[] All the models in this group
---@field guides GuideList | Guide[] All the guides in this group
---@field cameraPaths CameraPathList | CameraPath[] All the camera paths in this group
Group = {}

---@return Group # The new group
function Group:New() end

---@param image Image The image to add
function Group:Add(image) end

---@param video Video The video to add
function Group:Add(video) end

---@param model Model The model to add
function Group:Add(model) end

---@param guide Guide The guide to add
function Group:Add(guide) end

---@param cameraPath CameraPath The CameraPath to add
function Group:Add(cameraPath) end

---@param stroke Stroke The Stroke to add
function Group:Add(stroke) end



---@class GroupList
---@field last Group Returns the last layer
---@field count number The number of layers
GroupList = {}


---@class Guide
---@field index number The index of the active widget
---@field layer Layer The layer the guide is on
---@field group Group The group this guide is part of
---@field transform Transform The transform of the Guide Widget
---@field position Vector3 | number[] The 3D position of the Guide Widget
---@field rotation Rotation | number[] The 3D orientation of the Guide Widget
---@field scale number The scale of the Guide Widget
Guide = {}
---@param transform Transform The transform of the Guide Widget
---@return Guide # A new cube guide
function Guide:NewCube(transform) end

---@param transform Transform The transform of the Guide Widget
---@return Guide # A new sphere guide
function Guide:NewSphere(transform) end

---@param transform Transform The transform of the Guide Widget
---@return Guide # A new capsule guide
function Guide:NewCapsule(transform) end

---@param transform Transform The transform of the Guide Widget
---@return Guide # A new cone guide
function Guide:NewCone(transform) end

---@param transform Transform The transform of the Guide Widget
---@return Guide # A new ellipsoid guide
function Guide:NewEllipsoid(transform) end

---@param transform Transform The transform of the Guide Widget
---@param model Model The Model to use for the custom guide
---@return Guide # A new custom guide based on the convex hull of the model
function Guide:NewCustom(transform, model) end


function Guide:Select() end


function Guide:Deselect() end


function Guide:Delete() end

---@param scale Vector3 The scale vector for scaling the Guide Widget
function Guide:Scale(scale) end



---@class GuideList
---@field lastSelected Guide Returns the last guide that was selected
---@field last Guide Returns the last Guide
---@field enabled boolean Gets or sets the state of "Enable guides"
---@field count number The number of guides
GuideList = {}


---@class Headset
Headset = {}
---@param size number How many frames of position/rotation to remember
function Headset:ResizeHistory(size) end

---@param size number How many frames of position/rotation to remember
function Headset:SetHistorySize(size) end

---@param back number How many frames back in the history to look
---@return Vector3 # 
function Headset:PastPosition(back) end

---@param back number How many frames back in the history to look
---@return Rotation # 
function Headset:PastRotation(back) end



---@class Image
---@field index number The index of the active widget
---@field layer Layer The layer the image is on
---@field group Group The group this image is part of
---@field transform Transform The transform of the image widget
---@field position Vector3 | number[] The 3D position of the Image Widget
---@field rotation Rotation | number[] The 3D orientation of the Image Widget
---@field scale number The scale of the image widget
Image = {}
---@param location string The location of the image
---@return Image # The imported image widget
function Image:Import(location) end


function Image:Select() end


function Image:Deselect() end


function Image:Delete() end

---@param depth number The depth of the extrusion
---@param color? Color The color of the extrusion
function Image:Extrude(depth, color) end


---@return string # The encoded image so it can be submitted as a response to a HTML form
function Image:FormEncode() end

---@param base64 string The base64 data for the image
---@param filename string The filename to save as
---@return string # 
function Image:SaveBase64(base64, filename) end



---@class ImageList
---@field lastSelected Image Returns the last image that was selected
---@field last Image Returns the last Image
---@field count number The number of images
ImageList = {}


---@class Layer
---@field strokes StrokeList | Stroke[] All the strokes on this layer
---@field images ImageList | Image[] All the images on this layer
---@field allowStrokeAnimation boolean Sets whether or not individual strokes on this layer can be animated via 
---@field videos VideoList | Video[] All the videos on this layer
---@field models ModelList | Model[] All the models on this layer
---@field guides GuideList | Guide[] All the guides on this layer
---@field cameraPaths CameraPathList | CameraPath[] All the camera paths on this layer
---@field groups GroupList | Group[] All the groups on this layer
---@field index number Gets the index of the layer in the layer canvases
---@field active boolean Is the layer the active layer. Making another layer inactive will make the main layer the active layer again.
---@field transform Transform The transform of the layer
---@field position Vector3 | number[] The 3D position of the Layer (specifically the position of it's anchor point
---@field rotation Rotation | number[] The rotation of the layer in 3D space
---@field scale number The scale of the layer
Layer = {}

---@return Layer # The new layer
function Layer:New() end


function Layer:SelectAll() end


function Layer:CenterPivot() end


function Layer:ShowPivot() end


function Layer:HidePivot() end


function Layer:Clear() end


function Layer:Delete() end


---@return Layer # The layer that contains the combined content
function Layer:Squash() end

---@param destinationLayer Layer The destination layer
function Layer:SquashTo(destinationLayer) end


function Layer:Show() end


function Layer:Hide() end

---@param clipStart number The amount of the stroke to hide from the start (0-1)
---@param clipEnd number The amount of the stroke to hide from the end (0-1)
function Layer:SetShaderClipping(clipStart, clipEnd) end

---@param brushType string Only strokes of this brush type will be affected
---@param clipStart number The amount of the stroke to hide from the start (0-1)
---@param clipEnd number The amount of the stroke to hide from the end (0-1)
function Layer:SetShaderClipping(brushType, clipStart, clipEnd) end

---@param parameter string The shader parameter name
---@param value number The new value
function Layer:SetShaderFloat(parameter, value) end

---@param brushType string Only strokes of this brush type will be affected
---@param parameter string The shader parameter name
---@param value number The new value
function Layer:SetShaderFloat(brushType, parameter, value) end

---@param parameter string The shader parameter name
---@param color Color The new color
function Layer:SetShaderColor(parameter, color) end

---@param brushType string Only strokes of this brush type will be affected
---@param parameter string The shader parameter name
---@param color Color The new color
function Layer:SetShaderColor(brushType, parameter, color) end

---@param parameter string The shader parameter name
---@param image Image The new image to use as a texture
function Layer:SetShaderTexture(parameter, image) end

---@param brushType string Only strokes of this brush type will be affected
---@param parameter string The shader parameter name
---@param image Image The new image to use as a texture
function Layer:SetShaderTexture(brushType, parameter, image) end

---@param parameter string The shader parameter name
---@param x number The new x value
---@param y? number The new y value
---@param z? number The new z value
---@param w? number The new w value
function Layer:SetShaderVector(parameter, x, y, z, w) end

---@param brushType string Only strokes of this brush type will be affected
---@param parameter string The shader parameter name
---@param x number The new x value
---@param y? number The new y value
---@param z? number The new z value
---@param w? number The new w value
function Layer:SetShaderVector(brushType, parameter, x, y, z, w) end



---@class LayerList
---@field last Layer Returns the last layer
---@field main Layer Returns the main layer
---@field count number The number of layers
---@field active Layer Returns the active layer
LayerList = {}


---@class Math
---@field deg2Rad number A constant that you multiply with a value in degrees to convert it to radians
---@field epsilon number The smallest value that a float can have such that 1.0 plus this does not equal 1.0
---@field positiveInfinity number Positive Infinity
---@field negativeInfinity number Negative Infinity
---@field pi number The value of Pi
---@field rad2Deg number A constant that you multiply with a value in radians to convert it to degrees
Math = {}
---@param f number The input value
---@return number # The absolute value of f
function Math:Abs(f) end

---@param f number The input value
---@return number # The angle in radians whose cosine is f
function Math:Acos(f) end

---@param a number The first value
---@param b number The second value
---@return boolean # True if the difference between the values is less than Math.epsilon
function Math:Approximately(a, b) end

---@param f number The input value
---@return number # The angle in radians whose sine is f
function Math:Asin(f) end

---@param f number The input value
---@return number # The angle in radians whose tangent is f
function Math:Atan(f) end

---@param y number The numerator value
---@param x number The denominator value
---@return number # The angle in radians whose tan is y/x
function Math:Atan2(y, x) end

---@param f number The input value
---@return number # The smallest integer greater to or equal to f
function Math:Ceil(f) end

---@param f number The input value
---@param min number The minimum value
---@param max number The maximum value
---@return number # min if f < min, max if f > max otherwise f
function Math:Clamp(f, min, max) end

---@param value number The input value
---@return number # 0 if f < 0, 1 if f > 1 otherwise f
function Math:Clamp01(value) end

---@param value number The input value
---@return number # The closest power of two
function Math:ClosestPowerOfTwo(value) end

---@param f number The input value in radians
---@return number # The cosine of angle f
function Math:Cos(f) end

---@param a number The first value in degrees
---@param b number The second value in degrees
---@return number # The smaller of the two angles in degrees between input and target
function Math:DeltaAngle(a, b) end

---@param power number The input value
---@return number # Returns e raised to the specified power
function Math:Exp(power) end

---@param f number The input value
---@return number # The largest integer that is less than or equal to the input
function Math:Floor(f) end

---@param min number The minimum value
---@param max number The maximum value
---@param t number The input value
---@return number # A value between 0 and 1 representing how far t is between min and max
function Math:InverseLerp(min, max, t) end

---@param value number The input value
---@return boolean # The logarithm of f in base b
function Math:IsPowerOfTwo(value) end

---@param min number The minimum value
---@param max number The maximum value
---@param t number The input value
---@return number # A value between min and max representing how far t is between 0 and 1
function Math:Lerp(min, max, t) end

---@param min number The start angle in degrees
---@param max number The end angle in degrees
---@param a number The input value in degrees
---@return number # An angle between min and max representing how far t is between 0 and 1
function Math:LerpAngle(min, max, a) end

---@param min number The minimum value
---@param max number The maximum value
---@param t number The input value
---@return number # A value representing t scaled from the range 0:1 to a new range min:max
function Math:LerpUnclamped(min, max, t) end

---@param f number The input value
---@param b number The base
---@return number # The logarithm of f in base b
function Math:Log(f, b) end

---@param f number The input value
---@return number # The base 10 logarithm of a specified number
function Math:Log10(f) end

---@param a number The first input value
---@param b number The second input value
---@return number # The largest of a and b
function Math:Max(a, b) end

---@param values number[] A list of numbers
---@return number # The largest value in the list
function Math:Max(values) end

---@param a number The first input value
---@param b number The second input value
---@return number # The smaller of a and b
function Math:Min(a, b) end

---@param values number[] A list of numbers
---@return number # The smallest value in a sequence of float numbers
function Math:Min(values) end

---@param current number The input value
---@param target number The target value
---@param maxDelta number The largest change allowed each time
---@return number # The input + or - maxDelta but clamped to it won't overshoot the target value
function Math:MoveTowards(current, target, maxDelta) end

---@param value number The input value
---@return number # The smallest power of two greater than or equal to the specified number
function Math:NextPowerOfTwo(value) end

---@param x number The input value
---@param y number The power to raise to
---@return number # Returns the value of the perlin noise as coordinates x,y
function Math:PerlinNoise(x, y) end

---@param t number The input value
---@param length number The upper limit
---@return number # A value that is never larger than length and never smaller than 0
function Math:PingPong(t, length) end

---@param f number The input value
---@param p number The power to raise to
---@return number # Returns f raised to the specified power
function Math:Pow(f, p) end

---@param t number The input value
---@param length number The upper limit
---@return number # A value that is never larger than length and never smaller than 0
function Math:Repeater(t, length) end

---@param f number The input value
---@return number # The nearest integer value to f
function Math:Round(f) end

---@param f number The input value
---@return number # The sign of f
function Math:Sign(f) end

---@param f number The input value in radians
---@return number # The sine of angle f
function Math:Sin(f) end

---@param f number The input value
---@return number # The square root of f
function Math:Sqrt(f) end

---@param from number The lower range
---@param to number The upper range
---@param t number The input value
---@return number # The input smoothly interpolated between the range [from, to] by the ratio t
function Math:SmoothStep(from, to, t) end

---@param f number The input value
---@return number # The tangent of an angle
function Math:Tan(f) end

---@param f number The input value in radians
---@return number # The hyperbolic sine of f
function Math:Sinh(f) end

---@param f number The input value in radians
---@return number # The hyperbolic cosine of f
function Math:Cosh(f) end

---@param f number The input value in radians
---@return number # The hyperbolic tangent of f
function Math:Tanh(f) end



---@class Model
---@field index number The index of the active Model Widget
---@field layer Layer The layer the model is on
---@field group Group The group this model is part of
---@field transform Transform The transformation of the Model Widget
---@field position Vector3 | number[] The 3D position of the Model Widget
---@field rotation Rotation | number[] The 3D orientation of the Model Widget
---@field scale number The scale of the Model Widget
Model = {}
---@param filename string The filename of the model to be imported
---@return Model # Returns the Model instance
function Model:Import(filename) end


function Model:Select() end


function Model:Deselect() end


function Model:Delete() end



---@class ModelList
---@field lastSelected Model Returns the last model that was selected
---@field last Model Returns the last Model
---@field count number The number of models
ModelList = {}


---@class Path
---@field count number Returns the number of points in this path
---@field last Transform Returns the last point in this path
Path = {}

---@return Path # 
function Path:New() end

---@param transformList Transform[] The list of transforms
---@return Path # 
function Path:New(transformList) end

---@param positionList Vector3[] The list of positions
---@return Path # 
function Path:New(positionList) end

---@param index number Index of control point to use
---@return Vector3 # 
function Path:GetDirection(index) end

---@param index number Index of control point to use
---@return Vector3 # 
function Path:GetNormal(index) end

---@param index number Index of control point to use
---@return Vector3 # 
function Path:GetTangent(index) end


function Path:Draw() end

---@param transform Transform The transform to be inserted at the end of the path
function Path:Insert(transform) end

---@param transform Transform The transform to be inserted
---@param index number The index at which to insert the transform
function Path:Insert(transform, index) end

---@param transform Transform The transform to be applied to all points in the path
function Path:TransformBy(transform) end

---@param amount Vector3 The distance to move the points
function Path:TranslateBy(amount) end

---@param amount Rotation The amount by which to rotate the path
function Path:RotateBy(amount) end

---@param scale Vector3 The scaling factor to apply to the path
function Path:ScaleBy(scale) end


function Path:Center() end

---@param index number The index of the point to make the new first point
function Path:StartingFrom(index) end

---@param point Vector3 The 3D position that we are seeking the closest to
---@return number # 
function Path:FindClosest(point) end


---@return number # 
function Path:FindMinimumX() end


---@return number # 
function Path:FindMinimumY() end


---@return number # 
function Path:FindMinimumZ() end


---@return number # 
function Path:FindMaximumX() end


---@return number # 
function Path:FindMaximumY() end


---@return number # 
function Path:FindMaximumZ() end

---@param size? number The size of the cube to fit the path into
function Path:Normalize(size) end

---@param spacing number The space between points in the new path
function Path:SampleByDistance(spacing) end

---@param count number The number of points in the new path
function Path:SampleByCount(count) end

---@param parts number Number of parts to subdivide into
function Path:SubdivideSegments(parts) end

---@param startTransform Transform Starting transformation
---@param endTransform Transform End transformation
---@param startTangent Vector3 Starting tangent
---@param endTangent Vector3 End tangent
---@param resolution number Resolution of the spline
---@param tangentStrength? number Strength of the tangent
---@return Path # A new Path
function Path:Hermite(startTransform, endTransform, startTangent, endTangent, resolution, tangentStrength) end



---@class PathList
---@field count number Gets the number of paths in the PathList
---@field pointCount number Gets the number of points in all paths in the PathList
PathList = {}

---@return PathList # 
function PathList:New() end

---@param pathList Path[] A list of Paths .
---@return PathList # 
function PathList:New(pathList) end


function PathList:Draw() end

---@param text string Input text to generate a path.
---@return PathList # 
function PathList:FromText(text) end

---@param path Path The path to be inserted.
function PathList:Insert(path) end

---@param path Path The path to be inserted
---@param index number Inserts the new path at this position in the list of paths
function PathList:Insert(path, index) end

---@param transform Transform The point to be inserted
function PathList:InsertPoint(transform) end

---@param transform Transform The point to be inserted
---@param pathIndex number Index of the path to add the point to
---@param pointIndex number Inserts the point at this index in the list of points
function PathList:InsertPoint(transform, pathIndex, pointIndex) end

---@param transform Transform A Transform specifying the translation, rotation and scale to apply
function PathList:TransformBy(transform) end

---@param amount Vector3 The amount to move the paths by
function PathList:TranslateBy(amount) end

---@param rotation Rotation The amount to rotate the paths by
function PathList:RotateBy(rotation) end

---@param scale Vector3 The amount to scale the paths by
function PathList:ScaleBy(scale) end

---@param scale number The amount to scale the paths by
function PathList:ScaleBy(scale) end


function PathList:Center() end

---@param size? number The size of the cube to fit inside
function PathList:Normalize(size) end

---@param spacing number The distance between each new point
function PathList:SampleByDistance(spacing) end

---@param count number Number of points in the new path
function PathList:SampleByCount(count) end

---@param parts number Number of parts to subdivide each path segment into
function PathList:SubdivideSegments(parts) end


---@return Path # A single path
function PathList:Join() end


---@return Path # The path with the most control points
function PathList:Longest() end



---@class Path2d
---@field count number Returns the number of points in this path
---@field last Transform Returns the last point in this path
Path2d = {}

---@return Path2d # 
function Path2d:New() end

---@param positionList Vector2[] The list of points
---@return Path2d # 
function Path2d:New(positionList) end

---@param positionList Vector3[] The list of points
---@return Path2d # 
function Path2d:New(positionList) end

---@param point Vector2 The point to be inserted at the end of the path
function Path2d:Insert(point) end

---@param point Vector2 The point to be inserted
---@param index number The index at which to insert the point
function Path2d:Insert(point, index) end


---@return Path # A 3D Path based on the input but with all x as 0: (0, inX, inY)
function Path2d:OnX() end


---@return Path # A 3D Path based on the input but with all y as 0: (inX, 0, inY)
function Path2d:OnY() end


---@return Path # A 3D Path based on the input but with all z as 0: (inX, inY, 0)
function Path2d:OnZ() end

---@param transform Transform The transform to be applied to all points in the path
function Path2d:TransformBy(transform) end

---@param amount Vector2 The distance to move the points
function Path2d:TranslateBy(amount) end

---@param amount Rotation The amount by which to rotate the path
function Path2d:RotateBy(amount) end

---@param scale number The scaling factor to apply to the path
function Path2d:ScaleBy(scale) end

---@param x number The x scaling factor to apply to the path
---@param y number The y scaling factor to apply to the path
function Path2d:ScaleBy(x, y) end

---@param scale Vector2 The scaling factor to apply to the path
function Path2d:ScaleBy(scale) end


function Path2d:Center() end

---@param index number The index of the point to make the new first point
function Path2d:StartingFrom(index) end

---@param point Vector2 The 3D position that we are seeking the closest to
---@return number # 
function Path2d:FindClosest(point) end


---@return number # 
function Path2d:FindMinimumX() end


---@return number # 
function Path2d:FindMinimumY() end


---@return number # 
function Path2d:FindMaximumX() end


---@return number # 
function Path2d:FindMaximumY() end

---@param size? number The size of the square to fit the path into
function Path2d:Normalize(size) end

---@param sides number The number of sides for the polygon
---@return Path2d # The new path
function Path2d:Polygon(sides) end

---@param spacing number The space between points in the new pat
function Path2d:Resample(spacing) end



---@class Pointer
---@field isDrawing boolean True if the pointer is currently drawing a stroke, otherwise false
---@field layer Layer Sets the layer that the pointer will draw on. Must be set before starting a new stroke
---@field color Color | number[] Sets the color of the strokes created by this pointer. Must be set before starting a new stroke
---@field brush string Sets the brush type the pointer will draw. Must be set before starting a new stroke
---@field size number Sets the size of the brush strokes this pointer will draw. Must be set before starting a new stroke
---@field pressure number Sets the pressure of the stroke being drawn
---@field transform Transform The position and orientation of the pointer
---@field position Vector3 | number[] The 3D position of this pointer
---@field rotation Rotation | number[] The 3D orientation of the Pointer
Pointer = {}

---@return Pointer # The new pointer
function Pointer:New() end



---@class Random
---@field insideUnitCircle Vector2 | number[] Returns a random 2d point inside a circle of radius 1
---@field insideUnitSphere Vector3 | number[] Returns a random 3d point inside a sphere of radius 1
---@field onUnitSphere Vector3 | number[] Returns a random 3d point on the surface of a sphere of radius 1
---@field rotation Rotation | number[] Returns a random rotation
---@field rotationUniform Rotation | number[] Returns a random rotation with uniform distribution
---@field value number Returns a random number between 0 and 1
---@field color Color | number[] Returns a random color
Random = {}
---@param hueMin number Minimum hue
---@param hueMax number Maximum hue
---@param saturationMin number Minimum saturation
---@param saturationMax number Maximum saturation
---@param valueMin number Minimum brightness
---@param valueMax number Maximum brightness
---@return Color # The new random color
function Random:ColorHSV(hueMin, hueMax, saturationMin, saturationMax, valueMin, valueMax) end

---@param seed number The seed for the random number generator
function Random:InitState(seed) end

---@param min number Minimum value
---@param max number Maximum value
---@return number # A random whole number >= min and <= max
function Random:Range(min, max) end

---@param min number Minimum value
---@param max number Maximum value
---@return number # The random number  >= min and <= max
function Random:Range(min, max) end



---@class Rotation
---@field x number The amount of rotation around the x axis in degrees
---@field y number The amount of rotation around the y axis in degrees
---@field z number The amount of rotation around the z axis in degrees
---@field zero Rotation | number[] A rotation of zero in all axes
---@field left Rotation | number[] A 90 degree anti-clockwise rotation in the y axis (yaw)
---@field right Rotation | number[] A 90 degree clockwise rotation in the y axis (yaw)
---@field up Rotation | number[] A 90 degree clockwise rotation in the x axis (pitch)
---@field down Rotation | number[] A 90 degree anti-clockwise rotation in the x axis (pitch)
---@field normalized Rotation | number[] Converts this rotation to one with the same orientation but with a magnitude of 1
---@field inverse Rotation | number[] Returns the Inverse of this rotation
---@field angle number The angle in degrees of the angle-axis representation of this rotation
---@field axis Vector3 | number[] The axis part of the angle-axis representation of this rotation
Rotation = {}
---@param x? number The angle of rotation on the x axis in degrees
---@param y? number The angle of rotation on the y axis in degrees
---@param z? number The angle of rotation on the z axis in degrees
---@return Rotation # 
function Rotation:New(x, y, z) end

---@param fromDirection Vector3 The starting direction
---@param toDirection Vector3 The target direction
---@return Rotation # A rotation that would change one direction to the other
function Rotation:SetFromToRotation(fromDirection, toDirection) end

---@param view Vector3 The direction to look in
---@return Rotation # The new Rotation
function Rotation:SetLookRotation(view) end

---@param view Vector3 The direction to look in
---@param up Vector3 The vector that defines in which direction is up
---@return Rotation # The new Rotation
function Rotation:SetLookRotation(view, up) end

---@param a Rotation The first rotation angle
---@param b Rotation The second rotation angle
---@return number # Returns the angle in degrees between two rotations
function Rotation:Angle(a, b) end

---@param angle number The angle in degrees
---@param axis Vector3 The axis of rotation
---@return Rotation # Returns a Quaternion that represents the rotation
function Rotation:AngleAxis(angle, axis) end

---@param a Rotation The first rotation
---@param b Rotation The second rotation
---@return number # Returns the dot product between two rotations
function Rotation:Dot(a, b) end

---@param from Vector3 The initial direction vector
---@param to Vector3 The target direction vector
---@return Rotation # Returns a Quaternion that represents the rotation
function Rotation:FromToRotation(from, to) end

---@param a Rotation The first rotation
---@param b Rotation The second rotation
---@param t number A ratio between 0 and 1
---@return Rotation # Interpolated rotation
function Rotation:Lerp(a, b, t) end

---@param a Rotation The first rotation
---@param b Rotation The second rotation
---@param t number A ratio between 0 and 1
---@return Rotation # Interpolated rotation
function Rotation:LerpUnclamped(a, b, t) end

---@param forward Vector3 Vector3 forward direction
---@return Rotation # Rotation with specified forward direction
function Rotation:LookRotation(forward) end

---@param forward Vector3 Vector3 forward direction
---@param up Vector3 Vector3 up direction
---@return Rotation # Rotation with specified forward and up directions
function Rotation:LookRotation(forward, up) end

---@param a Rotation The input rotation
---@return Rotation # Normalized rotation
function Rotation:Normalize(a) end

---@param from Rotation Rotation from
---@param to Rotation Rotation to
---@param maxDegreesDelta number Max degrees delta
---@return Rotation # Rotation rotated from towards to
function Rotation:RotateTowards(from, to, maxDegreesDelta) end

---@param a Rotation The first rotation
---@param b Rotation The second rotation
---@param t number A ratio between 0 and 1
---@return Rotation # Spherically interpolated rotation
function Rotation:Slerp(a, b, t) end

---@param a Rotation The first rotation
---@param b Rotation The second rotation
---@param t number A ratio
---@return Rotation # Spherically interpolated rotation
function Rotation:SlerpUnclamped(a, b, t) end

---@param other Rotation The other rotation
---@return Rotation # The rotation that represents applying both rotations in turn
function Rotation:Multiply(other) end

---@param other Rotation The rotation to compare
---@return boolean # true if this rotation is not equal to the specified rotation; otherwise, false
function Rotation:NotEquals(other) end



---@class Selection
Selection = {}

function Selection:Deselect() end


function Selection:Duplicate() end


function Selection:Group() end


function Selection:Invert() end


function Selection:Flip() end


function Selection:Recolor() end


function Selection:Rebrush() end


function Selection:Resize() end

---@param count number The number of points to trim from each stroke
function Selection:Trim(count) end



---@class Sketch
---@field cameraPaths CameraPathList | CameraPath[] Returns a list of active camera paths in the sketch
---@field strokes StrokeList | Stroke[] Returns a list of all active strokes in the sketch
---@field layers LayerList | Layer[] Returns a list of all layers in the sketch
---@field mainLayer Layer Returns a list of all layers in the sketch
---@field groups GroupList | Group[] All the groups in this sketch
---@field images ImageList | Image[] Returns a list of active image widgets in the sketch
---@field videos VideoList | Video[] Returns a list of active video widgets in the sketch
---@field models ModelList | Model[] Returns a list of active model widgets in the sketch
---@field guides GuideList | Guide[] Returns a list of active stencil widgets in the sketch
---@field environments EnvironmentList | Environment[] Returns a list of all the available environments
---@field ambientLightColor Color | number[] The ambient light color
---@field mainLightColor Color | number[] The main light's color
---@field secondaryLightColor Color | number[] The secondary light's color
---@field mainLightRotation Rotation | number[] The main light's rotation
---@field secondaryLightRotation Rotation | number[] The secondary light's rotation
Sketch = {}
---@param name string The filename of the sketch
function Sketch:Open(name) end

---@param overwrite boolean If set to true, overwrite the existing file. If false, the method will not overwrite the file
function Sketch:Save(overwrite) end

---@param name string The new name for the sketch
function Sketch:SaveAs(name) end


function Sketch:Export() end


function Sketch:NewSketch() end

---@param filename string The filename of the image
function Sketch:ImportSkybox(filename) end



---@class Spectator
---@field canSeeWidgets boolean Sets whether Widgets are visible to the spectator camera
---@field canSeeStrokes boolean Sets whether Strokes are visible to the spectator camera
---@field canSeeSelection boolean Sets whether Selection are visible to the spectator camera
---@field canSeeHeadset boolean Sets whether Headset are visible to the spectator camera
---@field canSeePanels boolean Sets whether Panels are visible to the spectator camera
---@field canSeeUi boolean Sets whether Ui are visible to the spectator camera
---@field canSeeUsertools boolean Sets whether Usertools are visible to the spectator camera
---@field active boolean Is the spectator camera currently active?
---@field position Vector3 | number[] The 3D position of the Spectator Camera Widget
---@field rotation Rotation | number[] The 3D orientation of the Spectator Camera
---@field lockedToScene boolean Sets whether the spectator camera moves with the scene or with the user
Spectator = {}
---@param position Vector3 The point in the scene to look towards
function Spectator:LookAt(position) end


function Spectator:Stationary() end


function Spectator:SlowFollow() end


function Spectator:Wobble() end


function Spectator:Circular() end



---@class Stroke
---@field path Path | Transform[] The control points of this stroke from a Path
---@field brushType string The stroke's brush type
---@field brushSize number The stroke's size
---@field brushColor Color | number[] The stroke's Color
---@field layer Layer The layer the stroke is on
---@field group Group The group this stroke is part of
---@field count number The number of control points in this stroke
Stroke = {}
---@param brushName string The name (or guid) of the brush to get the material from
function Stroke:ChangeMaterial(brushName) end


function Stroke:Delete() end


function Stroke:Select() end


function Stroke:Deselect() end

---@param from number Start stroke index (0 is the first stroke that was drawn
---@param to number End stroke index
function Stroke:SelectRange(from, to) end

---@param from number Start stroke index (0 is the first stroke that was drawn
---@param to number End stroke index
---@return Stroke # 
function Stroke:JoinRange(from, to) end


---@return Stroke # 
function Stroke:JoinToPrevious() end

---@param stroke2 Stroke The stroke to join to this one
---@return Stroke # 
function Stroke:Join(stroke2) end

---@param name string Name of the file to be merged
function Stroke:MergeFrom(name) end

---@param clipStart number The amount of the stroke to hide from the start (0-1)
---@param clipEnd number The amount of the stroke to hide from the end (0-1)
function Stroke:SetShaderClipping(clipStart, clipEnd) end

---@param parameter string The shader parameter name
---@param value number The new value
function Stroke:SetShaderFloat(parameter, value) end

---@param parameter string The shader parameter name
---@param color Color The new color
function Stroke:SetShaderColor(parameter, color) end

---@param parameter string The shader parameter name
---@param image Image The new image to use as a texture
function Stroke:SetShaderTexture(parameter, image) end

---@param parameter string The shader parameter name
---@param x number The new x value
---@param y? number The new y value
---@param z? number The new z value
---@param w? number The new w value
function Stroke:SetShaderVector(parameter, x, y, z, w) end



---@class StrokeList
---@field lastSelected Stroke | Transform[] Returns the last stroke that was selected
---@field last Stroke | Transform[] Returns the last Stroke
---@field count number The number of strokes
StrokeList = {}

function StrokeList:Select() end


function StrokeList:Deselect() end


function StrokeList:Delete() end

---@param clipStart number The amount of the stroke to hide from the start (0-1)
---@param clipEnd number The amount of the stroke to hide from the end (0-1)
function StrokeList:SetShaderClipping(clipStart, clipEnd) end

---@param parameter string The shader parameter name
---@param value number The new value
function StrokeList:SetShaderFloat(parameter, value) end

---@param parameter string The shader parameter name
---@param color Color The new color
function StrokeList:SetShaderColor(parameter, color) end

---@param parameter string The shader parameter name
---@param image Image The new image to use as a texture
function StrokeList:SetShaderTexture(parameter, image) end

---@param parameter string The shader parameter name
---@param x number The new x value
---@param y? number The new y value
---@param z? number The new z value
---@param w? number The new w value
function StrokeList:SetShaderVector(parameter, x, y, z, w) end



---@class Svg
Svg = {}
---@param svgPath string The SVG path string to parse
---@return PathList # Returns a PathList representing the parsed SVG path
function Svg:ParsePathString(svgPath) end

---@param svg string A text string that is valid SVG document
---@param offsetPerPath? number Each path can be lifted to form a layered result
---@param includeColors? boolean Whether the colors from the SVG are used
---@return PathList # Returns a PathList representing the parsed SVG document
function Svg:ParseDocument(svg, offsetPerPath, includeColors) end

---@param svg string The SVG path string to draw
---@param tr? Transform The transform to apply to the result
function Svg:DrawPathString(svg, tr) end

---@param svg string A text string that is a valid SVG document
---@param tr? Transform The transform (position, rotation and scale) to apply to the result
function Svg:DrawDocument(svg, tr) end



---@class Symmetry
---@field current SymmetrySettings The current symmetry settings
---@field brushOffset Vector3 | number[] Gets the offset betwen the current brush position and the symmetry widget
---@field wandOffset Vector3 | number[] Gets the offset betwen the current wand position and the symmetry widget
Symmetry = {}

function Symmetry:SummonWidget() end

---@param angle number The angle in degrees to sample the radius at
---@param minorRadius number The minor radius of the ellipse (The major radius is always 1)
---@return number # 
function Symmetry:Ellipse(angle, minorRadius) end

---@param angle number The angle in degrees to sample the radius at
---@return number # 
function Symmetry:Square(angle) end

---@param angle number The angle in degrees to sample the radius at
---@param n number The exponent of the superellipse. This determines the roundness vs sharpness of the corners of the superellipse. For n = 2, you get an ellipse. As n increases, the shape becomes more rectangular with sharper corners. As n approaches infinity, the superellipse becomes a rectangle. If n is less than 1, the shape becomes a star with pointed tips.
---@param a? number The horizontal radius of the superellipse
---@param b? number The vertical radius of the superellipse
---@return number # 
function Symmetry:Superellipse(angle, n, a, b) end

---@param angle number The angle in degrees to sample the radius at
---@param size number Half the length of a side or the distance from the center to any edge midpoint
---@param cornerRadius number The radius of the rounded corners
---@return number # 
function Symmetry:Rsquare(angle, size, cornerRadius) end

---@param angle number The angle in degrees to sample the radius at
---@param numSides number The number of sides of the polygon
---@param radius? number The distance from the center to any vertex
---@return number # 
function Symmetry:Polygon(angle, numSides, radius) end


function Symmetry:ClearColors() end

---@param color Color The color to add
function Symmetry:AddColor(color) end

---@param colors Color[] The list of colors to set
function Symmetry:SetColors(colors) end


---@return Color[] # 
function Symmetry:GetColors() end

---@param brush string The brush to add. Either the name or the GUID of the brush
function Symmetry:AddBrush(brush) end


function Symmetry:ClearBrushes() end

---@param brushes string[] The list of brushes to set. Either the names or the GUIDs of the brushes
function Symmetry:SetBrushes(brushes) end


---@return string[] # 
function Symmetry:GetBrushNames() end


---@return string[] # 
function Symmetry:GetBrushGuids() end

---@param path Path The path to convert
---@return Path # 
function Symmetry:PathToPolar(path) end



---@class SymmetrySettings
---@field mode SymmetryMode The symmetry mode
---@field transform Transform The transform of the symmetry widget
---@field position Vector3 | number[] The position of the symmetry widget
---@field rotation Rotation | number[] The rotation of the symmetry widget
---@field spin Vector3 | number[] How fast the symmetry widget is spinning in each axis
---@field pointType SymmetryPointType The type of point symmetry
---@field pointOrder number The order of point symmetry (how many times it repeats around it's axis)
---@field wallpaperType SymmetryWallpaperType The type of wallpaper symmetry
---@field wallpaperRepeatX number How many times the wallpaper symmetry repeats in the X axis
---@field wallpaperRepeatY number How many times the wallpaper symmetry repeats in the Y axis
---@field wallpaperScale number The overall scale of the wallpaper symmetry
---@field wallpaperScaleX number The scale of the wallpaper symmetry in the X axis
---@field wallpaperScaleY number The scale of the wallpaper symmetry in the Y axis
---@field wallpaperSkewX number The skew of the wallpaper symmetry in the X axis
---@field wallpaperSkewY number The skew of the wallpaper symmetry in the Y axis
SymmetrySettings = {}

---@return SymmetrySettings # 
function SymmetrySettings:Duplicate() end



---@class Timer
Timer = {}
---@param fn function The function to call
---@param interval number How long to wait inbetween repeated calls
---@param delay? number How long to wait until the first call
---@param repeats? number The number of times to call the function. A value of -1 means "run forever"
function Timer:Set(fn, interval, delay, repeats) end

---@param fn function The function to remove
function Timer:Unset(fn) end



---@class Transform
---@field inverse Transform The inverse of this transform
---@field up Vector3 | number[] A translation of 1 in the y axis
---@field down Vector3 | number[] A translation of -1 in the y axis
---@field right Vector3 | number[] A translation of 1 in the x axis
---@field left Vector3 | number[] A translation of -1 in the x axis
---@field forward Vector3 | number[] A translation of 1 in the z axis
---@field back Vector3 | number[] A translation of -1 in the z axis
---@field position Vector3 | number[] Get or set the position of this transform
---@field rotation Rotation | number[] Get or set the rotation of this transform
---@field scale number Get or set the scale of this transform
---@field identity Transform A transform that does nothing. No translation, rotation or scaling
Transform = {}
---@param transform Transform The transform to apply
---@return Transform # 
function Transform:TransformBy(transform) end

---@param translation Vector3 The translation to apply
---@return Transform # 
function Transform:TranslateBy(translation) end

---@param x number The x translation to apply
---@param y number The y translation to apply
---@param z number The z translation to apply
---@return Transform # 
function Transform:TranslateBy(x, y, z) end

---@param rotation Rotation The rotation to apply
---@return Transform # 
function Transform:RotateBy(rotation) end

---@param x number The x rotation to apply
---@param y number The y rotation to apply
---@param z number The z rotation to apply
---@return Transform # 
function Transform:RotateBy(x, y, z) end

---@param scale number The scale value to apply
---@return Transform # 
function Transform:ScaleBy(scale) end

---@param translation Vector3 The translation amount
---@param rotation Rotation The rotation amount
---@param scale number The scale amount
---@return Transform # 
function Transform:New(translation, rotation, scale) end

---@param translation Vector3 The translation amount
---@param rotation Rotation The rotation amount
---@return Transform # 
function Transform:New(translation, rotation) end

---@param translation Vector3 The translation amount
---@return Transform # 
function Transform:New(translation) end

---@param translation Vector3 The translation amount
---@param scale number The scale amount
---@return Transform # 
function Transform:New(translation, scale) end

---@param x number The x translation amount
---@param y number The y translation amount
---@param z number The z translation amount
---@return Transform # 
function Transform:Position(x, y, z) end

---@param position Vector3 The Vector3 position
---@return Transform # 
function Transform:Position(position) end

---@param x number The x rotation amount
---@param y number The y rotation amount
---@param z number The z rotation amount
---@return Transform # 
function Transform:Rotation(x, y, z) end

---@param rotation Rotation The rotation
---@return Transform # 
function Transform:Rotation(rotation) end

---@param amount number The scale amount
---@return Transform # 
function Transform:Scale(amount) end

---@param other Transform The Transform to apply to this one
---@return Transform # 
function Transform:Multiply(other) end

---@param a Transform The first transform
---@param b Transform The second transform
---@param t number The value between 0 and 1 that controls how far between a and b the new transform is
---@return Transform # A transform that blends between a and b based on the value of t
function Transform:Lerp(a, b, t) end



---@class User
---@field position Vector3 | number[] The 3D position of the user's viewpoint
---@field rotation Rotation | number[] The 3D orientation of the User (usually only a rotation around the Y axis unless you've set it manually or disabled axis locking
User = {}


---@class Vector2
---@field x number The x coordinate
---@field y number The y coordinate
---@field magnitude number The length of this vector
---@field sqrMagnitude number The square of the length of this vector (faster to calculate if you're just comparing two lengths)
---@field normalized Vector2 | number[] Returns a vector with the same distance but witha length of 1
---@field down Vector2 | number[] A vector of -1 in the y axis
---@field left Vector2 | number[] A vector of -1 in the x axis
---@field negativeInfinity Vector2 | number[] A vector of negative infinity in all axes
---@field one Vector2 | number[] A vector of 1 in all axes
---@field positiveInfinity Vector2 | number[] A vector of positive infinity in all axes
---@field right Vector2 | number[] A vector of 1 in the x axis
---@field up Vector2 | number[] A vector of 1 in the y axis
---@field zero Vector2 | number[] A vector of 0 in all axes
Vector2 = {}
---@param x? number The x coordinate
---@param y? number The y coordinate
---@return Vector2 # 
function Vector2:New(x, y) end

---@param other Vector2 The other vector
---@return number # 
function Vector2:Angle(other) end

---@param maxLength number The maximum length of the new vector
---@return Vector2 # 
function Vector2:ClampMagnitude(maxLength) end

---@param other Vector2 The other vector
---@return number # 
function Vector2:Distance(other) end

---@param a Vector2 The first vector
---@param b Vector2 The second vector
---@return number # 
function Vector2:Dot(a, b) end

---@param a Vector2 The first point
---@param b Vector2 The second point
---@param t number The value between 0 and 1 that controls how far between a and b the new point is
---@return Vector2 # A point somewhere between a and b based on the value of t
function Vector2:Lerp(a, b, t) end

---@param a Vector2 The first point
---@param b Vector2 The second point
---@param t number The value that controls how far between (or beyond) a and b the new point is
---@return Vector2 # A point somewhere between a and b based on the value of t
function Vector2:LerpUnclamped(a, b, t) end

---@param a Vector2 The first vector
---@param b Vector2 The second vector
---@return Vector2 # 
function Vector2:Max(a, b) end

---@param a Vector2 The first vector
---@param b Vector2 The second vector
---@return Vector2 # 
function Vector2:Min(a, b) end

---@param target Vector2 The target point
---@param maxDistanceDelta number The maximum distance to move
---@return Vector2 # 
function Vector2:MoveTowards(target, maxDistanceDelta) end

---@param normal Vector2 The normal vector
---@return Vector2 # 
function Vector2:Reflect(normal) end

---@param other Vector2 The vector to scale by
---@return Vector2 # 
function Vector2:Scale(other) end

---@param other Vector2 The other vector
---@return number # 
function Vector2:SignedAngle(other) end

---@param a Vector2 The first point
---@param b Vector2 The second point
---@param t number The value that controls how far between (or beyond) a and b the new point is
---@return Vector2 # A point somewhere between a and b based on the value of t
function Vector2:Slerp(a, b, t) end

---@param a Vector2 The first point
---@param b Vector2 The second point
---@param t number The value that controls how far between (or beyond) a and b the new point is
---@return Vector2 # A point somewhere between a and b based on the value of t
function Vector2:SlerpUnclamped(a, b, t) end

---@param degrees number The angle in degrees
---@return Vector2 # 
function Vector2:PointOnCircle(degrees) end


---@return Vector3 # A 3D Vector based on the input but with x as 0: (0, inX, inY)
function Vector2:OnX() end


---@return Vector3 # A 3D Vector based on the input but with y as 0: (inX, 0, inY)
function Vector2:OnY() end


---@return Vector3 # A 3D Vector based on the input but with z as 0: (inX, inX, 0)
function Vector2:OnZ() end

---@param other Vector2 The other vector
---@return Vector2 # 
function Vector2:Add(other) end

---@param x number The x value
---@param y number The y value
---@return Vector2 # 
function Vector2:Add(x, y) end

---@param other Vector2 The other vector
---@return Vector2 # 
function Vector2:Subtract(other) end

---@param x number The x value
---@param y number The y value
---@return Vector2 # 
function Vector2:Subtract(x, y) end

---@param value number The value to multiply by
---@return Vector2 # 
function Vector2:Multiply(value) end

---@param other Vector2 The other vector
---@return Vector2 # 
function Vector2:ScaleBy(other) end

---@param x number The x value
---@param y number The y value
---@return Vector2 # 
function Vector2:ScaleBy(x, y) end

---@param value number The value to divide by
---@return Vector2 # 
function Vector2:Divide(value) end

---@param other Vector2 The other vector
---@return boolean # 
function Vector2:NotEquals(other) end

---@param x number The x value
---@param y number The y value
---@return boolean # 
function Vector2:NotEquals(x, y) end



---@class Vector3
---@field x number The x coordinate
---@field y number The y coordinate
---@field z number The z coordinate
---@field magnitude number Returns the length of this vector
---@field sqrMagnitude number Returns the squared length of this vector
---@field normalized Vector3 | number[] Returns a vector with the same direction but with a length of 1
---@field back Vector3 | number[] A vector of -1 in the z axis
---@field down Vector3 | number[] A vector of -1 in the y axis
---@field forward Vector3 | number[] A vector of 1 in the z axis
---@field left Vector3 | number[] A vector of -1 in the x axis
---@field negativeInfinity Vector3 | number[] A vector of -infinity in all axes
---@field one Vector3 | number[] A vector of 1 in all axes
---@field positiveInfinity Vector3 | number[] A vector of infinity in all axes
---@field right Vector3 | number[] A vector of 1 in the x axis
---@field up Vector3 | number[] A vector of 1 in the y axis
---@field zero Vector3 | number[] A vector of 0 in all axes
Vector3 = {}
---@param x number The x coordinate
---@param y number The y coordinate
---@param z number The z coordinate
---@return Vector3 # 
function Vector3:New(x, y, z) end

---@param other Vector3 The other vector
---@return number # 
function Vector3:Angle(other) end

---@param maxLength number The maximum length of the returned vector
---@return Vector3 # 
function Vector3:ClampMagnitude(maxLength) end

---@param a Vector3 The first vector
---@param b Vector3 The second vector
---@return Vector3 # 
function Vector3:Cross(a, b) end

---@param other Vector3 The other vector
---@return number # 
function Vector3:Distance(other) end

---@param a Vector3 The first point
---@param b Vector3 The second point
---@param t number The value between 0 and 1 that controls how far between a and b the new point is
---@return Vector3 # A point somewhere between a and b based on the value of t
function Vector3:Lerp(a, b, t) end

---@param a Vector3 The first point
---@param b Vector3 The second point
---@param t number The value that controls how far between (or beyond) a and b the new point is
---@return Vector3 # A point somewhere between a and b based on the value of t
function Vector3:LerpUnclamped(a, b, t) end

---@param a Vector3 The first vector
---@param b Vector3 The second vector
---@return Vector3 # 
function Vector3:Max(a, b) end

---@param a Vector3 The first vector
---@param b Vector3 The second vector
---@return Vector3 # 
function Vector3:Min(a, b) end

---@param target Vector3 The target point
---@param maxDistanceDelta number The maximum distance to move towards the target point
---@return Vector3 # 
function Vector3:MoveTowards(target, maxDistanceDelta) end

---@param other Vector3 The other vector
---@return Vector3 # 
function Vector3:Project(other) end

---@param planeNormal Vector3 The normal vector of the plane
---@return Vector3 # 
function Vector3:ProjectOnPlane(planeNormal) end

---@param normal Vector3 The normal vector
---@return Vector3 # 
function Vector3:Reflect(normal) end

---@param target Vector3 The target vector
---@param maxRadiansDelta number The maximum change in angle
---@param maxMagnitudeDelta number The maximum allowed change in vector magnitude for this rotation
---@return Vector3 # 
function Vector3:RotateTowards(target, maxRadiansDelta, maxMagnitudeDelta) end

---@param other Vector3 The other vector
---@return Vector3 # 
function Vector3:ScaleBy(other) end

---@param other Vector3 The other vector
---@param axis Vector3 The axis around which the vectors are rotated
---@return number # 
function Vector3:SignedAngle(other, axis) end

---@param a Vector3 The first point
---@param b Vector3 The second point
---@param t number The value that controls how far between (or beyond) a and b the new point is
---@return Vector3 # A point somewhere between a and b based on the value of t
function Vector3:Slerp(a, b, t) end

---@param a Vector3 The first point
---@param b Vector3 The second point
---@param t number The value that controls how far between (or beyond) a and b the new point is
---@return Vector3 # A point somewhere between a and b based on the value of t
function Vector3:SlerpUnclamped(a, b, t) end

---@param other Vector3 The other vector
---@return Vector3 # 
function Vector3:Add(other) end

---@param x number The x value
---@param y number The y value
---@param z number The z value
---@return Vector3 # 
function Vector3:Add(x, y, z) end

---@param other Vector3 The vector to subtract
---@return Vector3 # 
function Vector3:Subtract(other) end

---@param x number The x value
---@param y number The y value
---@param z number The z value
---@return Vector3 # 
function Vector3:Subtract(x, y, z) end

---@param value number The scalar value
---@return Vector3 # 
function Vector3:Multiply(value) end

---@param x number The x value
---@param y number The y value
---@param z number The z value
---@return Vector3 # 
function Vector3:ScaleBy(x, y, z) end

---@param value number The scalar value
---@return Vector3 # 
function Vector3:Divide(value) end

---@param other Vector3 The other vector
---@return boolean # 
function Vector3:NotEquals(other) end

---@param x number The x value
---@param y number The y value
---@param z number The z value
---@return boolean # 
function Vector3:NotEquals(x, y, z) end



---@class Video
---@field index number Gets the index of this Video
---@field layer Layer The layer the video is on
---@field group Group The group this video is part of
---@field transform Transform The Transform (position, rotation, scale) of the Video Widget
---@field position Vector3 | number[] The 3D position of the Video Widget
---@field rotation Rotation | number[] The 3D orientation of the Video Widget
---@field scale number The scale of the Video Widget
Video = {}
---@param location string The filename of the video file to import from the user's MediaLibrary/Videos folder
---@return Video # 
function Video:Import(location) end


function Video:Select() end


function Video:Deselect() end


function Video:Delete() end



---@class VideoList
---@field lastSelected Video Returns the last Video that was selected
---@field last Video Returns the last Video
---@field count number The number of videos
VideoList = {}


---@class Visualizer
---@field sampleRate number The current audio sample rate
---@field duration number The current duration of the audio buffer
Visualizer = {}

function Visualizer:EnableScripting() end


function Visualizer:DisableScripting() end

---@param data number[] An array of numbers representing the waveform
function Visualizer:SetWaveform(data) end

---@param data1 number[] An array of numbers representing first FFT band
---@param data2 number[] An array of numbers representing second FFT band
---@param data3 number[] An array of numbers representing third FFT band
---@param data4 number[] An array of numbers representing fourth FFT band
function Visualizer:SetFft(data1, data2, data3, data4) end

---@param x number The first beat value
---@param y number The second beat value
---@param z number The third beat value
---@param w number The fourth beat value
function Visualizer:SetBeats(x, y, z, w) end

---@param x number The first beat accumulator value
---@param y number The second beat accumulator value
---@param z number The third beat accumulator value
---@param w number The fourth beat accumulator value
function Visualizer:SetBeatAccumulators(x, y, z, w) end

---@param peak number The peak value
function Visualizer:SetBandPeak(peak) end



---@class Wand
---@field position Vector3 | number[] The 3D position of the Wand Controller
---@field rotation Rotation | number[] The 3D orientation of the Wand
---@field direction Vector3 | number[] The vector representing the forward direction of the wand controller
---@field pressure number How far the trigger on the wand contrller is pressed in
---@field speed Vector3 | number[] How fast the wand contrller is currently moving
---@field triggerIsPressed boolean Check whether the wand trigger is currently pressed
---@field triggerPressedThisFrame boolean Check whether the wand trigger was pressed during the current frame
Wand = {}
---@param size number The size of the history buffer
function Wand:ResizeHistory(size) end

---@param size number The size of the history buffer
function Wand:SetHistorySize(size) end

---@param back number How far back in the history to get the position from
---@return Vector3 # 
function Wand:PastPosition(back) end

---@param back number How far back in the history to get the rotation from
---@return Rotation # 
function Wand:PastRotation(back) end



---@class Waveform
Waveform = {}
---@param time number The time to sample the waveform at
---@param frequency number The frequency of the wave
---@return number # The value of the wave sampled at the given time
function Waveform:Sine(time, frequency) end

---@param time number The time to sample the waveform at
---@param frequency number The frequency of the wave
---@return number # The value of the wave sampled at the given time
function Waveform:Cosine(time, frequency) end

---@param time number The time to sample the waveform at
---@param frequency number The frequency of the wave
---@return number # The value of the wave sampled at the given time
function Waveform:Triangle(time, frequency) end

---@param time number The time to sample the waveform at
---@param frequency number The frequency of the wave
---@return number # The value of the wave sampled at the given time
function Waveform:Sawtooth(time, frequency) end

---@param time number The time to sample the waveform at
---@param frequency number The frequency of the wave
---@return number # The value of the wave sampled at the given time
function Waveform:Square(time, frequency) end

---@param time number The time to sample the waveform at
---@param frequency number The frequency of the wave
---@param pulseWidth number The width of the pulse
---@return number # The value of the wave sampled at the given time
function Waveform:Pulse(time, frequency, pulseWidth) end

---@param time number The time to sample the waveform at
---@param frequency number The frequency of the wave
---@return number # The value of the wave sampled at the given time
function Waveform:Exponent(time, frequency) end

---@param time number The time to sample the waveform at
---@param frequency number The frequency of the wave
---@param power number The power exponent of the wave
---@return number # The value of the wave sampled at the given time
function Waveform:Power(time, frequency, power) end

---@param time number The time to sample the waveform at
---@param frequency number The frequency of the wave
---@return number # The value of the wave sampled at the given time
function Waveform:Parabolic(time, frequency) end

---@param time number The time to sample the waveform at
---@param frequency number The frequency of the wave
---@param exponent number The exponent of the wave
---@return number # The value of the wave sampled at the given time
function Waveform:ExponentialSawtooth(time, frequency, exponent) end

---@param time number The time to sample the waveform at
---@param frequency number The frequency of the wave
---@return number # The value of the wave sampled at the given time
function Waveform:PerlinNoise(time, frequency) end


---@return number # The value of the wave sampled at the given time
function Waveform:WhiteNoise() end

---@param previous number The previous calculated value to feed back into the function
---@return number # The value of the wave sampled at the given time
function Waveform:BrownNoise(previous) end

---@param previous number The previous calculated value to feed back into the function
---@return number # The value of the wave sampled at the given time
function Waveform:BlueNoise(previous) end

---@param time number The time to start sampling the waveform at
---@param frequency number The frequency of the wave
---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:Sine(time, frequency, duration, sampleRate, amplitude) end

---@param time number The time to start sampling the waveform at
---@param frequency number The frequency of the wave
---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:Cosine(time, frequency, duration, sampleRate, amplitude) end

---@param time number The time to start sampling the waveform at
---@param frequency number The frequency of the wave
---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:Triangle(time, frequency, duration, sampleRate, amplitude) end

---@param time number The time to start sampling the waveform at
---@param frequency number The frequency of the wave
---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:Sawtooth(time, frequency, duration, sampleRate, amplitude) end

---@param time number The time to start sampling the waveform at
---@param frequency number The frequency of the wave
---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:Square(time, frequency, duration, sampleRate, amplitude) end

---@param time number The time to start sampling the waveform at
---@param frequency number The frequency of the wave
---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:Exponent(time, frequency, duration, sampleRate, amplitude) end

---@param time number The time to start sampling the waveform at
---@param frequency number The frequency of the wave
---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:Parabolic(time, frequency, duration, sampleRate, amplitude) end

---@param time number The time to start sampling the waveform at
---@param frequency number The frequency of the wave
---@param pulseWidth number The width of the pulse
---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:Pulse(time, frequency, pulseWidth, duration, sampleRate, amplitude) end

---@param time number The time to start sampling the waveform at
---@param frequency number The frequency of the wave
---@param power number The power exponent of the wave
---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:Power(time, frequency, power, duration, sampleRate, amplitude) end

---@param time number The time to start sampling the waveform at
---@param frequency number The frequency of the wave
---@param exponent number The exponent of the wave
---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:ExponentialSawtoothWave(time, frequency, exponent, duration, sampleRate, amplitude) end

---@param time number The time to start sampling the waveform at
---@param frequency number The frequency of the wave
---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:PerlinNoise(time, frequency, duration, sampleRate, amplitude) end

---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:WhiteNoise(duration, sampleRate, amplitude) end

---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:BrownNoise(duration, sampleRate, amplitude) end

---@param duration number The duration of samples to generate
---@param sampleRate number The sample rate of the generated waveform
---@param amplitude? number The amplitude of the generated waveform
---@return number[] # An array of float values
function Waveform:BlueNoise(duration, sampleRate, amplitude) end



---@class WebRequest
WebRequest = {}
---@param url string The URL to send the request to
---@param onSuccess function A function to call when the request succeeds
---@param onError? function A function to call when the request fails
---@param headers? table A table of key-value pairs to send as headers
---@param context? table A value to pass to the onSuccess and onError functions
function WebRequest:Get(url, onSuccess, onError, headers, context) end

---@param url string The URL to send the request to
---@param postData table A table of key-value pairs to send as POST data
---@param onSuccess function A function to call when the request succeeds
---@param onError function A function to call when the request fails
---@param headers table A table of key-value pairs to send as headers
---@param context table A value to pass to the onSuccess and onError functions
function WebRequest:Post(url, postData, onSuccess, onError, headers, context) end



---@class SymmetryMode
SymmetryMode = {}
SymmetryMode.None = nil
SymmetryMode.Standard = nil
SymmetryMode.Scripted = nil
SymmetryMode.TwoHanded = nil
SymmetryMode.Point = nil
SymmetryMode.Wallpaper = nil



---@class SymmetryPointType
SymmetryPointType = {}
SymmetryPointType.Cn = nil
SymmetryPointType.Cnv = nil
SymmetryPointType.Cnh = nil
SymmetryPointType.Sn = nil
SymmetryPointType.Dn = nil
SymmetryPointType.Dnh = nil
SymmetryPointType.Dnd = nil
SymmetryPointType.T = nil
SymmetryPointType.Th = nil
SymmetryPointType.Td = nil
SymmetryPointType.O = nil
SymmetryPointType.Oh = nil
SymmetryPointType.I = nil
SymmetryPointType.Ih = nil



---@class SymmetryWallpaperType
SymmetryWallpaperType = {}
SymmetryWallpaperType.p1 = nil
SymmetryWallpaperType.pg = nil
SymmetryWallpaperType.cm = nil
SymmetryWallpaperType.pm = nil
SymmetryWallpaperType.p6 = nil
SymmetryWallpaperType.p6m = nil
SymmetryWallpaperType.p3 = nil
SymmetryWallpaperType.p3m1 = nil
SymmetryWallpaperType.p31m = nil
SymmetryWallpaperType.p4 = nil
SymmetryWallpaperType.p4m = nil
SymmetryWallpaperType.p4g = nil
SymmetryWallpaperType.p2 = nil
SymmetryWallpaperType.pgg = nil
SymmetryWallpaperType.pmg = nil
SymmetryWallpaperType.pmm = nil
SymmetryWallpaperType.cmm = nil



---@class Tool
---@field startPoint Transform The position and orientation of the point where the trigger was pressed
---@field endPoint Transform The position and orientation of the point where the trigger was released
---@field vector Vector3 The vector from startPoint to endPoint
---@field rotation Rotation The rotation from startPoint to endPoint
Tool = {}

---@class json
json = {}
---@param jsonString string The JSON string to parse
---@return table # A table representing the parsed JSON
function json:parse(jsonString) end

---@param table table The table to serialize to JSON
---@return string # The JSON representation of the table
function json:serialize(table) end

---@return jsonNull # a special value which is a representation of a null in JSON
function json:null() end

---@return bool # true if the value specified is a null read from JSON
function json:isNull() end
