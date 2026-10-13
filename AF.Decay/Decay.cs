using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AF.Decay
{
    /// <summary>
    /// Decaying objects eventually lose their value based on lifetime conditions.
    /// </summary>
    /// <remarks>
    /// Instances are thread-safe. Once an instance expires it stays expired, and the wrapped value is released.
    /// Equality is by reference: two <see cref="Decay{T}"/> instances wrapping equal values are distinct objects.
    /// Use <see cref="Decay"/> to create instances with type inference.
    /// </remarks>
    /// <typeparam name="T">The type of decaying object.</typeparam>
    [DebuggerStepThrough]
    [DebuggerDisplay("{ToString(),nq}")]
    public class Decay<T> : IComparable<Decay<T>>, IComparable
    {
        #region Fields & Properties

        private readonly object _sync = new object();

        private T? _value;
        private ExpirationReasons _expirationReason;

        private long _count;
        private readonly long? _expireAfterCount;
        private readonly DateTimeOffset? _expirationDateTime;

        private readonly Func<DecayState<T>, bool>? _expireOnCondition;

        private readonly bool _throwExceptionOnExpiration;

        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Gets the decaying value of the current <see cref="Decay{T}" /> instance.
        /// Each read counts as one access toward an access-count policy.
        /// Returns <c>default(T)</c> once expired, unless the instance was created to throw on expiration.
        /// Prefer <see cref="TryGetValue"/> when <c>default(T)</c> is a meaningful value.
        /// </summary>
        /// <exception cref="ObjectExpiredException">Only thrown if the decay object was set to throw an exception when attempting to access the value.</exception>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] // Evaluating in a debugger would consume an access.
        public T? Value
        {
            get
            {
                lock (_sync)
                {
                    if (Access())
                    {
                        return _value;
                    }

                    if (_throwExceptionOnExpiration)
                    {
                        throw CreateExpiredException();
                    }

                    return default;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance has expired. Reading this property does not count as an access.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance expired; otherwise, <c>false</c>.
        /// </value>
        public bool Expired
        {
            get
            {
                lock (_sync)
                {
                    return EvaluateExpired();
                }
            }
        }

        /// <summary>
        /// Gets the policies that caused this instance to expire, or <see cref="ExpirationReasons.None"/> if it has not expired.
        /// Reading this property does not count as an access.
        /// </summary>
        public ExpirationReasons ExpirationReason
        {
            get
            {
                lock (_sync)
                {
                    EvaluateExpired();
                    return _expirationReason;
                }
            }
        }

        /// <summary>
        /// Gets the number of times the value has been accessed.
        /// </summary>
        public long AccessCount
        {
            get
            {
                lock (_sync)
                {
                    return _count;
                }
            }
        }

        /// <summary>
        /// Gets the configured access limit, or <c>null</c> if there is none.
        /// </summary>
        public long? AccessLimit => _expireAfterCount;

        /// <summary>
        /// Gets the number of accesses left before the value expires, or <c>null</c> if there is no access limit.
        /// </summary>
        public long? RemainingAccesses
        {
            get
            {
                lock (_sync)
                {
                    return _expireAfterCount.HasValue ? Math.Max(0L, _expireAfterCount.Value - _count) : (long?)null;
                }
            }
        }

        /// <summary>
        /// Gets the point in time after which the value expires, or <c>null</c> if there is no time limit.
        /// </summary>
        public DateTimeOffset? ExpiresAt => _expirationDateTime;

        private bool CounterExpired => _expireAfterCount.HasValue && _count > _expireAfterCount.Value;

        private bool TimeExpired => _expirationDateTime.HasValue && _timeProvider.GetUtcNow() > _expirationDateTime.Value;

        private bool ConditionExpired => _expireOnCondition != null && _expireOnCondition(
            new DecayState<T>(_value!, _count, _expireAfterCount, _expirationDateTime, _timeProvider.GetUtcNow()));

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Decay{T}"/> class.
        /// Unspecified optional conditions will not count toward value expiration. The first condition to be true will expire the value.
        /// If no conditions are specified, the value never expires.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="expireAfterCount">The number of times this value may be accessed before it expires. A minimum value of 1 will be enforced if this value is set.</param>
        /// <param name="expirationDateTime">The point in time after which this value expires.</param>
        /// <param name="expireOnCondition">
        /// Specify a custom function to determine when the object should expire. It receives the expiration time and the access count.
        /// Use <see cref="Decay.Of{T}(T)"/> with <see cref="DecayBuilder{T}.ExpireWhen"/> for a condition that can also see the value.
        /// </param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing <see cref="Value"/> after expiration throws <see cref="ObjectExpiredException"/>. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        public Decay(
            T value,
            long? expireAfterCount = null,
            DateTimeOffset? expirationDateTime = null,
            Func<DateTimeOffset?, long?, bool>? expireOnCondition = null,
            bool throwExceptionOnExpiration = false)
            : this(
                value,
                expireAfterCount,
                expirationDateTime,
                expireOnCondition == null ? null : state => expireOnCondition(state.ExpiresAt, state.AccessCount),
                throwExceptionOnExpiration,
                TimeProvider.System)
        {
        }

        internal Decay(
            T value,
            long? expireAfterCount,
            DateTimeOffset? expirationDateTime,
            Func<DecayState<T>, bool>? expireOnCondition,
            bool throwExceptionOnExpiration,
            TimeProvider timeProvider)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            _value = value;
            _expireAfterCount = expireAfterCount.HasValue ? Math.Max(1L, expireAfterCount.Value) : (long?)null;
            _expirationDateTime = expirationDateTime;
            _expireOnCondition = expireOnCondition;
            _throwExceptionOnExpiration = throwExceptionOnExpiration;
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <summary>
        /// Creates a new Decay instance that expires at a point in time.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="expirationDateTime">The point in time after which this value expires.</param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing <see cref="Value"/> after expiration throws <see cref="ObjectExpiredException"/>. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <returns>The new decaying object.</returns>
        public static Decay<T> OnExpiration(T value, DateTimeOffset expirationDateTime, bool throwExceptionOnExpiration = false)
            => Decay.OnExpiration(value, expirationDateTime, throwExceptionOnExpiration);

        /// <summary>
        /// Creates a new Decay instance that expires after a period of time from now.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="expireAfterTime">The period of time this value may be accessed from the current time before it expires.</param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing <see cref="Value"/> after expiration throws <see cref="ObjectExpiredException"/>. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <returns>The new decaying object.</returns>
        public static Decay<T> OnExpiration(T value, TimeSpan expireAfterTime, bool throwExceptionOnExpiration = false)
            => Decay.OnExpiration(value, expireAfterTime, throwExceptionOnExpiration);

        /// <summary>
        /// Creates a new Decay instance that expires after being accessed a number of times.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="count">The number of times this value may be accessed before it expires. A minimum value of 1 will be enforced if this value is set.</param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing <see cref="Value"/> after expiration throws <see cref="ObjectExpiredException"/>. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <returns>The new decaying object.</returns>
        public static Decay<T> OnCount(T value, long count, bool throwExceptionOnExpiration = false)
            => Decay.OnCount(value, count, throwExceptionOnExpiration);

        /// <summary>
        /// Creates a new Decay instance that expires when a custom condition returns <c>true</c>.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="condition">Specify a custom function to determine when the object should expire.</param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing <see cref="Value"/> after expiration throws <see cref="ObjectExpiredException"/>. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <returns>The new decaying object.</returns>
        [Obsolete("Use Decay.OnCondition(value, state => ...), whose condition can also see the value.")]
        public static Decay<T> OnCondition(T value, Func<DateTimeOffset?, long?, bool> condition, bool throwExceptionOnExpiration = false)
        {
            return new Decay<T>(value, expireOnCondition: condition, throwExceptionOnExpiration: throwExceptionOnExpiration);
        }

        /// <summary>
        /// Creates a new Decay instance that expires when a custom condition returns <c>true</c>.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="condition">A function that receives the current <see cref="DecayState{T}"/> and returns <c>true</c> when the value should expire.</param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing <see cref="Value"/> after expiration throws <see cref="ObjectExpiredException"/>. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <returns>The new decaying object.</returns>
        public static Decay<T> OnCondition(T value, Func<DecayState<T>, bool> condition, bool throwExceptionOnExpiration = false)
            => Decay.OnCondition(value, condition, throwExceptionOnExpiration);

        #endregion

        #region Methods

        /// <summary>
        /// Attempts to get the value. Counts as one access, and never throws on expiration.
        /// </summary>
        /// <param name="value">The value, or <c>default(T)</c> if the instance has expired.</param>
        /// <returns><c>true</c> if the value is still alive; otherwise, <c>false</c>.</returns>
        public bool TryGetValue([MaybeNullWhen(false)] out T value)
        {
            lock (_sync)
            {
                if (Access())
                {
                    value = _value!;
                    return true;
                }

                value = default;
                return false;
            }
        }

        /// <summary>
        /// Gets the value, or <paramref name="defaultValue"/> if the instance has expired. Counts as one access, and never throws on expiration.
        /// </summary>
        /// <param name="defaultValue">The value to return if the instance has expired.</param>
        public T GetValueOrDefault(T defaultValue) => TryGetValue(out var value) ? value : defaultValue;

        /// <summary>
        /// Expires this instance immediately and releases the value.
        /// </summary>
        public void Expire()
        {
            lock (_sync)
            {
                if (_expirationReason == ExpirationReasons.None)
                {
                    Latch(ExpirationReasons.Manual);
                }
            }
        }

        /// <summary>
        /// Records one access and reports whether the value is still alive. Caller must hold <see cref="_sync"/>.
        /// </summary>
        private bool Access()
        {
            if (_expirationReason == ExpirationReasons.None)
            {
                _count++;
            }

            return !EvaluateExpired();
        }

        /// <summary>
        /// Checks every policy and latches the expired state the first time one is met. Caller must hold <see cref="_sync"/>.
        /// </summary>
        private bool EvaluateExpired()
        {
            if (_expirationReason != ExpirationReasons.None)
            {
                return true;
            }

            var reasons = ExpirationReasons.None;
            if (CounterExpired) reasons |= ExpirationReasons.Count;
            if (TimeExpired) reasons |= ExpirationReasons.Time;
            if (ConditionExpired) reasons |= ExpirationReasons.Condition;

            if (reasons == ExpirationReasons.None)
            {
                return false;
            }

            Latch(reasons);
            return true;
        }

        private void Latch(ExpirationReasons reasons)
        {
            _expirationReason = reasons;
            _value = default; // Release the value so it can be collected.
        }

        private ObjectExpiredException CreateExpiredException()
        {
            // Never include the value itself: it may be a secret.
            return new ObjectExpiredException(
                $"Decay<{typeof(T).Name}> has decayed ({_expirationReason}).", _expirationReason)
            {
                Data =
                {
                    [nameof(CounterExpired)] = (_expirationReason & ExpirationReasons.Count) != 0,
                    [nameof(TimeExpired)] = (_expirationReason & ExpirationReasons.Time) != 0,
                    [nameof(ConditionExpired)] = (_expirationReason & ExpirationReasons.Condition) != 0,
                    ["AccessCount"] = _count,
                    ["AccessCountLimit"] = _expireAfterCount
                }
            };
        }

        /// <summary>
        /// Sorts the Decay objects by expiration conditions with expired items first followed by active items with the longest expiration last.
        /// </summary>
        /// <remarks>
        /// Items are ordered by: already-expired first, then by remaining accesses (fewest first, unlimited last),
        /// then by expiration time (earliest first, none last). Access counts and times are never compared against each other.
        /// Custom conditions cannot be predicted and do not affect ordering.
        /// The comparison does not evaluate expiration policies, so it stays consistent while a sort is in progress.
        /// </remarks>
        public int CompareTo(Decay<T>? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;

            var (thisExpired, thisExpiration, thisRemaining) = GetSortKey();
            var (otherExpired, otherExpiration, otherRemaining) = other.GetSortKey();

            if (thisExpired != otherExpired)
            {
                return thisExpired ? -1 : 1;
            }

            var remainingComparison = thisRemaining.CompareTo(otherRemaining);
            if (remainingComparison != 0) return remainingComparison;

            return thisExpiration.CompareTo(otherExpiration);
        }

        private (bool Expired, DateTimeOffset Expiration, long RemainingAccesses) GetSortKey()
        {
            lock (_sync)
            {
                var expired = _expirationReason != ExpirationReasons.None || CounterExpired;
                var expiration = _expirationDateTime ?? DateTimeOffset.MaxValue;
                var remaining = _expireAfterCount.HasValue ? _expireAfterCount.Value - _count : long.MaxValue;
                return (expired, expiration, remaining);
            }
        }

        /// <inheritdoc cref="CompareTo(Decay{T})"/>
        public int CompareTo(object? obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            return obj is Decay<T> other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Decay<T>)}");
        }

        /// <inheritdoc />
        public override string ToString() => $"Decay<{typeof(T).Name}>[{(Expired ? "Expired" : "Active")}]";

        #endregion
    }
}
