using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("Represents the user and their position in the sketch")]
    [MoonSharpUserData]
    public static class UserApiWrapper
    {
        [LuaDocsDescription(@"The 3D position of the user's viewpoint")]
        public static Vector3 position
        {
            get => App.Scene.Pose.translation;
            set
            {
                TrTransform pose = App.Scene.Pose;
                pose.translation = value;
                float BoundsRadius = SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
                pose = SketchControlsScript.MakeValidScenePose(pose, BoundsRadius);
                App.Scene.Pose = pose;
            }
        }

        [LuaDocsDescription("The 3D orientation of the User (usually only a rotation around the Y axis unless you've set it manually or disabled axis locking")]
        public static Quaternion rotation
        {
            get => App.Scene.Pose.rotation;
            set
            {
                TrTransform pose = App.Scene.Pose;
                pose.rotation = value;
                float BoundsRadius = SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
                pose = SketchControlsScript.MakeValidScenePose(pose, BoundsRadius);
                App.Scene.Pose = pose;
            }
        }
    }
}
