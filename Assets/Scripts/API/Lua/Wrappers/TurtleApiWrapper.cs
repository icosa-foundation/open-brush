using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("The turtle can be used to draw strokes in a classic LOGO style for moving and turning in space")]
    [MoonSharpUserData]
    public static class TurtleApiWrapper
    {
        public static TrTransform transform => TrTransform.TR(position, rotation);

        [LuaDocsDescription("The current 3D position of the turtle")]
        public static Vector3 position => ApiManager.Instance.BrushPosition;

        [LuaDocsDescription("The current 3D orientation of the Turtle")]
        public static Quaternion rotation => ApiManager.Instance.BrushRotation;
        public static void MoveTo(Vector3 position) => ApiMethods.BrushMoveTo(position);
        public static void MoveBy(Vector3 amount) => ApiMethods.BrushMoveBy(amount);
        public static void Move(float amount) => ApiMethods.BrushMove(amount);
        public static void Draw(float amount) => ApiMethods.BrushDraw(amount);
        public static void DrawPolygon(int sides, float radius = 1, float angle = 0) => ApiMethods.DrawPolygon(sides, radius, angle);
        public static void DrawText(string text) => ApiMethods.Text(text);
        public static void DrawSvgPath(string svgPath) => ApiMethods.DrawSvgPath(svgPath);
        public static void DrawSvg(string svg) => ApiMethods.DrawSvg(svg);
        public static void TurnY(float angle) => ApiMethods.BrushYaw(angle);
        public static void TurnX(float angle) => ApiMethods.BrushPitch(angle);
        public static void TurnZ(float angle) => ApiMethods.BrushRoll(angle);
        public static void LookAt(Vector3 amount) => ApiMethods.BrushLookAt(amount);
        public static void LookForwards() => ApiMethods.BrushLookForwards();
        public static void LookUp() => ApiMethods.BrushLookUp();
        public static void LookDown() => ApiMethods.BrushLookDown();
        public static void LookLeft() => ApiMethods.BrushLookLeft();
        public static void LookRight() => ApiMethods.BrushLookRight();
        public static void LookBackwards() => ApiMethods.BrushLookBackwards();
        public static void HomeReset() => ApiMethods.BrushHome();
        public static void HomeSet() => ApiMethods.BrushSetHome();
        public static void TransformPush() => ApiMethods.BrushTransformPush();
        public static void TransformPop() => ApiMethods.BrushTransformPop();
    }
}
