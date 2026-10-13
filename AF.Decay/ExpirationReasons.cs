using System;

namespace AF.Decay
{
    /// <summary>
    /// The policies that caused a <see cref="Decay{T}"/> to expire.
    /// </summary>
    [Flags]
    public enum ExpirationReasons
    {
        /// <summary>The value has not expired.</summary>
        None = 0,

        /// <summary>The value was accessed more times than its access limit allows.</summary>
        Count = 1,

        /// <summary>The expiration time passed.</summary>
        Time = 2,

        /// <summary>The custom expiration condition returned <c>true</c>.</summary>
        Condition = 4,

        /// <summary>The value was expired explicitly by calling <see cref="Decay{T}.Expire"/>.</summary>
        Manual = 8
    }
}
