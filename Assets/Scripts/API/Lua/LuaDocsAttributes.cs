using System;

namespace TiltBrush
{

    public class LuaDocsAttributeBase : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class LuaDocsParameterAttribute : LuaDocsAttributeBase
    {
        public string Name;
        public string Description;
        public LuaDocsParameterAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class LuaDocsReturnValueAttribute : LuaDocsAttributeBase
    {
        public string Description;
        public LuaDocsReturnValueAttribute(string description)
        {
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class LuaDocsExampleAttribute : LuaDocsAttributeBase
    {
        public string Example;
        public LuaDocsExampleAttribute(string example)
        {
            Example = example;
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Enum)]
    public class LuaDocsDescriptionAttribute : LuaDocsAttributeBase
    {
        public string Description;
        public LuaDocsDescriptionAttribute(string description)
        {
            Description = description;
        }
    }
}