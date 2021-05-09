using System;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class ApiEndpoint : Attribute  
{
    private string endpoint;
    public Type type;
    public MethodInfo methodInfo;
    public object instance;
    public ParameterInfo[] parameterInfo;

    public ApiEndpoint(string endpoint)
    {
        this.endpoint = endpoint;
    }

    public virtual string Endpoint
    {
        get {return endpoint;}
    }

    public void Invoke(System.Object[] parameters)
    {
        methodInfo.Invoke(instance, parameters);
    }

    public object[] DecodeParams(string commandValue)
    {
        var parameters = new object[parameterInfo.Length];
        
        // TODO multiple parameters.
        // Do we need them?
        // for (var i = 0; i < parameterInfo.Length; i++)
        // {
            int i = 0;
            ParameterInfo paramType = parameterInfo[i];
            object paramValue;
            if (paramType.ParameterType == typeof(string))
            {
                paramValue = commandValue;
            }
            else if (paramType.ParameterType == typeof(float))
            {
                paramValue = float.Parse(commandValue);
            }
            else if (paramType.ParameterType == typeof(Vector3))
            {
                string[] temp = commandValue.Split(',');
                paramValue = new Vector3(float.Parse(temp[0]), float.Parse(temp[1]), float.Parse(temp[2])); 
            }
            else
            {
                paramValue = TypeDescriptor.GetConverter(paramType).ConvertFromString(commandValue);
            }
            parameters[i] = paramValue;
        // }
        return parameters;
    }
}
