using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public static class UserApiWrapper
    {
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
