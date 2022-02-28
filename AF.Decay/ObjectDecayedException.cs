using System;

namespace AF.Decay
{
    public class ObjectDecayedException : Exception
    {
        public ObjectDecayedException(string message) : base(message)
        {
        }
    }
}