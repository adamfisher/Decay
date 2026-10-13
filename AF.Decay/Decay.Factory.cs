using System;

namespace AF.Decay
{
    /// <summary>
    /// Creates <see cref="Decay{T}"/> instances with type inference, e.g. <c>Decay.OnCount("token", 3)</c>.
    /// </summary>
    public static class Decay
    {
        /// <summary>
        /// Starts building a <see cref="Decay{T}"/> that can combine several expiration policies.
        /// </summary>
        /// <example>
        /// <code>
        /// var token = Decay.Of(accessToken)
        ///     .ExpireAfterAccesses(5)
        ///     .ExpireAfter(TimeSpan.FromMinutes(10))
        ///     .ThrowOnExpiration()
        ///     .Build();
        /// </code>
        /// </example>
        /// <param name="value">The value to decay.</param>
        public static DecayBuilder<T> Of<T>(T value) => new DecayBuilder<T>(value);

        /// <summary>
        /// Creates a <see cref="Decay{T}"/> that expires after being accessed <paramref name="count"/> times.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="count">The number of times this value may be accessed before it expires. A minimum value of 1 will be enforced.</param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing the value after expiration throws <see cref="ObjectExpiredException"/>.</param>
        public static Decay<T> OnCount<T>(T value, long count, bool throwExceptionOnExpiration = false)
            => Of(value).ExpireAfterAccesses(count).ThrowOnExpiration(throwExceptionOnExpiration).Build();

        /// <summary>
        /// Creates a <see cref="Decay{T}"/> that expires at <paramref name="expirationDateTime"/>.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="expirationDateTime">The point in time after which this value expires.</param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing the value after expiration throws <see cref="ObjectExpiredException"/>.</param>
        public static Decay<T> OnExpiration<T>(T value, DateTimeOffset expirationDateTime, bool throwExceptionOnExpiration = false)
            => Of(value).ExpireAt(expirationDateTime).ThrowOnExpiration(throwExceptionOnExpiration).Build();

        /// <summary>
        /// Creates a <see cref="Decay{T}"/> that expires <paramref name="expireAfterTime"/> from now.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="expireAfterTime">How long from now the value may be accessed before it expires.</param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing the value after expiration throws <see cref="ObjectExpiredException"/>.</param>
        public static Decay<T> OnExpiration<T>(T value, TimeSpan expireAfterTime, bool throwExceptionOnExpiration = false)
            => Of(value).ExpireAfter(expireAfterTime).ThrowOnExpiration(throwExceptionOnExpiration).Build();

        /// <summary>
        /// Creates a <see cref="Decay{T}"/> that expires when <paramref name="condition"/> returns <c>true</c>.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="condition">A function that receives the current <see cref="DecayState{T}"/> and returns <c>true</c> when the value should expire.</param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing the value after expiration throws <see cref="ObjectExpiredException"/>.</param>
        public static Decay<T> OnCondition<T>(T value, Func<DecayState<T>, bool> condition, bool throwExceptionOnExpiration = false)
            => Of(value).ExpireWhen(condition).ThrowOnExpiration(throwExceptionOnExpiration).Build();
    }
}
