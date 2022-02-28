using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AF.Decay
{
    /// <summary>
    /// Provides support for decaying objects that eventually lose their value.
    /// </summary>
    /// <typeparam name="T">The type of object that is being decayed.</typeparam>
    [DebuggerStepThrough]
    public class Decay<T> : IEquatable<Decay<T>>
    {
        #region Fields & Properties

        private T _value;

        private long _count;
        private readonly long? _expireAfterCount;

        private readonly TimeSpan? _expireAfterTime;
        private readonly DateTimeOffset _startTime;
        
        private readonly Func<DateTimeOffset, long?, TimeSpan?, bool> _expireOnCondition;
        
        private readonly bool _throwExceptionOnExpiration;

        /// <summary>
        /// Gets the decaying value of the current <see cref="T:AF.Decay.Decay`1" /> instance.
        /// </summary>
        /// <exception cref="ObjectDecayedException">Only thrown if the decay object was set to throw an exception when attempting to access the value.</exception>
        public T Value
        {
            get
            {

                _count++;

                if (IsValueExpired)
                {
                    if (_throwExceptionOnExpiration)
                    {
                        var exception = new ObjectDecayedException($"{_value} of type {_value.GetType().Name} has decayed due to one or more expiration conditions.");
                        exception.Data[nameof(CounterExpired)] = CounterExpired;
                        exception.Data[nameof(TimeExpired)] = TimeExpired;
                        exception.Data[nameof(ConditionExpired)] = ConditionExpired;
                        exception.Data["AccessCount"] = _count;
                        exception.Data["ExpireAfterCountLimit"] = _expireAfterCount;
                        _value = default;
                        throw exception;
                    }
                    _value = default;
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
        public bool IsValueExpired => CounterExpired || TimeExpired || ConditionExpired;

        private bool CounterExpired => _expireAfterCount.HasValue && _count > _expireAfterCount;

        private bool TimeExpired => _expireAfterTime.HasValue && DateTimeOffset.UtcNow > _startTime.Add(_expireAfterTime.Value);

        private bool ConditionExpired => _expireOnCondition != null && _expireOnCondition(_startTime, _count, _expireAfterTime);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Decay{T}"/> class.
        /// Unspecified optional conditions will not count toward value expiration. The first condition to be true will expire the value. If no conditions are specified, the default behavior will be to never expire the value which means you probably shouldn't use this Decay<> wrapper if that's the case.
        /// </summary>
        /// <param name="value">The value to decay.</param>
        /// <param name="expireAfterCount">The number of times this value may be accessed before it expires.</param>
        /// <param name="expireAfterTime">The period of time (starting at the time the Decay object is constructed) this value may be accessed before it expires.</param>
        /// <param name="expireOnCondition">Specify a custom function to determine when the object should expire.</param>
        /// <param name="throwExceptionOnExpiration">if set to <c>true</c> [throw exception on expiration]. Default is false.</param>
        /// <exception cref="System.ArgumentNullException">value</exception>
        public Decay(
            T value,
            long? expireAfterCount = null,
            TimeSpan? expireAfterTime = null,
            Func<DateTimeOffset, long?, TimeSpan?, bool> expireOnCondition = null,
            bool throwExceptionOnExpiration = false)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            _value = value;
            _expireAfterCount = expireAfterCount;
            _expireAfterTime = expireAfterTime;
            _expireOnCondition = expireOnCondition;
            _throwExceptionOnExpiration = throwExceptionOnExpiration;
            _startTime = DateTimeOffset.UtcNow;
        }

        #endregion

        #region Methods

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
            if (obj.GetType() != this.GetType()) return false;
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

        /// <summary>
        /// Converts to string.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString() => $"Decay<{typeof(T).Name}>[Expired: {IsValueExpired}]";

        #endregion
    }
}
