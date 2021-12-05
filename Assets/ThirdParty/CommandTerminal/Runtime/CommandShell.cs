using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CommandTerminal
{
    public struct CommandInfo
    {
        public Action<CommandArg[]> proc;
        public int max_arg_count;
        public int min_arg_count;
        public string help;
        public string hint;
    }

    public struct CommandArg
    {
        public string String { get; set; }

        public int Int
        {
            get
            {
                int int_value;

                if (int.TryParse(String, out int_value))
                {
                    return int_value;
                }

                TypeError("int");
                return 0;
            }
        }

        public float Float
        {
            get
            {
                float float_value;

                if (float.TryParse(String, out float_value))
                {
                    return float_value;
                }

                TypeError("float");
                return 0;
            }
        }

        public bool Bool
        {
            get
            {
                if (string.Compare(String, "TRUE", ignoreCase: true) == 0)
                {
                    return true;
                }

                if (string.Compare(String, "FALSE", ignoreCase: true) == 0)
                {
                    return false;
                }

                TypeError("bool");
                return false;
            }
        }

        public override string ToString()
        {
            return String;
        }

        void TypeError(string expected_type)
        {
            Terminal.Shell.IssueErrorMessage(
                "Incorrect type for {0}, expected <{1}>",
                String, expected_type
            );
        }
    }

    public class CommandShell
    {
        Dictionary<string, CommandInfo> commands = new Dictionary<string, CommandInfo>();
        Dictionary<string, CommandArg> variables = new Dictionary<string, CommandArg>();
        List<CommandArg> arguments = new List<CommandArg>(); // Cache for performance

        public string IssuedErrorMessage { get; private set; }

        public Dictionary<string, CommandInfo> Commands
        {
            get { return commands; }
        }

        public Dictionary<string, CommandArg> Variables
        {
            get { return variables; }
        }

        /// <summary>
        /// Uses reflection to find all RegisterCommand attributes
        /// and adds them to the commands dictionary.
        /// </summary>
        public void RegisterCommands()
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            
            var rejected_commands = new Dictionary<string, CommandInfo>();
            var method_flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(method_flags))
                    {
                        var attribute = Attribute.GetCustomAttribute(
                            method, typeof(RegisterCommandAttribute)) as RegisterCommandAttribute;

                        if (attribute == null)
                        {
                            if (method.Name.StartsWith("FRONTCOMMAND", StringComparison.CurrentCultureIgnoreCase))
                            {
                                // Front-end Command methods don't implement RegisterCommand, use default attribute
                                attribute = new RegisterCommandAttribute();
                            }
                            else
                            {
                                continue;
                            }
                        }

                        var methods_params = method.GetParameters();

                        string command_name = InferFrontCommandName(method.Name);
                        Action<CommandArg[]> proc;

                        if (attribute.Name == null)
                        {
                            // Use the method's name as the command's name
                            command_name = InferCommandName(command_name == null ? method.Name : command_name);
                        }
                        else
                        {
                            command_name = attribute.Name;
                        }

                        if (methods_params.Length != 1 || methods_params[0].ParameterType != typeof(CommandArg[]))
                        {
                            // Method does not match expected Action signature,
                            // this could be a command that has a FrontCommand method to handle its arguments.
                            rejected_commands.Add(command_name.ToUpper(), CommandFromParamInfo(methods_params, attribute.Help));
                            continue;
                        }

                        // Convert MethodInfo to Action.
                        // This is essentially allows us to store a reference to the method,
                        // which makes calling the method significantly more performant than using MethodInfo.Invoke().
                        proc = (Action<CommandArg[]>)Delegate.CreateDelegate(typeof(Action<CommandArg[]>), method);
                        AddCommand(command_name, proc, attribute.MinArgCount, attribute.MaxArgCount, attribute.Help, attribute.Hint);
                    }
                }
            HandleRejectedCommands(rejected_commands);
            Debug.Log($"Registered shell commands in {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// Parses an input line into a command and runs that command.
        /// </summary>
        public void RunCommand(string line)
        {
            string remaining = line;
            IssuedErrorMessage = null;
            arguments.Clear();

            while (remaining != "")
            {
                var argument = EatArgument(ref remaining);

                if (argument.String != "")
                {
                    if (argument.String[0] == '$')
                    {
                        string variable_name = argument.String.Substring(1).ToUpper();

                        if (variables.ContainsKey(variable_name))
                        {
                            // Replace variable argument if it's defined
                            argument = variables[variable_name];
                        }
                    }
                    arguments.Add(argument);
                }
            }

            if (arguments.Count == 0)
            {
                // Nothing to run
                return;
            }

            string command_name = arguments[0].String.ToUpper();
            arguments.RemoveAt(0); // Remove command name from arguments

            if (!commands.ContainsKey(command_name))
            {
                IssueErrorMessage("Command {0} could not be found", command_name);
                return;
            }

            RunCommand(command_name, arguments.ToArray());
        }

        public void RunCommand(string command_name, CommandArg[] arguments)
        {
            var command = commands[command_name];
            int arg_count = arguments.Length;
            string error_message = null;
            int required_arg = 0;

            if (arg_count < command.min_arg_count)
            {
                if (command.min_arg_count == command.max_arg_count)
                {
                    error_message = "exactly";
                }
                else
                {
                    error_message = "at least";
                }
                required_arg = command.min_arg_count;
            }
            else if (command.max_arg_count > -1 && arg_count > command.max_arg_count)
            {
                // Do not check max allowed number of arguments if it is -1
                if (command.min_arg_count == command.max_arg_count)
                {
                    error_message = "exactly";
                }
                else
                {
                    error_message = "at most";
                }
                required_arg = command.max_arg_count;
            }

            if (error_message != null)
            {
                string plural_fix = required_arg == 1 ? "" : "s";

                IssueErrorMessage(
                    "{0} requires {1} {2} argument{3}",
                    command_name,
                    error_message,
                    required_arg,
                    plural_fix
                );

                if (command.hint != null)
                {
                    IssuedErrorMessage += string.Format("\n    -> Usage: {0}", command.hint);
                }

                return;
            }

            command.proc(arguments);
        }

        public void AddCommand(string name, CommandInfo info)
        {
            name = name.ToUpper();

            if (commands.ContainsKey(name))
            {
                IssueErrorMessage("Command {0} is already defined.", name);
                return;
            }

            commands.Add(name, info);
        }

        public void AddCommand(string name, Action<CommandArg[]> proc, int min_args = 0, int max_args = -1, string help = "", string hint = null)
        {
            var info = new CommandInfo()
            {
                proc = proc,
                min_arg_count = min_args,
                max_arg_count = max_args,
                help = help,
                hint = hint
            };

            AddCommand(name, info);
        }

        public void SetVariable(string name, string value)
        {
            SetVariable(name, new CommandArg() { String = value });
        }

        public void SetVariable(string name, CommandArg value)
        {
            name = name.ToUpper();

            if (variables.ContainsKey(name))
            {
                variables[name] = value;
            }
            else
            {
                variables.Add(name, value);
            }
        }

        public CommandArg GetVariable(string name)
        {
            name = name.ToUpper();

            if (variables.ContainsKey(name))
            {
                return variables[name];
            }

            IssueErrorMessage("No variable named {0}", name);
            return new CommandArg();
        }

        public void IssueErrorMessage(string format, params object[] message)
        {
            IssuedErrorMessage = string.Format(format, message);
        }

        string InferCommandName(string method_name)
        {
            string command_name;
            int index = method_name.IndexOf("COMMAND", StringComparison.CurrentCultureIgnoreCase);

            if (index >= 0)
            {
                // Method is prefixed, suffixed with, or contains "COMMAND".
                command_name = method_name.Remove(index, 7);
            }
            else
            {
                command_name = method_name;
            }

            return command_name;
        }

        string InferFrontCommandName(string method_name)
        {
            int index = method_name.IndexOf("FRONT", StringComparison.CurrentCultureIgnoreCase);
            return index >= 0 ? method_name.Remove(index, 5) : null;
        }

        void HandleRejectedCommands(Dictionary<string, CommandInfo> rejected_commands)
        {
            foreach (var command in rejected_commands)
            {
                if (commands.ContainsKey(command.Key))
                {
                    commands[command.Key] = new CommandInfo()
                    {
                        proc = commands[command.Key].proc,
                        min_arg_count = command.Value.min_arg_count,
                        max_arg_count = command.Value.max_arg_count,
                        help = command.Value.help
                    };
                }
                else
                {
                    IssueErrorMessage("{0} is missing a front command.", command);
                }
            }
        }

        CommandInfo CommandFromParamInfo(ParameterInfo[] parameters, string help)
        {
            int optional_args = 0;

            foreach (var param in parameters)
            {
                if (param.IsOptional)
                {
                    optional_args += 1;
                }
            }

            return new CommandInfo()
            {
                proc = null,
                min_arg_count = parameters.Length - optional_args,
                max_arg_count = parameters.Length,
                help = help
            };
        }

        CommandArg EatArgument(ref string s)
        {
            var arg = new CommandArg();
            int space_index = s.IndexOf(' ');

            if (space_index >= 0)
            {
                arg.String = s.Substring(0, space_index);
                s = s.Substring(space_index + 1); // Remaining
            }
            else
            {
                arg.String = s;
                s = "";
            }

            return arg;
        }
    }
}
