using System;

namespace AF.Decay
{
    public class ObjectExpiredException : Exception
    {
        public ObjectExpiredException(string message) : base(message)
        {
        }
    }
}