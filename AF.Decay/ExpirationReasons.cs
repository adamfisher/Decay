using System;

namespace AF.Decay
{
    /// <summary>
    /// The policies that caused a <see cref="Decay{T}"/> to expire.
    /// </summary>
    [Flags]
    internal enum ExpirationReasons
    {
        None = 0,
        Count = 1,
        Time = 2,
        Condition = 4
    }
}
