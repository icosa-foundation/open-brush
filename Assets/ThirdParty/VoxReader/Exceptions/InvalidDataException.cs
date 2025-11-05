using System;
using System.Runtime.Serialization;

namespace VoxReader.Exceptions
{
    [Serializable]
    internal class InvalidDataException : Exception
    {
        public InvalidDataException()
        {
        }

        public InvalidDataException(string message) : base(message)
        {
        }

        public InvalidDataException(string message, Exception inner) : base(message, inner)
        {
        }

        protected InvalidDataException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}