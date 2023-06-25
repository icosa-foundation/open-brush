using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace TiltBrush
{
    [Serializable]
    public class ApiDocClass
    {
        public string Name;
        public string Description;

        public List<ApiDocProperty> Properties;
        public List<ApiDocMethod> Methods;
        
        // 0=name 1=description 2=properties 3=methods
        private string markdownTemplate = @"
# {0}

## Summary

{1}

{2}

{3}
";
        public string AutocompleteSerialize()
        {
            string properties = "";
            string methods = "";

            if (Properties.Count > 0)
            {
                properties = $@"
---Properties for type {Name}

{String.Join("\n", Properties.Select(p => p.AutocompleteSerialize(Name)))}

";
            }

            if (Methods.Count > 0)
            {
                methods = $@"---Methods for type {Name}

{String.Join("\n", Methods.Select(m => m.AutocompleteSerialize(Name)))}";

            }
            
            return $"{properties}{methods}";
        }

        public string MarkdownSerialize()
        {
            string properties = "";
            string methods = "";
            
            if (Properties.Count > 0)
            {
                properties = String.Join("\n", Properties.Select(p => p.MarkdownSerialize()));
                properties = $@"
## Properties

<table>
<thead><tr><th width=""225"">Name</th><th width=""160"">Return Type</th><th>Description</th></tr></thead>
<tbody>
{properties}
<tr><td></td><td></td><td></td></tr></tbody></table>

";
            }
            if (Methods.Count > 0)
            {
                methods = String.Join("\n", Methods.Select(m => m.MarkdownSerialize(Name)));
                methods = $@"
## Methods

{methods}
";
            }
            return string.Format(markdownTemplate, Name, Description, properties, methods);
        }

        public string JsonSerialize()
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            return json;
        }
        
        public static ApiDocClass Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<ApiDocClass>(json);
        }
    }

    [Serializable]
    public enum ApiDocPrimitiveType
    {
        Boolean,
        String,
        Number,
        Function,
        UserData,
        Table,
        DynValue,
        Nil
    }
    
    [Serializable]
    public class ApiDocType
    {
        public ApiDocPrimitiveType PrimitiveType;
        [CanBeNull] public string CustomTypeName;
        public bool IsTable;
        
        public static ApiDocType CsharpTypeToDocsType(string reflectedType)
        {
            var docsType = reflectedType switch
            {
                "System.Boolean" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.Boolean
                },
                "System.Single" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.Number
                },
                "System.String" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.String
                },
                "System.Int32" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.Number
                },
                "MoonSharp.Interpreter.Closure" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.Function,
                },
                "MoonSharp.Interpreter.Table" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.Table,
                },
                "MoonSharp.Interpreter.DynValue" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.Table,
                },
                "TiltBrush.TrTransform" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.UserData,
                    CustomTypeName = "Transform"
                },
                "UnityEngine.Vector2" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.UserData,
                    CustomTypeName = "Vector2"
                },
                "UnityEngine.Vector3" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.UserData,
                    CustomTypeName = "Vector3"
                },
                "System.Single[]" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.Number,
                    IsTable = true
                },
                "System.Collections.Generic.List`1[UnityEngine.Vector3]" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.UserData,
                    CustomTypeName = "Vector3",
                    IsTable = true
                },
                "System.Collections.Generic.List`1[UnityEngine.Vector2]" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.UserData,
                    CustomTypeName = "Vector2",
                    IsTable = true
                },
                "System.ValueTuple`2[System.Single,UnityEngine.Vector3]" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.UserData,
                    CustomTypeName = "Number, Vector3"
                },
                "System.Collections.Generic.List`1[UnityEngine.Color]" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.UserData,
                    CustomTypeName = "Color",
                    IsTable = true
                },
                "System.Collections.Generic.List`1[System.String]" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.String,
                    IsTable = true
                },
                "System.Collections.Generic.List`1[TiltBrush.TrTransform]" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.UserData,
                    CustomTypeName = "Transform",
                    IsTable = true
                },
                "System.Collections.Generic.List`1[TiltBrush.PathApiWrapper]" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.UserData,
                    CustomTypeName = "Path",
                    IsTable = true
                },
                "UnityEngine.Quaternion" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.UserData,
                    CustomTypeName = "Rotation"
                },
                "UnityEngine.Color" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.UserData,
                    CustomTypeName = "Color"
                },
                "System.Void" => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.Nil,
                },
                _ => new ApiDocType
                {
                    PrimitiveType = ApiDocPrimitiveType.UserData,
                    CustomTypeName = reflectedType
                        .Replace("TiltBrush.", "")
                        .Replace("ApiWrapper", "")
                }
            };

            return docsType;
        }

        public string TypeAsLuaString()
        {
            string luaString;
            if (PrimitiveType == ApiDocPrimitiveType.DynValue)
            {
                // Hmmmm...
                luaString = "Any";
            }
            else if (CustomTypeName != null)
            {
                luaString = CustomTypeName;
            }
            else
            {
                luaString = PrimitiveType.ToString().ToLower();
            }
            if (IsTable)
            {
                luaString = $"{luaString}[]";
            }
            return luaString;
        }
    }
    
    [Serializable]
    public class ApiDocParameter
    {
        public string Name;
        public ApiDocType ParameterType;
        public string Description;
        
        // 0=name 1=type 2=description
        private string markdownTemplate = "<tr><td>{0}</td><td>{1}</td><td>{2}</td></tr>";
        public string MarkdownSerialize()
        {
            return string.Format(markdownTemplate, Name, ParameterType.TypeAsLuaString(), Description);
        }
        
        public string AutocompleteSerialize()
        {
            return $"---@param {Name} {ParameterType.TypeAsLuaString()}";
        }
    }
    
    [Serializable]
    public class ApiDocMethod
    {
        public string Name;
        public List<ApiDocParameter> Parameters;
        public ApiDocType ReturnType;
        public string Description;
        public string Example;

        // 0=classname 1=methodname 2=description 3=returnType 4=parameters 5=example
        private string markdownTemplate = @"
### {0}:{1}

{2}

**Returns:** {3}

{4}

{5}
";
        public string MarkdownSerialize(string className)
        {
            string parameters = "";
            if (Parameters.Count > 0)
            {
                parameters = String.Join("\n", Parameters.Select(m => m.MarkdownSerialize()));
                parameters = $@"
**Parameters:**

<table data-full-width=""false"">
<thead><tr><th width=""217"">Name</th><th width=""134"">Type</th><th>Description</th></tr></thead>
<tbody>{parameters}</tbody></table>

";
            }
            string example = "";
            if (!string.IsNullOrEmpty(Example))
            {
                example = $@"
#### Example

<pre class=""language-lua""><code class=""lang-lua""><strong>{Example}</strong></code></pre>

";
            }
            return string.Format(markdownTemplate, className, Name, Description, ReturnType.TypeAsLuaString(), parameters, example);
        }
        
        public string AutocompleteSerialize(string className)
        {

            string parameters = "";
            if (Parameters.Count > 0)
            {
                parameters = String.Join("\n", Parameters.Select(m => m.AutocompleteSerialize()));
            }

            string returnTypeAnnotation = "";
            if (ReturnType.PrimitiveType != ApiDocPrimitiveType.Nil)
            {
                returnTypeAnnotation = $"\n---@return {ReturnType.TypeAsLuaString()}";
            }
            
            return $@"{parameters}{returnTypeAnnotation}
function {className}:{Name}({string.Join(", ", Parameters.Select(p => p.Name))}) end
";
        }
    }

    [Serializable]
    public class ApiDocProperty
    {
        public string Name;
        public ApiDocType PropertyType;
        public string Description;
        
        // 0=name 1=type 2=description
        private string markdownTemplate = "<tr><td>{0}</td><td>{1}</td><td>{2}</td></tr>";
        
        public string MarkdownSerialize()
        {
            return string.Format(markdownTemplate, Name, PropertyType.TypeAsLuaString(), Description);
        }

        public string AutocompleteSerialize(string className)
        {
            return $@"---@type {PropertyType.TypeAsLuaString()}
{className}.{Name} = nil
";
        }
    }
}
