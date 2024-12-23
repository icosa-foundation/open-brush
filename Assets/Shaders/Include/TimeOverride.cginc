#ifdef SHADER_SCRIPTING_ON

  uniform float4 _TimeOverrideValue = float4(0,0,0,0);
  uniform half _TimeBlend = 0.0;
  uniform half _TimeSpeed = 1.0;

  float4 GetTime() {
    return lerp(_Time * _TimeSpeed, _TimeOverrideValue, _TimeBlend);
  }

#else

  float4 GetTime() {
    return _Time;
  }
#endif

