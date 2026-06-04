using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FindMovingController : MonoBehaviour
{
    class Entry
    {
        public Transform t;
        public Vector3 lastPos;
        public Quaternion lastRot;
        public float movementScore;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private float timer;

    void Start()
    {
        foreach (Transform t in FindObjectsOfType<Transform>())
        {
            entries.Add(new Entry
            {
                t = t,
                lastPos = t.position,
                lastRot = t.rotation,
                movementScore = 0f
            });
        }

        Debug.Log("FindMovingController started. Bewege jetzt deinen Mal-Controller deutlich.");
    }

    void Update()
    {
        foreach (Entry e in entries)
        {
            if (e.t == null) continue;

            float posDelta = Vector3.Distance(e.t.position, e.lastPos);
            float rotDelta = Quaternion.Angle(e.t.rotation, e.lastRot) * 0.01f;

            e.movementScore += posDelta + rotDelta;

            e.lastPos = e.t.position;
            e.lastRot = e.t.rotation;
        }

        timer += Time.deltaTime;

        if (timer > 3f)
        {
            timer = 0f;

            Debug.Log("=== Moving Transform Candidates ===");

            foreach (Entry e in entries
                .Where(x => x.t != null && x.movementScore > 0.01f)
                .OrderByDescending(x => x.movementScore)
                .Take(20))
            {
                Debug.Log(
                    e.movementScore.ToString("F4") + " | " + GetPath(e.t),
                    e.t.gameObject
                );

                e.movementScore = 0f;
            }
        }
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
