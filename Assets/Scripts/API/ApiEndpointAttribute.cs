// Copyright 2021 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TiltBrush
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ApiEndpoint : Attribute
    {
        public Type type;
        public MethodInfo methodInfo;
        public object instance;
        public ParameterInfo[] parameterInfo;

        private string m_Endpoint;
        private string m_Description;

        public ApiEndpoint(string endpoint, string description, string exampleUsage = null)
        {
            m_Endpoint = endpoint;
            m_Description = description;
            if (exampleUsage != null)
            {
                if (ApiManager.Instance.CommandExamples == null)
                {
                    ApiManager.Instance.CommandExamples = new Dictionary<string, string>();
                }
                ApiManager.Instance.CommandExamples[endpoint] = exampleUsage;
            }
        }

        public virtual string Endpoint
        {
            get { return m_Endpoint; }
        }
        public string Description
        {
            get { return m_Description; }
        }

        public Dictionary<string, string> ParamsAsDict()
        {
            var paramInfo = new Dictionary<string, string>();
            foreach (var param in parameterInfo)
            {
                string typeName = param.ParameterType.Name;
                paramInfo[param.Name] = typeName;
            }
            return paramInfo;
        }

        public object Invoke(System.Object[] parameters)
        {
            return methodInfo.Invoke(instance, parameters);
        }

        public object[] DecodeParams(string commandValue)
        {
            var parameters = new object[parameterInfo.Length];

            string[] tokens = commandValue.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

            int tokenIndex = 0;

            for (var i = 0; i < parameterInfo.Length; i++)
            {
                ParameterInfo paramType = parameterInfo[i];
                object paramValue;

                // Stop parsing if we run out of tokens and the current param is optional
                // (All following params can be assumed to also be optional)
                if (i >= tokens.Length && paramType.IsOptional) break;

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
                else if (paramType.ParameterType == typeof(bool))
                {
                    paramValue = tokens[tokenIndex++];
                    string str = paramValue.ToString().ToLower();
                    paramValue = (str == "true" || str == "on" || str == "1");
                }
                else
                {
                    paramValue = TypeDescriptor.GetConverter(paramType).ConvertFromString(tokens[tokenIndex++]);
                }
                parameters[i] = paramValue;

                // Running out of tokens happens if we're calling a method with optional parameters
                if (tokenIndex >= tokens.Length) break;

            }
            return parameters;
        }
    }
}
