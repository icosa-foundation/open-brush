using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Utilities;
using UnityEditor;

#if UNITY_EDITOR
[InitializeOnLoad]
#endif

[DisplayStringFormat("{axis}")]
public class UnityXRFakeThumbTouchComposite : InputBindingComposite<float>
{
    [InputControl(layout = "Vector2")]
    public int axis;

    public override float ReadValue(ref InputBindingCompositeContext context)
    {
        var axisInput = context.ReadValue<Vector2, Vector2MagnitudeComparer>(axis);
        return axisInput.magnitude > 0.25f ? 1 : 0;
    }

    public override float EvaluateMagnitude(ref InputBindingCompositeContext context)
    {
        return ReadValue(ref context);
    }

    static UnityXRFakeThumbTouchComposite()
    {
        InputSystem.RegisterBindingComposite<UnityXRFakeThumbTouchComposite>("FakeThumbTouch");
    }

    [RuntimeInitializeOnLoadMethod]
    static void Init() { } // Trigger static constructor.
}
