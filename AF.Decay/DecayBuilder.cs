using System;

namespace AF.Decay
{
    /// <summary>
    /// Fluent builder for <see cref="Decay{T}"/>. Create one with <see cref="Decay.Of{T}(T)"/>.
    /// The first policy to be met expires the value; policies that are not configured never apply.
    /// </summary>
    /// <typeparam name="T">The type of decaying object.</typeparam>
    public sealed class DecayBuilder<T>
    {
        private readonly T _value;
        private long? _expireAfterCount;
        private DateTimeOffset? _expiresAt;
        private TimeSpan? _expiresAfter;
        private Func<DecayState<T>, bool>? _condition;
        private bool _throwOnExpiration;
        private TimeProvider _timeProvider = TimeProvider.System;

        internal DecayBuilder(T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            _value = value;
        }

        /// <summary>
        /// Expire after the value has been accessed <paramref name="count"/> times. A minimum of 1 is enforced.
        /// </summary>
        public DecayBuilder<T> ExpireAfterAccesses(long count)
        {
            _expireAfterCount = count;
            return this;
        }

        /// <summary>
        /// Expire at a point in time. Replaces any previous <see cref="ExpireAfter"/>.
        /// </summary>
        public DecayBuilder<T> ExpireAt(DateTimeOffset expiresAt)
        {
            _expiresAt = expiresAt;
            _expiresAfter = null;
            return this;
        }

        /// <summary>
        /// Expire after a period of time, measured from when <see cref="Build"/> is called. Replaces any previous <see cref="ExpireAt"/>.
        /// </summary>
        public DecayBuilder<T> ExpireAfter(TimeSpan duration)
        {
            _expiresAfter = duration;
            _expiresAt = null;
            return this;
        }

        /// <summary>
        /// Expire when <paramref name="condition"/> returns <c>true</c>. The condition runs on each access and each read of
        /// <see cref="Decay{T}.Expired"/> until the value expires, so it should be fast and free of side effects.
        /// </summary>
        public DecayBuilder<T> ExpireWhen(Func<DecayState<T>, bool> condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            return this;
        }

        /// <summary>
        /// Throw <see cref="ObjectExpiredException"/> when <see cref="Decay{T}.Value"/> is read after expiration, instead of returning <c>default(T)</c>.
        /// </summary>
        public DecayBuilder<T> ThrowOnExpiration(bool throwOnExpiration = true)
        {
            _throwOnExpiration = throwOnExpiration;
            return this;
        }

        /// <summary>
        /// Use a custom clock, e.g. <c>FakeTimeProvider</c> in tests. Defaults to <see cref="TimeProvider.System"/>.
        /// </summary>
        public DecayBuilder<T> UseTimeProvider(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            return this;
        }

        /// <summary>
        /// Creates the <see cref="Decay{T}"/>.
        /// </summary>
        public Decay<T> Build()
        {
            var expiresAt = _expiresAfter.HasValue ? _timeProvider.GetUtcNow().Add(_expiresAfter.Value) : _expiresAt;
            return new Decay<T>(_value, _expireAfterCount, expiresAt, _condition, _throwOnExpiration, _timeProvider);
        }

        /// <summary>
        /// Builds the <see cref="Decay{T}"/>, so a builder can be assigned directly to a <see cref="Decay{T}"/> variable.
        /// </summary>
        public static implicit operator Decay<T>(DecayBuilder<T> builder) => builder.Build();
    }
}
