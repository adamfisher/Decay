using System;
using System.Diagnostics;

namespace AF.Decay
{
    /// <summary>
    /// Decaying objects eventually lose their value based on lifetime conditions.
    /// </summary>
    /// <remarks>
    /// Instances are thread-safe. Once an instance expires it stays expired, and the wrapped value is released.
    /// Equality is by reference: two <see cref="Decay{T}"/> instances wrapping equal values are distinct objects.
    /// </remarks>
    /// <typeparam name="T">The type of decaying object.</typeparam>
    [DebuggerStepThrough]
    [DebuggerDisplay("{ToString(),nq}")]
    public class Decay<T> : IComparable<Decay<T>>, IComparable
    {
        #region Fields & Properties

        private readonly object _sync = new object();

        private T _value;
        private bool _expired;

        private long _count;
        private readonly long? _expireAfterCount;
        private readonly DateTimeOffset? _expirationDateTime;

        private readonly Func<DateTimeOffset?, long?, bool> _expireOnCondition;

        private readonly bool _throwExceptionOnExpiration;

        /// <summary>
        /// Gets the decaying value of the current <see cref="Decay{T}" /> instance.
        /// Each read counts as one access toward an access-count policy.
        /// Returns <c>default(T)</c> once expired, unless the instance was created to throw on expiration.
        /// </summary>
        /// <exception cref="ObjectExpiredException">Only thrown if the decay object was set to throw an exception when attempting to access the value.</exception>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] // Evaluating in a debugger would consume an access.
        public T Value
        {
            get
            {
                lock (_sync)
                {
                    if (!_expired && _expireAfterCount.HasValue)
                    {
                        _count++;
                    }

                    if (EvaluateExpired(out var reasons))
                    {
                        if (_throwExceptionOnExpiration)
                        {
                            throw CreateExpiredException(reasons);
                        }

                        return default;
                    }

                    return _value;
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
                    return EvaluateExpired(out _);
                }
            }
        }

        private bool CounterExpired => _expireAfterCount.HasValue && _count > _expireAfterCount.Value;

        private bool TimeExpired => _expirationDateTime.HasValue && DateTimeOffset.UtcNow > _expirationDateTime.Value;

        private bool ConditionExpired => _expireOnCondition != null && _expireOnCondition(_expirationDateTime, _count);

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
        /// <param name="expireOnCondition">Specify a custom function to determine when the object should expire.</param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing <see cref="Value"/> after expiration throws <see cref="ObjectExpiredException"/>. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        public Decay(
            T value,
            long? expireAfterCount = null,
            DateTimeOffset? expirationDateTime = null,
            Func<DateTimeOffset?, long?, bool> expireOnCondition = null,
            bool throwExceptionOnExpiration = false)
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
        {
            return new Decay<T>(value, expirationDateTime: expirationDateTime, throwExceptionOnExpiration: throwExceptionOnExpiration);
        }

        /// <summary>
        /// Creates a new Decay instance that expires after a period of time from now.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="expireAfterTime">The period of time this value may be accessed from the current time before it expires.</param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing <see cref="Value"/> after expiration throws <see cref="ObjectExpiredException"/>. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <returns>The new decaying object.</returns>
        public static Decay<T> OnExpiration(T value, TimeSpan expireAfterTime, bool throwExceptionOnExpiration = false)
        {
            var expiration = DateTimeOffset.UtcNow.Add(expireAfterTime);
            return new Decay<T>(value, expirationDateTime: expiration, throwExceptionOnExpiration: throwExceptionOnExpiration);
        }

        /// <summary>
        /// Creates a new Decay instance that expires after being accessed a number of times.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="count">The number of times this value may be accessed before it expires. A minimum value of 1 will be enforced if this value is set.</param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing <see cref="Value"/> after expiration throws <see cref="ObjectExpiredException"/>. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <returns>The new decaying object.</returns>
        public static Decay<T> OnCount(T value, long count, bool throwExceptionOnExpiration = false)
        {
            return new Decay<T>(value, expireAfterCount: count, throwExceptionOnExpiration: throwExceptionOnExpiration);
        }

        /// <summary>
        /// Creates a new Decay instance that expires when a custom condition returns <c>true</c>.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="condition">Specify a custom function to determine when the object should expire.</param>
        /// <param name="throwExceptionOnExpiration">If <c>true</c>, accessing <see cref="Value"/> after expiration throws <see cref="ObjectExpiredException"/>. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <returns>The new decaying object.</returns>
        public static Decay<T> OnCondition(T value, Func<DateTimeOffset?, long?, bool> condition, bool throwExceptionOnExpiration = false)
        {
            return new Decay<T>(value, expireOnCondition: condition, throwExceptionOnExpiration: throwExceptionOnExpiration);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Checks every policy and latches the expired state the first time one is met. Caller must hold <see cref="_sync"/>.
        /// </summary>
        private bool EvaluateExpired(out ExpirationReasons reasons)
        {
            reasons = ExpirationReasons.None;

            if (_expired)
            {
                return true;
            }

            if (CounterExpired) reasons |= ExpirationReasons.Count;
            if (TimeExpired) reasons |= ExpirationReasons.Time;
            if (ConditionExpired) reasons |= ExpirationReasons.Condition;

            if (reasons == ExpirationReasons.None)
            {
                return false;
            }

            _expired = true;
            _value = default; // Release the value so it can be collected.
            return true;
        }

        private ObjectExpiredException CreateExpiredException(ExpirationReasons reasons)
        {
            // Never include the value itself: it may be a secret.
            return new ObjectExpiredException(
                $"Decay<{typeof(T).Name}> has decayed due to one or more expiration conditions.")
            {
                Data =
                {
                    [nameof(CounterExpired)] = (reasons & ExpirationReasons.Count) != 0,
                    [nameof(TimeExpired)] = (reasons & ExpirationReasons.Time) != 0,
                    [nameof(ConditionExpired)] = (reasons & ExpirationReasons.Condition) != 0,
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
        /// then by expiration time (earliest first, none last). Access counts and times are never compared against each other. Custom conditions cannot be predicted and do not affect ordering.
        /// The comparison does not evaluate expiration policies, so it stays consistent while a sort is in progress.
        /// </remarks>
        public int CompareTo(Decay<T> other)
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
                var expired = _expired || CounterExpired;
                var expiration = _expirationDateTime ?? DateTimeOffset.MaxValue;
                var remaining = _expireAfterCount.HasValue ? _expireAfterCount.Value - _count : long.MaxValue;
                return (expired, expiration, remaining);
            }
        }

        /// <inheritdoc cref="CompareTo(Decay{T})"/>
        public int CompareTo(object obj)
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
