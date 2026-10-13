using System;

namespace AF.Decay
{
    /// <summary>
    /// Thrown when accessing the value of a <see cref="Decay{T}"/> that has expired and was created to throw on expiration.
    /// </summary>
    public class ObjectExpiredException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="ObjectExpiredException"/> class.</summary>
        public ObjectExpiredException()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ObjectExpiredException"/> class with a message.</summary>
        public ObjectExpiredException(string message) : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ObjectExpiredException"/> class with a message and inner exception.</summary>
        public ObjectExpiredException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
