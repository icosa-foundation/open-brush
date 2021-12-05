using CommandTerminal;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Grasp
{
    public abstract class ShellCommandBase 
    {
        [SerializeField]
        private string name;

        public string Name => name;

        [SerializeField]
        private string tooltip;

        public string ToolTip => tooltip;

        protected abstract void Execute(CommandArg[] args);
        public abstract void RegisterCommand();
    }

    public abstract class ShellCommand<T> : ShellCommandBase
    {
        [SerializeField]
        protected UnityEvent<T> commandEvent;

        public override void RegisterCommand()
        {
            Terminal.Shell?.AddCommand(Name, (args) => Execute(args), 1, 1, ToolTip);
        }

    }

    [Serializable]
    public sealed class ShellCommand : ShellCommandBase
    {

        [SerializeField]
        private UnityEvent commandEvent;
        protected override void Execute(CommandArg[] args)
        {
            commandEvent.Invoke();
        }

        public override void RegisterCommand()
        {
                Terminal.Shell?.AddCommand(Name, (args) => Execute(args), 0, 0, ToolTip);
        }
    }

    [Serializable]
    public sealed class ShellBoolCommand : ShellCommand<bool>
    {
        protected override void Execute(CommandArg[] args)
        {
            if (args.Length < 1)
            {
                Terminal.Log(TerminalLogType.Error, "Expected an argument of type bool");
                return;
            }
             
            commandEvent.Invoke(args[0].Bool);
        }
    }

    [Serializable]
    public sealed class ShellStringCommand : ShellCommand<string>
    {
        protected override void Execute(CommandArg[] args)
        {
            if (args.Length < 1)
            {
                Terminal.Log(TerminalLogType.Error, "Expected an argument of type string");
                return;
            }
            commandEvent.Invoke(args[0].String);
        }
    }

    [Serializable]
    public sealed class ShellFloatCommand : ShellCommand<float>
    {
        protected override void Execute(CommandArg[] args)
        {
            if (args.Length < 1)
            {
                Terminal.Log(TerminalLogType.Error, "Expected an argument of type float");
                return;
            }
            commandEvent.Invoke(args[0].Float);
        }
    }

    [Serializable]
    public sealed class ShellIntCommand : ShellCommand<int>
    {
        protected override void Execute(CommandArg[] args)
        {
            if (args.Length < 1)
            {
                Terminal.Log(TerminalLogType.Error, "Expected an argument of type int");
                return;
            }
            commandEvent.Invoke(args[0].Int);
        }
    }


    [AddComponentMenu("Grasp/Debug/Shell Commands")]
    public class ShellCommandComponent : MonoBehaviour
    {
        [SerializeField]
        private List<CommandCollectionBase> commandCollections;

        [SerializeField]
        private List<ShellCommand> commands = new List<ShellCommand>();

        [SerializeField]
        private List<ShellBoolCommand> boolCommands = new List<ShellBoolCommand>();

        [SerializeField]
        private List<ShellStringCommand> stringCommands = new List<ShellStringCommand>();

        [SerializeField]
        private List<ShellFloatCommand> floatCommands = new List<ShellFloatCommand>();

        [SerializeField]
        private List<ShellIntCommand> intCommands = new List<ShellIntCommand>();

        private void Start()
        {
            if (Terminal.Shell == null)
                return;

            foreach (var collection in commandCollections)
                collection.RegisterCommands();

            foreach (var cmd in commands)
                cmd.RegisterCommand();

            foreach (var cmd in boolCommands)
                cmd.RegisterCommand();

            foreach (var cmd in stringCommands)
                cmd.RegisterCommand();

            foreach (var cmd in floatCommands)
                cmd.RegisterCommand();

            foreach (var cmd in intCommands)
                cmd.RegisterCommand();

        }
    }
}
