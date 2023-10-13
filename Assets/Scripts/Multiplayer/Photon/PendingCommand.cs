
using System;
using System.Collections.Generic;
using TiltBrush;

namespace OpenBrush.Multiplayer
{
    public struct PendingCommand
    {
        public int TotalExpectedChildren;

        public Guid Guid;
        public BaseCommand Command;
        public Action PreCommandAction;
        public List<PendingCommand> ChildCommands;

        public PendingCommand(Guid guid, BaseCommand command, Action action, int count)
        {
            Guid = guid;
            Command = command;
            PreCommandAction = action;
            TotalExpectedChildren = count;
            ChildCommands = new List<PendingCommand>();
        }
    }
}