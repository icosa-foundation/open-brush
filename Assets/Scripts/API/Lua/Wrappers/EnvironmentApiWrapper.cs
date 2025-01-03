using MoonSharp.Interpreter;

namespace TiltBrush
{
    [LuaDocsDescription("The background, skybox and props")]
    [MoonSharpUserData]
    public class EnvironmentApiWrapper
    {
        private ColorApiWrapper _GradientColorA;
        private ColorApiWrapper _GradientColorB;
        private RotationApiWrapper _GradientOrientation;
        private ColorApiWrapper _FogColor;
        private float _FogDensity;
        private ColorApiWrapper _AmbientColor;
        private ColorApiWrapper _MainLightColor;
        private RotationApiWrapper _MainLightDirection;
        private ColorApiWrapper _SecondaryLightColor;
        private RotationApiWrapper _SecondaryLightDirection;

        private EnvironmentApiWrapper _Current;
        private bool _IsCurrent => ReferenceEquals(_Current, this);

        [MoonSharpHidden]
        public Environment _Environment;

        public EnvironmentApiWrapper(Environment environment)
        {
            _Environment = environment;
        }

        public EnvironmentApiWrapper(bool isCurrent)
        {
            _Environment = SceneSettings.m_Instance.CurrentEnvironment;
            if (isCurrent)
            {
                _GradientColorA = new(SceneSettings.m_Instance.SkyColorA);
                _GradientColorB = new(SceneSettings.m_Instance.SkyColorB);
                _GradientOrientation = new(SceneSettings.m_Instance.GradientOrientation);
                _FogColor = new(SceneSettings.m_Instance.FogColor);
                _FogDensity = SceneSettings.m_Instance.FogDensity;
                _AmbientColor = new(LightsControlScript.m_Instance.CustomLights.Ambient);
                _MainLightColor = new(LightsControlScript.m_Instance.CustomLights.Shadow.Color);
                _MainLightDirection = new(LightsControlScript.m_Instance.CustomLights.Shadow.Orientation);
                _SecondaryLightColor = new(LightsControlScript.m_Instance.CustomLights.NoShadow.Color);
                _SecondaryLightDirection = new(LightsControlScript.m_Instance.CustomLights.NoShadow.Orientation);
                _Current = this;
            }
        }

        [LuaDocsDescription("The current environment settings")]
        public static EnvironmentApiWrapper current
        {
            get => new(isCurrent: true);
            set
            {
                SceneSettings.m_Instance.SkyColorA = value._GradientColorA._Color;
                SceneSettings.m_Instance.SkyColorB = value._GradientColorB._Color;
                SceneSettings.m_Instance.GradientOrientation = value._GradientOrientation._Quaternion;
                SceneSettings.m_Instance.FogColor = value._FogColor._Color;
                SceneSettings.m_Instance.FogDensity = value._FogDensity;
                LightsControlScript.m_Instance.CustomLights.Ambient = value._AmbientColor._Color;
                LightsControlScript.m_Instance.CustomLights.Shadow.Color = value._MainLightColor._Color;
                LightsControlScript.m_Instance.CustomLights.Shadow.Orientation = value._MainLightDirection._Quaternion;
                LightsControlScript.m_Instance.CustomLights.NoShadow.Color = value._SecondaryLightColor._Color;
                LightsControlScript.m_Instance.CustomLights.NoShadow.Orientation = value._SecondaryLightDirection._Quaternion;
            }
        }

        public override string ToString()
        {
            return $"Environment({_Environment.Description})";
        }

        [LuaDocsDescription("The sky color at the top")]
        public ColorApiWrapper gradientColorA
        {
            get => _GradientColorA;
            set
            {
                _GradientColorA = value;
                if (_IsCurrent) SceneSettings.m_Instance.SkyColorA = value._Color;
            }
        }

        [LuaDocsDescription("The skybox color at the horizon")]
        public ColorApiWrapper gradientColorB
        {
            get => _GradientColorB;
            set
            {
                _GradientColorB = value;
                if (_IsCurrent) SceneSettings.m_Instance.SkyColorB = value._Color;
            }
        }

        [LuaDocsDescription("The sky gradient orientation")]
        public RotationApiWrapper gradientOrientation
        {
            get => _GradientOrientation;
            set
            {
                _GradientOrientation = value;
                if (_IsCurrent) SceneSettings.m_Instance.GradientOrientation = value._Quaternion;
            }
        }

        [LuaDocsDescription("The fog color")]
        public ColorApiWrapper fogColor
        {
            get => _FogColor;
            set
            {
                _FogColor = value;
                if (_IsCurrent) SceneSettings.m_Instance.FogColor = value._Color;
            }
        }

        [LuaDocsDescription("The fog density")]
        public float fogDensity
        {
            get => _FogDensity;
            set
            {
                _FogDensity = value;
                if (_IsCurrent) SceneSettings.m_Instance.FogDensity = value;
            }
        }

        [LuaDocsDescription("The ambient light color")]
        public ColorApiWrapper ambientColor
        {
            get => _AmbientColor;
            set
            {
                _AmbientColor = value;
                if (_IsCurrent) LightsControlScript.m_Instance.CustomLights.Ambient = value._Color;
            }
        }

        [LuaDocsDescription("The main light color")]
        public ColorApiWrapper mainLightColor
        {
            get => _MainLightColor;
            set
            {
                _MainLightColor = value;
                if (_IsCurrent) LightsControlScript.m_Instance.CustomLights.Shadow.Color = value._Color;
            }
        }

        [LuaDocsDescription("The main light direction")]
        public RotationApiWrapper mainLightDirection
        {
            get => _MainLightDirection;
            set
            {
                _MainLightDirection = value;
                if (_IsCurrent) LightsControlScript.m_Instance.CustomLights.Shadow.Orientation = value._Quaternion;
            }
        }

        [LuaDocsDescription("The secondary light color")]
        public ColorApiWrapper secondaryLightColor
        {
            get => _SecondaryLightColor;
            set
            {
                _SecondaryLightColor = value;
                if (_IsCurrent) LightsControlScript.m_Instance.CustomLights.NoShadow.Color = value._Color;
            }
        }

        [LuaDocsDescription("The secondary light direction")]
        public RotationApiWrapper secondaryLightDirection
        {
            get => _SecondaryLightDirection;
            set
            {
                _SecondaryLightDirection = value;
                if (_IsCurrent) LightsControlScript.m_Instance.CustomLights.NoShadow.Orientation = value._Quaternion;
            }
        }
    }
}
