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
        public List<LuaDocsEnumValue> EnumValues;
        public List<LuaDocsProperty> StaticProperties => Properties.Where(p => p.Static).ToList();
        public List<LuaDocsProperty> InstanceProperties => Properties.Where(p => !p.Static).ToList();
        public List<LuaDocsMethod> Methods;
        public List<LuaDocsMethod> StaticMethods => Methods.Where(p => p.Static).ToList();
        public List<LuaDocsMethod> InstanceMethods => Methods.Where(p => !p.Static).ToList();
        public bool IsTopLevelClass;

        // 0=name 1=description 2=static properties 3=instance properties 4=enum values 5=static methods 6=instance methods
        private string markdownTemplateForClass = @"
# {0}

## Summary
{1}
{2}
{3}{4}
{5}
{6}
";

        private string markdownTemplateForProperties = @"
## {0} Properties

<table data-full-width=""false"">
<thead><tr><th>Name</th><th>Return Type</th><th>Description</th></tr></thead>
<tbody>
{1}
</tbody></table>

";

        private string markdownTemplateForEnumValues = @"
## Values

<ul>{0}</ul>

";

        // 0=Static or Instance heading. 1=methods
        private string markdownTemplateForMethods = @"
## {0} Methods

        {1}
    ";

        public string AutocompleteSerialize()
        {
            string properties = "";
            string methods = "";
            string enumValues = "";

            if (Properties.Count > 0) properties = $@"{String.Join("\n",
                Properties
    .Where(p => p.Name != "Item")  // Exclude indexers
    .Select(p => p.AutocompleteSerialize()))}
";
            if (EnumValues.Count > 0) enumValues = $@"{String.Join("\n",
                EnumValues.Select(p => p.AutocompleteSerialize(Name)))}

";
            if (Methods.Count > 0) methods = $@"{String.Join("\n",
                Methods.Select(m => m.AutocompleteSerialize(Name)))}
";

            return $@"

---@class {Name}
{properties}{Name} = {{}}
{enumValues}{methods}";
        }

        public string MarkdownSerialize()
        {
            string staticProperties = "";
            string instanceProperties = "";
            string enumValues = "";

            if (StaticProperties.Count > 0)
            {
                staticProperties = String.Format(
                    markdownTemplateForProperties,
                    "Class",
                    String.Join("\n", StaticProperties.Select(p => p.MarkdownSerialize()))
                );
            }

            if (InstanceProperties.Count > 0)
            {
                instanceProperties = String.Format(
                    markdownTemplateForProperties,
                    "Instance",
                    String.Join("\n", InstanceProperties.Select(p => p.MarkdownSerialize()))
                );
            }

            if (EnumValues != null && EnumValues.Count > 0)
            {
                enumValues = String.Format(
                    markdownTemplateForEnumValues,
                    String.Join("\n", EnumValues.Select(p => p.MarkdownSerialize()))
                );
            }

            foreach (var property in Properties)
            {
                if (string.IsNullOrEmpty(property.Description))
                {
                    Debug.LogWarning($"Missing Description for property {Name}.{property.Name}");
                }
            }

            string staticMethods = "";
            string instanceMethods = "";

            if (StaticMethods.Count > 0)
            {
                staticMethods = String.Join("\n", StaticMethods.Select(m => m.MarkdownSerialize(Name)));
                staticMethods = String.Format(markdownTemplateForMethods, "Class", staticMethods);
            }

            if (InstanceMethods.Count > 0)
            {
                instanceMethods = String.Join("\n", InstanceMethods.Select(m => m.MarkdownSerialize(Name)));
                instanceMethods = String.Format(markdownTemplateForMethods, "Instance", instanceMethods);
            }

            if (string.IsNullOrEmpty(Description))
            {
                Debug.LogWarning($"Missing Description for class {Name}");
            }

            return string.Format(markdownTemplateForClass, Name, Description, staticProperties, instanceProperties, enumValues, staticMethods, instanceMethods);
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

        public static LuaDocsType CsharpTypeToDocsType(Type reflectedType)
        {
            string reflectedTypeName = reflectedType.ToString();
            var docsType = reflectedTypeName switch
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
                    CustomTypeName = reflectedTypeName
                        .Replace("TiltBrush.", "")
                        .Replace("ApiWrapper", ""),
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
        public bool IsOptional;
        public string DefaultValue;

        // 0=name 1=type 2=default value 3=description
        private string markdownTemplate = "<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td></tr>";

        public string MarkdownSerialize()
        {
            return string.Format(markdownTemplate, Name, ParameterType.TypeAsMarkdownString(), DefaultValue, Description);
        }

        public string AutocompleteSerialize()
        {
            string suffix = IsOptional ? "?" : "";
            return $"---@param {Name}{suffix} {ParameterType.TypeAsLuaString()} {Description}";
        }
    }

    [Serializable]
    public class LuaDocsMethod
    {
        public string Name;
        public List<LuaDocsParameter> Parameters;
        public LuaDocsType ReturnType;
        public string Description;
        public string ReturnValueDescription;
        public string Example;
        public bool Static;

        // 0=classname 1=methodname 2=method signature 3=description 4=returnType 5=returnValueDescription 6=parameters 7=example
        private string markdownTemplate = @"
### {0}:{1}({2})

{3}

**Returns:** {4} {5}

{6}

{7}
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
<thead><tr><th>Name</th><th>Type</th><th>Default</th><th>Description</th></tr></thead>
<tbody>{parameters}</tbody></table>

";
            }

            if (string.IsNullOrEmpty(Description))
            {
                Debug.LogWarning($"Missing Description for method {className}:{Name}");
            }

            foreach (var parameter in Parameters)
            {
                if (string.IsNullOrEmpty(parameter.Description))
                {
                    Debug.LogWarning($"Missing Parameter Description for {parameter.Name} on {className}:{Name}");
                }
            }

            string returnValueDescription = "";
            if (!string.IsNullOrEmpty(ReturnValueDescription))
            {
                returnValueDescription = $@" ({ReturnValueDescription})";
            }
            else if (ReturnType.PrimitiveType != LuaDocsPrimitiveType.Nil)
            {
                Debug.LogWarning($"Missing Return Value Description for {className}:{Name}");
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
                Debug.LogWarning($"Missing Example for {className}:{Name}");
            }

            string lowerCaseFirstChar(string s) => String.IsNullOrEmpty(s) ? s : Char.ToLower(s[0]) + s.Substring(1);
            className = Static ? className : lowerCaseFirstChar(className);
            return string.Format(markdownTemplate, className, Name, methodSignature, Description,
                ReturnType.TypeAsMarkdownString(), returnValueDescription, parameters, example);
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
                returnTypeAnnotation = $"\n---@return {ReturnType.TypeAsLuaString()} # {ReturnValueDescription}";
            }

            return $@"{parameters}{returnTypeAnnotation}
function {className}:{Name}({string.Join(", ", Parameters.Select(p => p.Name))}) end
";
        }
    }

    [Serializable]
    public class LuaDocsEnumValue
    {
        public string Name;
        private string markdownTemplate = "<li>{0}</li>";

        public string MarkdownSerialize()
        {
            string name = Name;
            return string.Format(markdownTemplate, name);
        }

        public string AutocompleteSerialize(string className)
        {
            return $"{className}.{Name} = nil";
        }
    }

    [Serializable]
    public class LuaDocsProperty
    {
        public string Name;
        public LuaDocsType PropertyType;
        public LuaDocsType IndexerType;
        public string Description;
        public bool ReadWrite;
        public bool Static;

        // 0=name 1=type 2=ReadWrite 3=description
        private string markdownTemplate = "<tr><td>{0}</td><td>{1}<br>{2}</td><td>{3}</td></tr>";

        public string MarkdownSerialize()
        {
            string readwrite = ReadWrite ? "Read/Write" : "Read-only";
            string name = Name;
            if (name == "Item") name = "this[index]";
            return string.Format(markdownTemplate, name, PropertyType.TypeAsMarkdownString(), readwrite, Description);
        }

        public string AutocompleteSerialize()
        {
            string indexerAnnotation = "";
            if (IndexerType != null)
            {
                // Union with the indexer type
                indexerAnnotation = $" | {IndexerType.TypeAsLuaString()}[]";
            }
            return $"---@field {Name} {PropertyType.TypeAsLuaString()}{indexerAnnotation} {Description}";
        }
    }
}
