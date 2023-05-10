Tool.startPosition = nil
Tool.endPosition = nil
Tool.vector = nil
Tool.rotation = nil
App.time = nil
App.frames = nil
App.currentScale = nil
App.environment = nil
App.clipboardText = nil
App.clipboardImage = nil
function App.Physics(active) end
function App.Undo() end
function App.Redo() end
function App.AddListener(a) end
function App.ResetPanels() end
function App.ShowScriptsFolder() end
function App.ShowExportFolder() end
function App.ShowSketchesFolder(a) end
function App.StraightEdge(a) end
function App.AutoOrient(a) end
function App.ViewOnly(a) end
function App.AutoSimplify(a) end
function App.Disco(a) end
function App.Profiling(a) end
function App.PostProcessing(a) end
function App.DraftingVisible() end
function App.DraftingTransparent() end
function App.DraftingHidden() end
function App.Watermark(a) end
function App.ReadFile(path) end
function App.Error(message) end
function App.SetFont(fontData) end
function App.TakeSnapshot(tr, filename, width, height, superSampling) end
function App.Take360Snapshot(tr, filename, width) end
Brush.timeSincePressed = nil
Brush.timeSinceReleased = nil
Brush.triggerIsPressed = nil
Brush.triggerIsPressedThisFrame = nil
Brush.distanceMoved = nil
Brush.distanceDrawn = nil
Brush.position = nil
Brush.rotation = nil
Brush.direction = nil
Brush.size = nil
Brush.pressure = nil
Brush.type = nil
Brush.speed = nil
Brush.colorRgb = nil
Brush.colorHsv = nil
Brush.colorHtml = nil
Brush.lastColorPicked = nil
Brush.LastColorPickedHsv = nil
function Brush.JitterColor() end
function Brush.ResizeBuffer(size) end
function Brush.SetBufferSize(size) end
function Brush.GetPastPosition(back) end
function Brush.GetPastRotation(back) end
function Brush.ForcePaintingOn(active) end
function Brush.ForcePaintingOff(active) end
function Brush.ForceNewStroke() end
CameraPath.index = nil
CameraPath.active = nil
CameraPath.transform = nil
CameraPath.position = nil
CameraPath.rotation = nil
CameraPath.scale = nil
function CameraPath.RenderActivePath() end
function CameraPath.ShowAll() end
function CameraPath.HideAll() end
function CameraPath.PreviewActivePath(active) end
function CameraPath.Delete() end
function CameraPath.New() end
function CameraPath.FromPath(path, looped) end
function CameraPath.AsPath(step) end
function CameraPath.Duplicate() end
function CameraPath.InsertPosition(position, rotation, smoothing) end
function CameraPath.InsertPosition(t, rotation, smoothing) end
function CameraPath.InsertRotation(pos, rot) end
function CameraPath.InsertRotation(t, rot) end
function CameraPath.InsertFov(pos, fov) end
function CameraPath.InsertFov(t, fov) end
function CameraPath.InsertSpeed(pos, speed) end
function CameraPath.InsertSpeed(t, speed) end
function CameraPath.Extend(position, rotation, smoothing, atStart) end
function CameraPath.Loop() end
function CameraPath.RecordActivePath() end
function CameraPath.Sample(time, loop, pingpong) end
function CameraPath.Simplify(tolerance, smoothing) end
Color.Item = nil
Color.r = nil
Color.g = nil
Color.b = nil
Color.a = nil
Color.grayscale = nil
Color.gamma = nil
Color.linear = nil
Color.maxColorComponent = nil
Color.black = nil
Color.blue = nil
Color.clear = nil
Color.cyan = nil
Color.gray = nil
Color.green = nil
Color.grey = nil
Color.magenta = nil
Color.red = nil
Color.white = nil
Color.yellow = nil
function Color.New(r, g, b) end
function Color.New(html) end
function Color.Greyscale(col) end
function Color.MaxColorComponent(col) end
function Color.ToHtmlString(col) end
function Color.ParseHtmlString(html) end
function Color.Lerp(a, b, t) end
function Color.LerpUnclamped(a, b, t) end
function Color.HsvToRgb(h, s, v) end
function Color.RgbToHsv(rgb) end
function Color.Add(b) end
function Color.Add(r, g, b) end
function Color.Subtract(b) end
function Color.Subtract(r, g, b) end
function Color.Multiply(b) end
function Color.Multiply(r, g, b) end
function Color.Divide(b) end
function Color.NotEquals(b) end
function Color.NotEquals(r, g, b) end
function Color.Add(a, b) end
function Color.Subtract(a, b) end
function Color.Multiply(a, b) end
function Color.Divide(a, b) end
function Color.NotEquals(a, b) end
function Easing.linear(t) end
function Easing.inQuad(t) end
function Easing.outQuad(t) end
function Easing.inOutQuad(t) end
function Easing.inCubic(t) end
function Easing.outCubic(t) end
function Easing.inOutCubic(t) end
function Easing.inQuart(t) end
function Easing.outQuart(t) end
function Easing.inOutQuart(t) end
function Easing.inQuint(t) end
function Easing.outQuint(t) end
function Easing.inOutQuint(t) end
function Easing.inSine(t) end
function Easing.outSine(t) end
function Easing.inOutSine(t) end
function Easing.inExpo(t) end
function Easing.outExpo(t) end
function Easing.inOutExpo(t) end
function Easing.inCirc(t) end
function Easing.outCirc(t) end
function Easing.inOutCirc(t) end
function Easing.inElastic(t) end
function Easing.outElastic(t) end
function Easing.inOutElastic(t) end
function Easing.inBack(t) end
function Easing.outBack(t) end
function Easing.inOutBack(t) end
function Easing.inBounce(t) end
function Easing.outBounce(t) end
function Easing.inOutBounce(t) end
Guide.index = nil
Guide.transform = nil
Guide.position = nil
Guide.rotation = nil
Guide.scale = nil
function Guide.NewCube(transform) end
function Guide.NewSphere(transform) end
function Guide.NewCapsule(transform) end
function Guide.NewCone(transform) end
function Guide.NewEllipsoid(transform) end
function Guide.NewCustom(transform, model) end
function Guide.Select() end
function Guide.Scale(scale) end
function Headset.ResizeBuffer(size) end
function Headset.SetBufferSize(size) end
function Headset.PastPosition(count) end
function Headset.PastRotation(count) end
Image.index = nil
Image.transform = nil
Image.position = nil
Image.rotation = nil
Image.scale = nil
function Image.Import(location) end
function Image.Select() end
function Image.FormEncode() end
function Image.SaveBase64(base64, filename) end
Layer.index = nil
Layer.active = nil
Layer.transform = nil
Layer.position = nil
Layer.rotation = nil
Layer.scale = nil
function Layer.New() end
function Layer.CenterPivot() end
function Layer.ShowPivot() end
function Layer.HidePivot() end
function Layer.Clear() end
function Layer.Delete() end
function Layer.Squash(other) end
function Layer.SquashTo(destinationLayer) end
function Layer.Show() end
function Layer.Hide() end
function Layer.Toggle() end
Math.deg2Rad = nil
Math.epsilon = nil
Math.positiveInfinity = nil
Math.negativeInfinity = nil
Math.pi = nil
Math.rad2Deg = nil
function Math.Abs(f) end
function Math.Acos(f) end
function Math.Approximately(a, b) end
function Math.Asin(f) end
function Math.Atan(f) end
function Math.Atan2(y, x) end
function Math.Ceil(f) end
function Math.Clamp(value, min, max) end
function Math.Clamp01(value) end
function Math.ClosestPowerOfTwo(value) end
function Math.Cos(f) end
function Math.DeltaAngle(current, target) end
function Math.Exp(power) end
function Math.Floor(f) end
function Math.InverseLerp(a, b, value) end
function Math.IsPowerOfTwo(value) end
function Math.Lerp(a, b, t) end
function Math.LerpAngle(a, b, t) end
function Math.LerpUnclamped(a, b, t) end
function Math.Log(f, p) end
function Math.Log10(f) end
function Math.Max(a, b) end
function Math.Max(values) end
function Math.Min(a, b) end
function Math.Min(values) end
function Math.MoveTowards(current, target, maxDelta) end
function Math.NextPowerOfTwo(value) end
function Math.PerlinNoise(x, y) end
function Math.PingPong(t, length) end
function Math.Pow(f, p) end
function Math.Repeater(t, length) end
function Math.Round(f) end
function Math.Sign(f) end
function Math.Sin(f) end
function Math.Sqrt(f) end
function Math.SmoothStep(from, to, t) end
function Math.Tan(f) end
function Math.Sinh(f) end
function Math.Cosh(f) end
function Math.Tanh(f) end
Model.index = nil
Model.transform = nil
Model.position = nil
Model.rotation = nil
Model.scale = nil
function Model.Import(location) end
function Model.Select() end
MultiPath.Space = nil
MultiPath.count = nil
MultiPath.pointCount = nil
function MultiPath.AsSingleTrList() end
function MultiPath.AsMultiTrList() end
function MultiPath.New() end
function MultiPath.New(pathList) end
function MultiPath.Draw() end
function MultiPath.FromText(text) end
function MultiPath.Insert(path) end
function MultiPath.InsertPoint(transform) end
function MultiPath.Transform(transform) end
function MultiPath.Translate(amount) end
function MultiPath.Rotate(amount) end
function MultiPath.Scale(scale) end
function MultiPath.Center() end
function MultiPath.Normalize(scale) end
function MultiPath.Resample(spacing) end
function MultiPath.Join() end
function MultiPath.Longest() end
Path.Space = nil
Path.count = nil
function Path.AsSingleTrList() end
function Path.AsMultiTrList() end
function Path.New() end
function Path.New(transformList) end
function Path.New(positionList) end
function Path.Draw() end
function Path.Insert(transform) end
function Path.Transform(transform) end
function Path.Translate(amount) end
function Path.Rotate(amount) end
function Path.Scale(scale) end
function Path.Center() end
function Path.StartingFrom(index) end
function Path.FindClosest(point) end
function Path.FindMinimumX() end
function Path.FindMinimumY() end
function Path.FindMinimumZ() end
function Path.FindMaximumX() end
function Path.FindMaximumY() end
function Path.FindMaximumZ() end
function Path._FindMinimum(axis) end
function Path._FindMaximum(axis) end
function Path.Normalize(scale) end
function Path._CalculateCenterAndScale(path) end
function Path.Subdivide(trs, parts) end
function Path.Resample(trs, spacing) end
function Path.Resample(spacing) end
function Path.Subdivide(parts) end
Path2d.Space = nil
Path2d.count = nil
function Path2d.AsSingleTrList() end
function Path2d.AsMultiTrList() end
function Path2d.New() end
function Path2d.New(transformList) end
function Path2d.New(positionList) end
function Path2d.Insert(transform) end
function Path2d.OnX() end
function Path2d.OnY() end
function Path2d.OnZ() end
function Path2d.Transform(transform) end
function Path2d.Translate(amount) end
function Path2d.Rotate(amount) end
function Path2d.Scale(scale) end
function Path2d.Center() end
function Path2d.StartingFrom(index) end
function Path2d.FindClosest(point) end
function Path2d.FindMinimumX() end
function Path2d.FindMinimumY() end
function Path2d.FindMinimumZ() end
function Path2d.FindMaximumX() end
function Path2d.FindMaximumY() end
function Path2d.FindMaximumZ() end
function Path2d._FindMinimum(axis) end
function Path2d._FindMaximum(axis) end
function Path2d.Normalize(scale) end
function Path2d._CalculateCenterAndScale(path) end
function Path2d.Polygon(sides) end
function Path2d.Resample(spacing) end
Random.insideUnitCircle = nil
Random.insideUnitSphere = nil
Random.onUnitSphere = nil
Random.rotation = nil
Random.rotationUniform = nil
Random.value = nil
Random.colorHSV = nil
function Random.InitState(seed) end
function Random.Range(min, max) end
function Random.Range(min, max) end
Rotation.Item = nil
Rotation.x = nil
Rotation.y = nil
Rotation.z = nil
Rotation.zero = nil
Rotation.left = nil
Rotation.right = nil
Rotation.up = nil
Rotation.down = nil
Rotation.anticlockwise = nil
Rotation.clockwise = nil
Rotation.normalized = nil
Rotation.kEpsilon = nil
function Rotation.New(x, y, z) end
function Rotation.SetFromToRotation(fromDirection, toDirection) end
function Rotation.SetLookRotation(view) end
function Rotation.SetLookRotation(view, up) end
function Rotation.ToAngleAxis() end
function Rotation.Angle(a, b) end
function Rotation.AngleAxis(angle, axis) end
function Rotation.Dot(a, b) end
function Rotation.FromToRotation(from, to) end
function Rotation.Inverse(a) end
function Rotation.Lerp(a, b, t) end
function Rotation.LerpUnclamped(a, b, t) end
function Rotation.LookRotation(forward) end
function Rotation.LookRotation(forward, up) end
function Rotation.Normalize(a) end
function Rotation.RotateTowards(from, to, maxDegreesDelta) end
function Rotation.Slerp(a, b, t) end
function Rotation.SlerpUnclamped(a, b, t) end
function Rotation.Multiply(b) end
function Rotation.Multiply(x, y, z) end
function Rotation.Multiply(a, b) end
function Selection.Duplicate() end
function Selection.Group() end
function Selection.Invert() end
function Selection.Flip() end
function Selection.Recolor() end
function Selection.Rebrush() end
function Selection.Resize() end
function Selection.Trim(count) end
function Selection.SelectAll() end
Sketch.cameraPaths = nil
Sketch.strokes = nil
Sketch.layers = nil
Sketch.images = nil
Sketch.videos = nil
Sketch.models = nil
Sketch.guides = nil
Sketch.lights = nil
Sketch.environments = nil
function Sketch.Open(name) end
function Sketch.Save(overwrite) end
function Sketch.SaveAs(name) end
function Sketch.Export() end
function Sketch.NewSketch() end
Spectator.position = nil
Spectator.rotation = nil
function Spectator.Turn(angle) end
function Spectator.TurnX(angle) end
function Spectator.TurnZ(angle) end
function Spectator.Direction(direction) end
function Spectator.LookAt(position) end
function Spectator.Mode(mode) end
function Spectator.Show(type) end
function Spectator.Hide(type) end
function Spectator.Toggle() end
function Spectator.On() end
function Spectator.Off() end
Stroke.path = nil
Stroke.brushType = nil
Stroke.brushSize = nil
Stroke.brushColor = nil
Stroke.layer = nil
Stroke.Item = nil
Stroke.count = nil
function Stroke.ChangeMaterial(brushName) end
function Stroke.Delete() end
function Stroke.Select() end
function Stroke.SelectMultiple(from, to) end
function Stroke.Join(from, to) end
function Stroke.JoinPrevious() end
function Stroke.Import(name) end
function Svg.ParsePathString(svgPath) end
function Svg.ParseDocument(svg, offsetPerPath, includeColors) end
function Svg.DrawPathString(svg, tr) end
function Svg.DrawDocument(svg, tr) end
Symmetry.transform = nil
Symmetry.position = nil
Symmetry.rotation = nil
Symmetry.brushOffset = nil
Symmetry.wandOffset = nil
Symmetry.direction = nil
function Symmetry.Mirror() end
function Symmetry.DoubleMirror() end
function Symmetry.TwoHandeded() end
function Symmetry.SummonWidget() end
function Symmetry.Spin(xSpeed, ySpeed, zSpeed) end
function Symmetry.Ellipse(angle, minorRadius) end
function Symmetry.Square(angle) end
function Symmetry.Superellipse(angle, n, a, b) end
function Symmetry.Rsquare(angle, halfSideLength, cornerRadius) end
function Symmetry.Polygon(angle, numSides, radius) end
function Symmetry.ClearColors(colors) end
function Symmetry.AddColor(color) end
function Symmetry.SetColors(colors) end
function Symmetry.GetColors() end
function Symmetry.AddBrush(brush) end
function Symmetry.ClearBrushes(brushes) end
function Symmetry.SetBrushes(brushes) end
function Symmetry.GetBrushNames() end
function Symmetry.GetBrushGuids() end
function Symmetry.PathToPolar(path) end
function Symmetry.Path2dToPolar(cartesianPoints) end
function Timer.Set(fn, interval, delay, repeats) end
function Timer.Unset(fn) end
Transform.zero = nil
function Transform.New(translation, rotation, scale) end
function Transform.New(translation, scale) end
function Transform.New(scale) end
function Transform.New(x, y, z) end
function Transform.Multiply(b) end
function Transform.Multiply(a, b) end
Turtle.transform = nil
Turtle.position = nil
Turtle.rotation = nil
function Turtle.MoveTo(position) end
function Turtle.MoveBy(amount) end
function Turtle.Move(amount) end
function Turtle.Draw(amount) end
function Turtle.DrawPolygon(sides, radius, angle) end
function Turtle.DrawText(text) end
function Turtle.DrawSvg(svg) end
function Turtle.TurnY(angle) end
function Turtle.TurnX(angle) end
function Turtle.TurnZ(angle) end
function Turtle.LookAt(amount) end
function Turtle.LookForwards() end
function Turtle.LookUp() end
function Turtle.LookDown() end
function Turtle.LookLeft() end
function Turtle.LookRight() end
function Turtle.LookBackwards() end
function Turtle.HomeReset() end
function Turtle.HomeSet() end
function Turtle.TransformPush() end
function Turtle.TransformPop() end
User.position = nil
User.rotation = nil
Vector2.Item = nil
Vector2.x = nil
Vector2.y = nil
Vector2.down = nil
Vector2.left = nil
Vector2.negativeInfinity = nil
Vector2.one = nil
Vector2.positiveInfinity = nil
Vector2.right = nil
Vector2.up = nil
Vector2.zero = nil
function Vector2.New(x, y) end
function Vector2.Angle(a, b) end
function Vector2.ClampMagnitude(v, maxLength) end
function Vector2.Distance(a, b) end
function Vector2.Magnitude(a) end
function Vector2.SqrMagnitude(a) end
function Vector2.Dot(a, b) end
function Vector2.Lerp(a, b, t) end
function Vector2.LerpUnclamped(a, b, t) end
function Vector2.Max(a, b) end
function Vector2.Min(a, b) end
function Vector2.MoveTowards(current, target, maxDistanceDelta) end
function Vector2.Normalized(a) end
function Vector2.Reflect(a, b) end
function Vector2.Scale(a, b) end
function Vector2.SignedAngle(from, to, axis) end
function Vector2.Slerp(a, b, t) end
function Vector2.SlerpUnclamped(a, b, t) end
function Vector2.PointOnCircle(degrees) end
function Vector2.OnX() end
function Vector2.OnY() end
function Vector2.OnZ() end
function Vector2.Add(b) end
function Vector2.Add(x, y) end
function Vector2.Subtract(b) end
function Vector2.Subtract(x, y) end
function Vector2.Multiply(b) end
function Vector2.Multiply(x, y) end
function Vector2.Divide(b) end
function Vector2.Divide(x, y) end
function Vector2.NotEquals(b) end
function Vector2.NotEquals(x, y) end
function Vector2.Add(a, b) end
function Vector2.Subtract(a, b) end
function Vector2.Multiply(a, b) end
function Vector2.Divide(a, b) end
function Vector2.NotEquals(a, b) end
Vector3.Item = nil
Vector3.x = nil
Vector3.y = nil
Vector3.z = nil
Vector3.magnitude = nil
Vector3.normalized = nil
Vector3.sqrMagnitude = nil
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
function Vector3.New(x, y, z) end
function Vector3.Angle(a, b) end
function Vector3.ClampMagnitude(v, maxLength) end
function Vector3.Cross(a, b) end
function Vector3.Distance(a, b) end
function Vector3.Magnitude(a) end
function Vector3.SqrMagnitude(a) end
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
function Vector3.Add(b) end
function Vector3.Add(x, y, z) end
function Vector3.Subtract(b) end
function Vector3.Subtract(x, y, z) end
function Vector3.Multiply(b) end
function Vector3.Scale(b) end
function Vector3.Scale(x, y, z) end
function Vector3.Divide(b) end
function Vector3.NotEquals(b) end
function Vector3.NotEquals(x, y, z) end
function Vector3.Add(a, b) end
function Vector3.Subtract(a, b) end
function Vector3.Multiply(a, b) end
function Vector3.Divide(a, b) end
function Vector3.NotEquals(a, b) end
Video.index = nil
Video.transform = nil
Video.position = nil
Video.rotation = nil
Video.scale = nil
function Video.Import(location) end
Visualizer.sampleRate = nil
Visualizer.duration = nil
function Visualizer.EnableScripting(name) end
function Visualizer.DisableScripting() end
function Visualizer.SetWaveform(data) end
function Visualizer.SetFft(data1, data2, data3, data4) end
function Visualizer.SetBeats(x, y, z, w) end
function Visualizer.SetBeatAccumulators(x, y, z, w) end
function Visualizer.SetBandPeak(peak) end
Wand.position = nil
Wand.rotation = nil
Wand.direction = nil
Wand.pressure = nil
Wand.speed = nil
function Wand.ResizeBuffer(size) end
function Wand.SetBufferSize(size) end
function Wand.PastPosition(back) end
function Wand.PastRotation(back) end
function Waveform.Sine(time, frequency) end
function Waveform.Cosine(time, frequency) end
function Waveform.Triangle(time, frequency) end
function Waveform.Sawtooth(time, frequency) end
function Waveform.Square(time, frequency) end
function Waveform.Pulse(time, frequency, pulseWidth) end
function Waveform.Exponent(time, frequency) end
function Waveform.Power(time, frequency, power) end
function Waveform.Parabolic(time, frequency) end
function Waveform.ExponentialSawtooth(time, frequency, exponent) end
function Waveform.PerlinNoise(time, frequency) end
function Waveform.WhiteNoise() end
function Waveform.BrownNoise(previous) end
function Waveform.BlueNoise(previous) end
function Waveform.Sine(time, frequency, duration, sampleRate, amplitude) end
function Waveform.Cosine(time, frequency, duration, sampleRate, amplitude) end
function Waveform.Triangle(time, frequency, duration, sampleRate, amplitude) end
function Waveform.Sawtooth(time, frequency, duration, sampleRate, amplitude) end
function Waveform.Square(time, frequency, duration, sampleRate, amplitude) end
function Waveform.Exponent(time, frequency, duration, sampleRate, amplitude) end
function Waveform.Parabolic(time, frequency, duration, sampleRate, amplitude) end
function Waveform.Pulse(time, frequency, pulseWidth, duration, sampleRate, amplitude) end
function Waveform.Power(time, frequency, power, duration, sampleRate, amplitude) end
function Waveform.ExponentialSawtoothWave(time, frequency, exponent, duration, sampleRate, amplitude) end
function Waveform.PerlinNoise(time, frequency, duration, sampleRate, amplitude) end
function Waveform.WhiteNoise(duration, sampleRate, amplitude) end
function Waveform.BrownNoise(previous, duration, sampleRate, amplitude) end
function Waveform.BlueNoise(previous, duration, sampleRate, amplitude) end
function WebRequest.Get(url, onSuccess, onError, headers, context) end
function WebRequest.Post(url, postData, onSuccess, onError, headers, context) end
