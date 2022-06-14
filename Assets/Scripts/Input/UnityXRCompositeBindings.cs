using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Utilities;
using UnityEditor;

#if UNITY_EDITOR
[InitializeOnLoad]
#endif

[DisplayStringFormat("{padButton}+{axisToModify}")]
public class VivePadButtonComposite : InputBindingComposite<float>
{
    [InputControl(layout = "Button")]
    public int padButton;

    [InputControl(layout = "Axis")]
    public int axisToModify;

    public float min;
    public float max;

    public override float ReadValue(ref InputBindingCompositeContext context)
    {
        var buttonVal = context.ReadValue<float>(padButton);
        var axisVal = context.ReadValue<float>(axisToModify);

        return buttonVal > 0 && isWithinRange(axisVal, min, max) ? 1 : 0;
    }

    public override float EvaluateMagnitude(ref InputBindingCompositeContext context)
    {
        return ReadValue(ref context);
    }

    private bool isWithinRange(float value, float min, float max)
    {
        return value >= min && value <= max;
    }

    static VivePadButtonComposite()
    {
        InputSystem.RegisterBindingComposite<VivePadButtonComposite>("VivePadButton");
    }

    [RuntimeInitializeOnLoadMethod]
    static void Init() { } // Trigger static constructor.
}

