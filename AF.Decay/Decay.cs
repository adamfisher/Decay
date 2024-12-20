using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace AF.Decay
{
    /// <summary>
    /// Decaying objects eventually lose their value based on lifetime conditions.
    /// </summary>
    /// <typeparam name="T">The type of decaying object.</typeparam>
    [DebuggerStepThrough]
    public class Decay<T> : IEquatable<Decay<T>>, IComparable<Decay<T>>, IComparable
    {
        #region Fields & Properties

        internal T _value;

        internal long _count;
        internal long? _expireAfterCount;
        internal DateTimeOffset? _expirationDateTime;

        internal readonly Func<DateTimeOffset?, long?, bool> _expireOnCondition;

        internal readonly bool _throwExceptionOnExpiration;

        /// <summary>
        /// Gets the decaying value of the current <see cref="T:AF.Decay.Decay`1" /> instance.
        /// </summary>
        /// <exception cref="ObjectExpiredException">Only thrown if the decay object was set to throw an exception when attempting to access the value.</exception>
        public T Value
        {
            get
            {
                if (_expireAfterCount.HasValue)
                {
                    Interlocked.Increment(ref _count);
                }

                if (Expired)
                {
                    if (_throwExceptionOnExpiration)
                    {
                        ThrowObjectDecayedException();
                    }

                    _value = default;
                    _expireAfterCount = null;
                }

                return _value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance has expired.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance expired; otherwise, <c>false</c>.
        /// </value>
        public bool Expired => EqualityComparer<T>.Default.Equals(_value, default) || CounterExpired || TimeExpired || ConditionExpired;

        private bool CounterExpired => _expireAfterCount.HasValue && Interlocked.Read(ref _count) > _expireAfterCount;

        private bool TimeExpired => _expirationDateTime.HasValue && DateTimeOffset.UtcNow > _expirationDateTime;

        private bool ConditionExpired => _expireOnCondition != null && _expireOnCondition(_expirationDateTime, _count);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Decay{T}"/> class.
        /// Unspecified optional conditions will not count toward value expiration. The first condition to be true will expire the value. If no conditions are specified, the default behavior will be to never expire the value which means you probably shouldn't use this Decay<> wrapper if that's the case.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="expireAfterCount">The number of times this value may be accessed before it expires. A minimum value of 1 will be enforced if this value is set.</param>
        /// <param name="expirationDateTime">The period of time this value may be accessed before it expires on the expiration date.</param>
        /// <param name="expireOnCondition">Specify a custom function to determine when the object should expire.</param>*
        /// <param name="throwExceptionOnExpiration">if set to <c>true</c> [throw exception on expiration]. Default is false.</param>
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
            _expireAfterCount = expireAfterCount.HasValue ? Math.Max(1L, expireAfterCount.Value) : null;
            _expirationDateTime = expirationDateTime;
            _expireOnCondition = expireOnCondition;
            _throwExceptionOnExpiration = throwExceptionOnExpiration;
        }

        /// <summary>
        /// Creates a new Decay instance based on the expiration time.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="expirationDateTime">The period of time this value may be accessed before it expires on the expiration date.</param>
        /// <param name="throwExceptionOnExpiration">if set to <c>true</c> [throw exception on expiration]. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <returns>The new decaying object.</returns>
        public static Decay<T> OnExpiration(T value, DateTimeOffset expirationDateTime, bool throwExceptionOnExpiration = false)
        {
            return new Decay<T>(value, expirationDateTime: expirationDateTime, throwExceptionOnExpiration: throwExceptionOnExpiration);
        }

        /// <summary>
        /// Creates a new Decay instance based on the expiration time.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="expireAfterTime">The period of time this value may be accessed from the current time before it expires.</param>
        /// <param name="throwExceptionOnExpiration">if set to <c>true</c> [throw exception on expiration]. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <returns>The new decaying object.</returns>
        public static Decay<T> OnExpiration(T value, TimeSpan expireAfterTime, bool throwExceptionOnExpiration = false)
        {
            var expiration = DateTime.UtcNow.Add(expireAfterTime);
            return new Decay<T>(value, expirationDateTime: expiration, throwExceptionOnExpiration: throwExceptionOnExpiration);
        }

        /// <summary>
        /// Creates a new Decay instance based on the expiration time.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="count">The number of times this value may be accessed before it expires. A minimum value of 1 will be enforced if this value is set.</param>
        /// <param name="throwExceptionOnExpiration">if set to <c>true</c> [throw exception on expiration]. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <returns>The new decaying object.</returns>
        public static Decay<T> OnCount(T value, long count, bool throwExceptionOnExpiration = false)
        {
            return new Decay<T>(value, expireAfterCount: count, throwExceptionOnExpiration: throwExceptionOnExpiration);
        }

        /// <summary>
        /// Creates a new Decay instance based on the expiration time.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="condition">Specify a custom function to determine when the object should expire.</param>*
        /// <param name="throwExceptionOnExpiration">if set to <c>true</c> [throw exception on expiration]. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        /// <returns>The new decaying object.</returns>
        public static Decay<T> OnCondition(T value, Func<DateTimeOffset?, long?, bool> condition, bool throwExceptionOnExpiration = false)
        {
            return new Decay<T>(value, expireOnCondition: condition, throwExceptionOnExpiration: throwExceptionOnExpiration);
        }

        #endregion

        #region Methods
        
        private void ThrowObjectDecayedException()
        {
            var exception = new ObjectExpiredException(
                $"{_value} of type {_value.GetType().Name} has decayed due to one or more expiration conditions.")
            {
                Data =
                {
                    [nameof(CounterExpired)] = CounterExpired,
                    [nameof(TimeExpired)] = TimeExpired,
                    [nameof(ConditionExpired)] = ConditionExpired,
                    ["AccessCount"] = Interlocked.Read(ref _count),
                    ["AccessCountLimit"] = _expireAfterCount
                }
            };

            throw exception;
        }

        /// <summary>
        /// Sorts the Decay objects by expiration conditions with expired items first followed by active items with the longest expiration last.
        /// </summary>
        public int CompareTo(Decay<T> other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;

            // Expired items come first
            if (this.Expired != other.Expired)
            {
                return this.Expired ? -1 : 1;
            }

            // Calculate "time to expiration" for both time and count conditions
            var now = DateTimeOffset.UtcNow;

            // Time until datetime expiration
            var thisTimeRemaining = _expirationDateTime.HasValue
                ? (_expirationDateTime.Value - now).TotalSeconds
                : double.MaxValue;
            var otherTimeRemaining = other._expirationDateTime.HasValue
                ? (other._expirationDateTime.Value - now).TotalSeconds
                : double.MaxValue;

            // Time until count expiration (estimated based on current count vs limit)
            var thisCountRemaining = _expireAfterCount.HasValue
                ? _expireAfterCount.Value - Interlocked.Read(ref _count)
                : long.MaxValue;
            var otherCountRemaining = other._expireAfterCount.HasValue
                ? other._expireAfterCount.Value - Interlocked.Read(ref other._count)
                : long.MaxValue;

            // Take the minimum remaining time/count for each item
            var thisMinRemaining = Math.Min(thisTimeRemaining, thisCountRemaining);
            var otherMinRemaining = Math.Min(otherTimeRemaining, otherCountRemaining);

            var remainingComparison = thisMinRemaining.CompareTo(otherMinRemaining);
            if (remainingComparison != 0) return remainingComparison;

            // Use throwExceptionOnExpiration as final tiebreaker
            return _throwExceptionOnExpiration.CompareTo(other._throwExceptionOnExpiration);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            return obj is Decay<T> other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Decay<T>)}");
        }

        public bool Equals(Decay<T> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<T>.Default.Equals(_value, other._value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Decay<T>) obj);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<T>.Default.GetHashCode(_value);
        }

        public static bool operator ==(Decay<T> left, Decay<T> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Decay<T> left, Decay<T> right)
        {
            return !Equals(left, right);
        }

        public override string ToString() => $"Decay<{typeof(T).Name}>[{(Expired ? "Expired" : "Active")}]";

        #endregion
    }
}
