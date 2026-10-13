using System;

namespace AF.Decay
{
    /// <summary>
    /// A snapshot of a <see cref="Decay{T}"/> passed to custom expiration conditions.
    /// </summary>
    /// <typeparam name="T">The type of decaying object.</typeparam>
    public readonly struct DecayState<T>
    {
        internal DecayState(T value, long accessCount, long? accessLimit, DateTimeOffset? expiresAt, DateTimeOffset now)
        {
            Value = value;
            AccessCount = accessCount;
            AccessLimit = accessLimit;
            ExpiresAt = expiresAt;
            Now = now;
        }

        /// <summary>The wrapped value.</summary>
        public T Value { get; }

        /// <summary>The number of times <see cref="Decay{T}.Value"/> (or a <c>TryGetValue</c>/<c>GetValueOrDefault</c> call) has been read, including the current one.</summary>
        public long AccessCount { get; }

        /// <summary>The configured access limit, or <c>null</c> if there is none.</summary>
        public long? AccessLimit { get; }

        /// <summary>The configured expiration time, or <c>null</c> if there is none.</summary>
        public DateTimeOffset? ExpiresAt { get; }

        /// <summary>The current time according to the instance's <see cref="TimeProvider"/>.</summary>
        public DateTimeOffset Now { get; }
    }
}
