using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LIV.SDK.Unity
{
    public struct LivResult<T, E>
    {
        private readonly bool _success;
        public readonly T result;
        public readonly E error;
        public string message;

        private LivResult(T result, E error, string message, bool success)
        {
            this.result = result;
            this.error = error;
            this.message = message;
            _success = success;
        }

        public static LivResult<T, E> Ok(T result)
        {
            return new LivResult<T, E>(result, default(E), null, true);
        }

        public static LivResult<T, E> Error(E error, string message)
        {
            return new LivResult<T, E>(default(T), error, message, false);
        }

        public bool isOk
        {
            get
            {
                return _success;
            }
        }
    }
}