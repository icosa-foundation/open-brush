using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Utilities;
using UnityEditor;

#if UNITY_EDITOR
[InitializeOnLoad]
#endif

[DisplayStringFormat("{axis}")]
public class PicoFakeTouchStickComposite : InputBindingComposite<float>
{
    [InputControl(layout = "Vector2")]
    public int axis;

    public override float ReadValue(ref InputBindingCompositeContext context)
    {
        var axisInput = context.ReadValue<Vector2, Vector2MagnitudeComparer>(axis);
        return axisInput.magnitude > 0.01f ? 1 : 0;
    }

    public override float EvaluateMagnitude(ref InputBindingCompositeContext context)
    {
        return ReadValue(ref context);
    }

    static PicoFakeTouchStickComposite()
    {
        InputSystem.RegisterBindingComposite<PicoFakeTouchStickComposite>("PicoFakeTouchStick");
    }

    [RuntimeInitializeOnLoadMethod]
    static void Init() { } // Trigger static constructor.
}
