function path.fromSvg(svgPathString) end
function path.fromSvgMultiple(svgPathString) end
function path.transform(path, tr) end
function path.translate(path, translation) end
function path.rotate(path, rotation) end
function path.scale(path, scale) end
function draw.path(path) end
function draw.paths(paths) end
function draw.polygon(sides, radius, angle) end
function draw.text(text) end
function draw.svg(svgPathString) end
function draw.camerapath(index) end
function strokes.delete(index) end
function strokes.select(index) end
function strokes.selectMultiple(from, to) end
function strokes.join(from, to) end
function strokes.joinPrevious() end
function strokes.import(filename) end
function headset.pastPosition(back) end
function headset.pastRotation(back) end
function color.addHsv(hsv) end
function color.addRgb(rgb) end
function color.setRgb(rgb) end
function color.setHsv(hsv) end
function color.setHtml(color) end
function color.jitter() end
function user.moveTo(position) end
function user.moveBy(amount) end
function spectator.moveTo(position) end
function spectator.moveBy(amount) end
function spectator.turnY(angle) end
function spectator.turnX(angle) end
function spectator.turnY(angle) end
function spectator.direction(direction) end
function spectator.lookAt(position) end
function spectator.mode(mode) end
function spectator.show(thing) end
function spectator.hide(thing) end
function spectator.toggle() end
function spectator.on() end
function spectator.off() end
function layer.add() end
function layer.clear(layer) end
function layer.delete(layer) end
function layer.squash(squashedLayer, destinationLayer) end
function layer.activate(layer) end
function layer.show(layer) end
function layer.hide(layer) end
function layer.toggle(layer) end
function image.import(location) end
function image.select(index) end
function image.position(index, position) end
function model.select(index) end
function model.position(index, position) end
function drafting.visible() end
function drafting.transparent() end
function drafting.hidden() end
function symmetry.mirror() end
function symmetry.doublemirror() end
function symmetry.twohandeded() end
function symmetry.setPosition(position) end
function symmetry.setTransform(position, rotation) end
function symmetry.summonwidget() end
function camerapath.render() end
function camerapath.togglevisuals() end
function camerapath.togglepreview() end
function camerapath.delete() end
function camerapath.record() end
function selection.duplicate() end
function selection.group() end
function selection.invert() end
function selection.flip() end
function selection.recolor() end
function selection.rebrush() end
function selection.resize() end
function selection.trim(count) end
function sketch.open(filename) end
function sketch.save(overwrite) end
function sketch.export() end
function sketch.new() end
function app.undo() end
function app.redo() end
function app.addListener(url) end
function app.resetPanels() end
function app.showScriptsFolder() end
function app.showExportFolder() end
function app.showSketchesFolder(index) end
function app.StraightEdge(active) end
function app.AutoOrient(active) end
function app.ViewOnly(active) end
function app.AutoSimplify(active) end
function app.Disco(active) end
function app.Profiling(active, deep) end
function app.PostProcessing(active) end
function app.Watermark(active) end
function app.setEnvironment(name) end
function guides.add(type) end
function guides.disable() end
function turtle.move.to(position) end
function turtle.move.by(offset) end
function turtle.move(distance) end
function turtle.draw(distance) end
function turtle.turn.y(angle) end
function turtle.turn.x(angle) end
function turtle.turn.z(angle) end
function turtle.look.at(direction) end
function turtle.look.forwards() end
function turtle.look.up() end
function turtle.look.down() end
function turtle.look.left() end
function turtle.look.right() end
function turtle.look.backwards() end
function turtle.home.reset() end
function turtle.home.set() end
function turtle.transform.push() end
function turtle.transform.pop() end
tool.startPosition = nil
tool.endPosition = nil
tool.vector = nil
Mathf.Deg2Rad = nil
Mathf.Epsilon = nil
Mathf.Infinity = nil
Mathf.NegativeInfinity = nil
Mathf.PI = nil
Mathf.Rad2Deg = nil
function Mathf.get_Deg2Rad() end
function Mathf.get_Epsilon() end
function Mathf.get_Infinity() end
function Mathf.get_NegativeInfinity() end
function Mathf.get_PI() end
function Mathf.get_Rad2Deg() end
function Mathf.Abs(f) end
function Mathf.Acos(f) end
function Mathf.Approximately(a, b) end
function Mathf.Asin(f) end
function Mathf.Atan(f) end
function Mathf.Atan2(y, x) end
function Mathf.Ceil(f) end
function Mathf.Clamp(value, min, max) end
function Mathf.Clamp01(value) end
function Mathf.ClosestPowerOfTwo(value) end
function Mathf.Cos(f) end
function Mathf.DeltaAngle(current, target) end
function Mathf.Exp(power) end
function Mathf.Floor(f) end
function Mathf.InverseLerp(a, b, value) end
function Mathf.IsPowerOfTwo(value) end
function Mathf.Lerp(a, b, t) end
function Mathf.LerpAngle(a, b, t) end
function Mathf.LerpUnclamped(a, b, t) end
function Mathf.Log(f, p) end
function Mathf.Log10(f) end
function Mathf.Max(a, b) end
function Mathf.Max(values) end
function Mathf.Min(a, b) end
function Mathf.Min(values) end
function Mathf.MoveTowards(current, target, maxDelta) end
function Mathf.NextPowerOfTwo(value) end
function Mathf.PerlinNoise(x, y) end
function Mathf.PingPong(t, length) end
function Mathf.Pow(f, p) end
function Mathf.Repeat(t, length) end
function Mathf.Round(f) end
function Mathf.Sign(f) end
function Mathf.Sin(f) end
function Mathf.Sqrt(f) end
function Mathf.SmoothStep(from, to, t) end
function Mathf.Tan(f) end
function Mathf.Equals(obj) end
function Mathf.GetHashCode() end
function Mathf.GetType() end
function Mathf.ToString() end
Vector3.back = nil
Vector3.down = nil
Vector3.forward = nil
Vector3.left = nil
Vector3.negativeInfinity = nil
Vector3.one = nil
Vector3.positiveInfinity = nil
Vector3.right = nil
Vector3.up = nil
Vector3.zero = nil
function Vector3.Angle(a, b) end
function Vector3.ClampMagnitude(v, maxLength) end
function Vector3.Cross(a, b) end
function Vector3.Distance(a, b) end
function Vector3.Dot(a, b) end
function Vector3.Lerp(a, b, t) end
function Vector3.LerpUnclamped(a, b, t) end
function Vector3.Max(a, b) end
function Vector3.Min(a, b) end
function Vector3.MoveTowards(current, target, maxDistanceDelta) end
function Vector3.Normalize(a) end
function Vector3.Project(a, b) end
function Vector3.ProjectOnPlane(vector, planeNormal) end
function Vector3.Reflect(a, b) end
function Vector3.RotateTowards(current, target, maxRadiansDelta, maxMagnitudeDelta) end
function Vector3.Scale(a, b) end
function Vector3.SignedAngle(from, to, axis) end
function Vector3.Slerp(a, b, t) end
function Vector3.SlerpUnclamped(a, b, t) end
function Vector3.get_back() end
function Vector3.get_down() end
function Vector3.get_forward() end
function Vector3.get_left() end
function Vector3.get_negativeInfinity() end
function Vector3.get_one() end
function Vector3.get_positiveInfinity() end
function Vector3.get_right() end
function Vector3.get_up() end
function Vector3.get_zero() end
function Vector3.Equals(obj) end
function Vector3.GetHashCode() end
function Vector3.GetType() end
function Vector3.ToString() end
brush.TimeSincePressed = nil
brush.TimeSinceReleased = nil
brush.TriggerIsPressed = nil
brush.TriggerIsPressedThisFrame = nil
brush.DistanceMoved = nil
brush.DistanceDrawn = nil
brush.Position = nil
brush.Rotation = nil
brush.Direction = nil
brush.Size = nil
brush.Size01 = nil
brush.Pressure = nil
brush.Name = nil
brush.Speed = nil
brush.Color = nil
brush.LastColorPicked = nil
brush.ColorHsv = nil
brush.LastColorPickedHsv = nil
function brush.get_TimeSincePressed() end
function brush.get_TimeSinceReleased() end
function brush.get_TriggerIsPressed() end
function brush.get_TriggerIsPressedThisFrame() end
function brush.get_DistanceMoved() end
function brush.get_DistanceDrawn() end
function brush.get_Position() end
function brush.get_Rotation() end
function brush.get_Direction() end
function brush.get_Size() end
function brush.get_Size01() end
function brush.get_Pressure() end
function brush.get_Name() end
function brush.get_Speed() end
function brush.get_Color() end
function brush.get_LastColorPicked() end
function brush.PastPosition(back) end
function brush.PastRotation(back) end
function brush.Type(brushType) end
function brush.SizeSet(size) end
function brush.SizeAdd(amount) end
function brush.ForcePaintingOn(active) end
function brush.ForcePaintingOff(active) end
function brush.get_ColorHsv() end
function brush.get_LastColorPickedHsv() end
function brush.Equals(obj) end
function brush.GetHashCode() end
function brush.GetType() end
function brush.ToString() end
wand.Position = nil
wand.Rotation = nil
wand.Direction = nil
wand.Pressure = nil
wand.Speed = nil
function wand.get_Position() end
function wand.get_Rotation() end
function wand.get_Direction() end
function wand.get_Pressure() end
function wand.get_Speed() end
function wand.PastPosition(back) end
function wand.PastRotation(back) end
function wand.Equals(obj) end
function wand.GetHashCode() end
function wand.GetType() end
function wand.ToString() end
app.Time = nil
app.Frames = nil
app.LastSelectedStroke = nil
app.LastStroke = nil
function app.get_Time() end
function app.get_Frames() end
function app.get_LastSelectedStroke() end
function app.get_LastStroke() end
function app.Equals(obj) end
function app.GetHashCode() end
function app.GetType() end
function app.ToString() end
canvas.Scale = nil
canvas.StrokeCount = nil
function canvas.get_Scale() end
function canvas.get_StrokeCount() end
function canvas.Equals(obj) end
function canvas.GetHashCode() end
function canvas.GetType() end
function canvas.ToString() end
