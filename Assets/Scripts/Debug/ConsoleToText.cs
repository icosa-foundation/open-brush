using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class ConsoleToText : MonoBehaviour
{
    // The LogType filter to expose in the Inspector
    public LogType filterType = LogType.Log; 
    public TextMeshProUGUI debugText;

    private readonly StringBuilder output = new StringBuilder();
    private string stack = "";

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
        Debug.Log("Log enabled with filter: " + filterType.ToString());
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Only process the log if its type matches the filter
        if (type != filterType) 
        {
            return;
        }

        string logPrefix = $"[{type}] "; 
        
        output.Insert(0, "\n");
        output.Insert(0, logString);
        output.Insert(0, logPrefix);
        stack = stackTrace;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
        ClearLog();
    }

    public void ClearLog()
    {
        output.Clear();
    }

    private void LateUpdate()
    {
        if (debugText != null)
            debugText.text = output.ToString();
    }
}
