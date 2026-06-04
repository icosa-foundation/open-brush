using UnityEngine;

public class ControllerUpVectorVisualizer : MonoBehaviour
{
    public string targetName = "monterey_controller_R";

    public float length = 10f;
    public float width = 0.01f;

    private Transform target;
    private LineRenderer line;

    void Awake()
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width;
        line.useWorldSpace = true;

        line.material = new Material(Shader.Find("Sprites/Default"));
    }

    void Update()
    {
        if (target == null)
        {
            GameObject obj = GameObject.Find(targetName);

            if (obj != null)
            {
                target = obj.transform;
                Debug.Log("Found controller target: " + GetPath(target), obj);
            }
            else
            {
                return;
            }
        }

        Vector3 start = target.position;
        Vector3 end = start + target.up * length;

        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    string GetPath(Transform t)
    {
        string path = t.name;

        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }
}
