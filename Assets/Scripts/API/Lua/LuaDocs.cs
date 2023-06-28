using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace TiltBrush
{
    public static class PropertyInfoExtensions
    {
        public static bool IsStatic(this PropertyInfo source, bool nonPublic = false)
            => source.GetAccessors(nonPublic).Any(x => x.IsStatic);
    }

    [Serializable]
    public class LuaDocsClass
    {
        public string Name;
        public string Description;

        public List<LuaDocsProperty> Properties;
        public List<LuaDocsMethod> Methods;
        
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
<thead><tr><th width=""225"">Name</th><th width=""160"">Return Type</th><th width=""80"">Read/Write?</th><th width=""80"">Static?</th><th>Description</th></tr></thead>
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

            if (string.IsNullOrEmpty(Description))
            {
                Debug.LogWarning($"Missing Description for class {Name}");
            }

            return string.Format(markdownTemplate, Name, Description, properties, methods);
        }

        public string JsonSerialize()
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            return json;
        }
        
        public static LuaDocsClass Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<LuaDocsClass>(json);
        }
    }

    [Serializable]
    public enum LuaDocsPrimitiveType
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
    public class LuaDocsType
    {
        public LuaDocsPrimitiveType PrimitiveType;
        [CanBeNull] public string CustomTypeName;
        public bool IsTable;
        
        public static LuaDocsType CsharpTypeToDocsType(string reflectedType)
        {
            var docsType = reflectedType switch
            {
                "System.Boolean" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.Boolean
                },
                "System.Single" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.Number
                },
                "System.String" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.String
                },
                "System.Int32" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.Number
                },
                "MoonSharp.Interpreter.Closure" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.Function,
                },
                "MoonSharp.Interpreter.Table" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.Table,
                },
                "MoonSharp.Interpreter.DynValue" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.Table,
                },
                "TiltBrush.TrTransform" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.UserData,
                    CustomTypeName = "Transform"
                },
                "UnityEngine.Vector2" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.UserData,
                    CustomTypeName = "Vector2"
                },
                "UnityEngine.Vector3" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.UserData,
                    CustomTypeName = "Vector3"
                },
                "System.Single[]" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.Number,
                    IsTable = true
                },
                "System.Collections.Generic.List`1[UnityEngine.Vector3]" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.UserData,
                    CustomTypeName = "Vector3",
                    IsTable = true
                },
                "System.Collections.Generic.List`1[UnityEngine.Vector2]" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.UserData,
                    CustomTypeName = "Vector2",
                    IsTable = true
                },
                "System.ValueTuple`2[System.Single,UnityEngine.Vector3]" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.UserData,
                    CustomTypeName = "Number, Vector3"
                },
                "System.Collections.Generic.List`1[UnityEngine.Color]" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.UserData,
                    CustomTypeName = "Color",
                    IsTable = true
                },
                "System.Collections.Generic.List`1[System.String]" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.String,
                    IsTable = true
                },
                "System.Collections.Generic.List`1[TiltBrush.TrTransform]" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.UserData,
                    CustomTypeName = "Transform",
                    IsTable = true
                },
                "System.Collections.Generic.List`1[TiltBrush.PathApiWrapper]" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.UserData,
                    CustomTypeName = "Path",
                    IsTable = true
                },
                "UnityEngine.Quaternion" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.UserData,
                    CustomTypeName = "Rotation"
                },
                "UnityEngine.Color" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.UserData,
                    CustomTypeName = "Color"
                },
                "System.Void" => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.Nil,
                },
                _ => new LuaDocsType
                {
                    PrimitiveType = LuaDocsPrimitiveType.UserData,
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
            if (PrimitiveType == LuaDocsPrimitiveType.DynValue)
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

        public string TypeAsMarkdownString()
        {
            string markdown = TypeAsLuaString();
            if (!string.IsNullOrEmpty(CustomTypeName) && !IsTable)
            {
                // Make types into links to the class docs
                markdown = $@"<a href=""{markdown.ToLower()}.md"">{markdown}</a>";
            }

            return markdown;
        }
    }
    
    [Serializable]
    public class LuaDocsParameter
    {
        public string Name;
        public LuaDocsType ParameterType;
        public string Description;
        
        // 0=name 1=type 2=description
        private string markdownTemplate = "<tr><td>{0}</td><td>{1}</td><td>{2}</td></tr>";
        public string MarkdownSerialize()
        {
            if (string.IsNullOrEmpty(Description))
            {
                Debug.LogWarning($"Missing Description for parameter {Name}");
            }
            return string.Format(markdownTemplate, Name, ParameterType.TypeAsMarkdownString(), Description);
        }
        
        public string AutocompleteSerialize()
        {
            return $"---@param {Name} {ParameterType.TypeAsLuaString()}";
        }
    }
    
    [Serializable]
    public class LuaDocsMethod
    {
        public string Name;
        public List<LuaDocsParameter> Parameters;
        public LuaDocsType ReturnType;
        public string Description;
        public string Example;
        public bool Static;

        // 0=classname 1=methodname 2=method signature 3=description 4=returnType 5=parameters 6=example
        private string markdownTemplate = @"
### {0}:{1}({2})

{3}

**Returns:** {4}

{5}

{6}
";
        public string MarkdownSerialize(string className)
        {
            string parameters = "";
            string methodSignature = "";

            if (Parameters.Count > 0)
            {
                parameters = String.Join("\n", Parameters.Select(m => m.MarkdownSerialize()));
                methodSignature = String.Join(", ", Parameters.Select(m => m.Name));
                parameters = $@"
**Parameters:**

<table data-full-width=""false"">
<thead><tr><th width=""217"">Name</th><th width=""134"">Type</th><th>Description</th></tr></thead>
<tbody>{parameters}</tbody></table>

";
            }
            if (string.IsNullOrEmpty(Description))
            {
                Debug.LogWarning($"Missing Description for method {className}.{Name}");
            }

            string example = "";
            if (!string.IsNullOrEmpty(Example))
            {
                example = $@"
#### Example

<pre class=""language-lua""><code class=""lang-lua""><strong>{Example}</strong></code></pre>

";
            }
            else
            {
                Debug.LogWarning($"Missing Example for {className}.{Name}");
            }

            if (string.IsNullOrEmpty(Description))
            {
                Debug.LogWarning($"Missing Description for method {Name}");
            }
            className = Static ? className : lowerCaseFirstChar(className);
            return string.Format(markdownTemplate, className, Name, methodSignature, Description, ReturnType.TypeAsMarkdownString(), parameters, example);
        }
        
        public string AutocompleteSerialize(string className)
        {

            string parameters = "";
            if (Parameters.Count > 0)
            {
                parameters = String.Join("\n", Parameters.Select(m => m.AutocompleteSerialize()));
            }

            string returnTypeAnnotation = "";
            if (ReturnType.PrimitiveType != LuaDocsPrimitiveType.Nil)
            {
                returnTypeAnnotation = $"\n---@return {ReturnType.TypeAsLuaString()}";
            }

            return $@"{parameters}{returnTypeAnnotation}
function {className}:{Name}({string.Join(", ", Parameters.Select(p => p.Name))}) end
";
        }
    }

    [Serializable]
    public class LuaDocsProperty
    {
        public string Name;
        public LuaDocsType PropertyType;
        public string Description;
        public bool ReadWrite;
        public bool Static;

        // 0=name 1=type 2=ReadWrite 3=Static 4=description
        private string markdownTemplate = "<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td></tr>";
        
        public string MarkdownSerialize()
        {
            if (string.IsNullOrEmpty(Description))
            {
                Debug.LogWarning($"Missing Description for property {Name}");
            }
            string readwrite = ReadWrite ? "Read/Write" : "Read-only";
            string staticYN = Static ? "Yes" : "No";
            return string.Format(markdownTemplate, Name, PropertyType.TypeAsMarkdownString(), readwrite, staticYN, Description);
        }

        public string AutocompleteSerialize(string className)
        {
            return $@"---@type {PropertyType.TypeAsLuaString()}
{className}.{Name} = nil
";
        }
    }
}
