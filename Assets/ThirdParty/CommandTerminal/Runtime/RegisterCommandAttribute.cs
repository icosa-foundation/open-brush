using System;
using System.Reflection;

namespace CommandTerminal
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RegisterCommandAttribute : Attribute
    {
        int min_arg_count = 0;
        int max_arg_count = -1;

        public int MinArgCount {
            get { return min_arg_count; }
            set { min_arg_count = value; }
        }

        public int MaxArgCount {
            get { return max_arg_count; }
            set { max_arg_count = value; }
        }

        public string Name { get; set; }
        public string Help { get; set; }
        public string Hint { get; set; }

        public RegisterCommandAttribute(string command_name = null) {
            Name = command_name;
        }
    }
}
