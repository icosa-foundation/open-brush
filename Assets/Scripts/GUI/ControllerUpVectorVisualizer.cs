using UnityEngine;

/// @class ControllerUpVectorVisualizer
/// @brief Visualizes the controller up vector used by the Markov pen.
///
/// Finds the runtime-generated controller, draws its local up vector,
/// and helps inspect where the Markov pen stem curve is created.
public class ControllerUpVectorVisualizer : MonoBehaviour
{
    public string TargetName = "monterey_controller_R";

    public float Length = 10f;
    public float Width = 0.01f;

    private Transform m_Target;
    private LineRenderer m_Line;

    private void Awake()
    {
        m_Line = gameObject.AddComponent<LineRenderer>();
        m_Line.positionCount = 2;
        m_Line.startWidth = Width;
        m_Line.endWidth = Width;
        m_Line.useWorldSpace = true;
        m_Line.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void Update()
    {
        if (m_Target == null)
        {
            TryFindTarget();

            if (m_Target == null)
            {
                return;
            }
        }

        DrawUpVector();
    }

    private void TryFindTarget()
    {
        GameObject targetObject = GameObject.Find(TargetName);

        if (targetObject == null)
        {
            return;
        }

        m_Target = targetObject.transform;
    }

    private void DrawUpVector()
    {
        Vector3 startPosition = m_Target.position;
        Vector3 endPosition = startPosition + m_Target.up * Length;

        m_Line.SetPosition(0, startPosition);
        m_Line.SetPosition(1, endPosition);
    }

    private string GetPath(Transform targetTransform)
    {
        string path = targetTransform.name;

        while (targetTransform.parent != null)
        {
            targetTransform = targetTransform.parent;
            path = targetTransform.name + "/" + path;
        }

        return path;
    }
}
