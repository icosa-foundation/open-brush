using System;
using System.ComponentModel;
using System.Linq;
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
        
        string[] tokens = commandValue.Split(',').Select(x=>x.Trim()).ToArray();
        
        int tokenIndex = 0;
        for (var i = 0; i < parameterInfo.Length; i++)
        {
            ParameterInfo paramType = parameterInfo[i];
            object paramValue;
            
            if (paramType.ParameterType == typeof(string))
            {
                if (parameterInfo.Length == 1 && i == 0)
                {
                    // Special case methods with one string param
                    // This allows string params to include commas if they are the only parameter
                    paramValue = commandValue;
                }
                else
                {
                    paramValue = tokens[tokenIndex++];
                }
            }
            else if (paramType.ParameterType == typeof(float))
            {
                paramValue = float.Parse(tokens[tokenIndex++]);
            }
            else if (paramType.ParameterType == typeof(int))
            {
                paramValue = int.Parse(tokens[tokenIndex++]);
            }
            else if (paramType.ParameterType == typeof(Vector3))
            {
                paramValue = new Vector3(
                    float.Parse(tokens[tokenIndex++]),
                    float.Parse(tokens[tokenIndex++]),
                    float.Parse(tokens[tokenIndex++])
                );
            }
            else
            {
                paramValue = TypeDescriptor.GetConverter(paramType).ConvertFromString(tokens[tokenIndex++]);
            }
            parameters[i] = paramValue;
        }
        return parameters;
    }
}
