using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using UnityEngine;

namespace TiltBrush
{
    public static class LuaDocsRegistration
    {

        public static List<LuaDocsClass> ApiDocClasses;

        public static void RegisterForDocs(Type t, bool isTopLevelClass = true)
        {
            bool isHidden(ICustomAttributeProvider info)
            {
                // Ignore MoonSharpHidden and MoonSharpVisible if false
                if (info.GetCustomAttributes(typeof(MoonSharpHiddenAttribute), true).Length > 0) return true;
                var vis = info.GetCustomAttributes(typeof(MoonSharpVisibleAttribute), true);
                if (vis.Length > 0)
                    if (!((MoonSharpVisibleAttribute)vis[0]).Visible)
                        return true;
                return false;
            }

            // Only run this code if ApiDocClasses has been initialized by LuaDocsGenerator
            if (Application.isEditor && ApiDocClasses != null)
            {
                string GetClassDescription()
                {
                    var attr = Attribute.GetCustomAttribute(t, typeof(LuaDocsDescriptionAttribute));
                    if (attr == null) return "";
                    return ((LuaDocsDescriptionAttribute)attr).Description;
                }

                string GetPropertyDescription(MemberInfo m)
                {
                    var attr = m.GetCustomAttribute<LuaDocsDescriptionAttribute>();
                    if (attr == null) return "";
                    return attr.Description;
                }

                string GetMethodDescription(MethodBase m)
                {
                    var attr = m.GetCustomAttribute<LuaDocsDescriptionAttribute>();
                    if (attr == null) return "";
                    return attr.Description;
                }

                string GetMethodExample(MethodBase m)
                {
                    var attr = m.GetCustomAttribute<LuaDocsExampleAttribute>();
                    if (attr == null) return "";
                    return attr.Example;
                }

                string GetReturnValueDescription(MethodBase m)
                {
                    var attr = m.GetCustomAttribute<LuaDocsReturnValueAttribute>();
                    if (attr == null) return "";
                    return attr.Description;
                }

                Dictionary<string, string> GetMethodParameters(MethodBase m)
                {
                    var attrs = m.GetCustomAttributes<LuaDocsParameterAttribute>();
                    var paramsDict = new Dictionary<string, string>();
                    foreach (var attr in attrs)
                    {
                        paramsDict[attr.Name] = attr.Description;
                    }
                    return paramsDict;
                }


                string className = t.ToString()
                    .Replace("ApiWrapper", "")
                    .Replace("TiltBrush.", "");

                var apiDocClass = new LuaDocsClass
                {
                    Name = className,
                    Description = GetClassDescription(),
                    Properties = new List<LuaDocsProperty>(),
                    Methods = new List<LuaDocsMethod>(),
                    IsTopLevelClass = isTopLevelClass
                };

                foreach (var prop in t.GetProperties())
                {
                    if (isHidden(prop)) continue;

                    var typeName = prop.PropertyType.ToString();
                    var property = new LuaDocsProperty
                    {
                        Name = prop.Name,
                        Description = GetPropertyDescription(prop),
                        PropertyType = LuaDocsType.CsharpTypeToDocsType(typeName),
                        ReadWrite = prop.CanWrite,
                        Static = prop.IsStatic()
                    };
                    apiDocClass.Properties.Add(property);
                }

                foreach (var prop in t.GetMethods().Where(m => !m.IsSpecialName)
                             .Where(x =>
                                 x.Name != "Equals" &&
                                 x.Name != "GetHashCode" &&
                                 x.Name != "GetType" &&
                                 x.Name != "ToString"))
                {
                    if (isHidden(prop)) continue;

                    var method = new LuaDocsMethod
                    {
                        Name = prop.Name,
                        Description = GetMethodDescription(prop),
                        ReturnValueDescription = GetReturnValueDescription(prop),
                        Example = GetMethodExample(prop),
                        Parameters = new List<LuaDocsParameter>(),
                        Static = prop.IsStatic

                    };

                    var paramDict = GetMethodParameters(prop);
                    foreach (var param in prop.GetParameters())
                    {
                        var typeName = param.ParameterType.ToString();
                        string description;
                        if (!paramDict.TryGetValue(param.Name, out description)) description = "";
                        var parameter = new LuaDocsParameter
                        {
                            Name = param.Name,
                            Description = description,
                            ParameterType = LuaDocsType.CsharpTypeToDocsType(typeName)
                        };
                        method.Parameters.Add(parameter);
                    }

                    var returnTypeName = prop.ReturnType.ToString();
                    method.ReturnType = LuaDocsType.CsharpTypeToDocsType(returnTypeName);

                    apiDocClass.Methods.Add(method);
                }
                ApiDocClasses.Add(apiDocClass);
            }
        }
    }
}
