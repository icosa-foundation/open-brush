using System.Collections.Generic;

namespace CommandTerminal
{
    public class CommandHistory
    {
        List<string> history = new List<string>();
        int position;

        public void Push(string command_string) {
            if (command_string == "") {
                return;
            }

            history.Add(command_string);
            position = history.Count;
        }

        public string Next() {
            position++;

            if (position >= history.Count) {
                position = history.Count;
                return "";
            }

            return history[position];
        }

        public string Previous() {
            if (history.Count == 0) {
                return "";
            }

            position--;

            if (position < 0) {
                position = 0;
            }

            return history[position];
        }

        public void Clear() {
            history.Clear();
            position = 0;
        }
    }
}
