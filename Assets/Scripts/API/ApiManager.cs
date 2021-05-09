using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using TiltBrush;
using UnityEngine;

public class ApiManager : MonoBehaviour
{
    private const string ROOT_API_PATH = "/api/v1";
    
    private Queue m_RequestedCommandQueue = Queue.Synchronized(new Queue());
    private static ApiManager m_Instance;
    private Dictionary<string, ApiEndpoint> endpoints;

    [NonSerialized] public Vector3 BrushPosition;
    [NonSerialized] public Quaternion BrushBearing = Quaternion.LookRotation(Vector3.forward, Vector3.up);

    public static ApiManager Instance
    {
        get { return m_Instance; }
    }

    void Awake()
    {
        m_Instance = this;
        Populate();
    }
    private void Populate()
    {
        endpoints = new Dictionary<string, ApiEndpoint>();
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(t => t.GetTypes())
            .Where(t => t.IsClass && t.Namespace == "TiltBrush");

        foreach (var type in types)
        {
            foreach (MethodInfo methodInfo in type.GetMethods())
            {
                var attrs = Attribute.GetCustomAttributes(methodInfo, typeof(ApiEndpoint));
                foreach (Attribute attr in attrs)
                {
                    ApiEndpoint apiEndpoint = (ApiEndpoint)attr;
                    bool valid = false;
                    if (type.IsAbstract && type.IsSealed) // therefore is static
                    {
                        apiEndpoint.instance = null;
                        valid = true;
                    }
                    else if (type.IsSubclassOf(typeof(MonoBehaviour)))
                    {
                        apiEndpoint.instance = FindObjectOfType(type);
                        if (apiEndpoint.instance != null)
                        {
                            valid = true;
                        }
                        else
                        {
                            Debug.LogWarning($"No instance found for ApiEndpoint on: {type}");
                        }
                    }
                    if (valid)
                    {
                        apiEndpoint.type = type;
                        apiEndpoint.methodInfo = methodInfo;
                        apiEndpoint.parameterInfo = methodInfo.GetParameters();
                        endpoints[apiEndpoint.Endpoint] = apiEndpoint;
                    }
                    else
                    {
                        Debug.LogWarning($"ApiEndpoint declared on invalid class: {type}");
                    }
                }
            }
        }
        App.HttpServer.AddHttpHandler(ROOT_API_PATH, ApiCommandCallback);
    }
    public bool InvokeEndpoint(KeyValuePair<string, string> command)
    {
        if (endpoints.ContainsKey(command.Key))
        {
            var endpoint = endpoints[command.Key];
            var parameters = endpoint.DecodeParams(command.Value);
            endpoint.Invoke(parameters);
            return true;
        }
        else
        {
            Debug.LogError($"Invalid API command: {command.Key}");
        }
        return false;
    }

    string ApiCommandCallback(HttpListenerRequest request)
    {

        KeyValuePair<string, string> command;

        // Handle GET
        foreach (string pair in request.Url.Query.TrimStart('?').Split('&'))
        {
            var kv = pair.Split(new[]{'='}, 2);
            command = new KeyValuePair<string, string>(kv[0], kv[1]);
            m_RequestedCommandQueue.Enqueue(command);
        }
        
        // Handle POST
        // TODO also accept JSON
        if (request.HasEntityBody)
        {
            using (Stream body = request.InputStream)
            {
                using (var reader = new StreamReader(body, request.ContentEncoding))
                {
                    var formdata = Uri.UnescapeDataString(reader.ReadToEnd());
                    var pairs = formdata.Split('&');
                    foreach (var pair in pairs)
                    {
                        var kv = pair.Split(new[]{'='}, 2);
                        command = new KeyValuePair<string, string>(kv[0], kv[1]);
                        m_RequestedCommandQueue.Enqueue(command);
                    }
                }
            }
        }
        
        return "OK";
    }

    private bool HandleApiCommand()
    {
        KeyValuePair<string, string> command;
        try
        {
            command = (KeyValuePair<string, string>)m_RequestedCommandQueue.Dequeue();
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return Instance.InvokeEndpoint(command);
    }

    private void Update()
    {
        HandleApiCommand();
    }

}
