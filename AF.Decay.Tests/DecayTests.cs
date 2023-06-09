using System;
using System.Collections.Generic;
using System.Threading;
using CategoryTraits.Xunit2;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AF.Decay.Tests
{
    public class DecayTests
    {
        [Fact]
        public void NoExpirationPolicy_DoesNotExpire()
        {
            var value = 4523;
            var decayingObject = new Decay<int>(value);

            decayingObject.Value.Should().Be(value);
            decayingObject.Expired.Should().BeFalse();
        }

        [Fact]
        public void ExpireOnCount_ExpiresAfter3()
        {
            var value = "SomeConfigValue";
            var decayingObject = new Decay<string>(value, expireAfterCount: 3);

            decayingObject.Value.Should().Be(value);
            decayingObject.Expired.Should().BeFalse();

            decayingObject.Value.Should().Be(value);
            decayingObject.Expired.Should().BeFalse();

            decayingObject.Value.Should().Be(value);
            decayingObject.Expired.Should().BeFalse();

            decayingObject.Value.Should().BeNull();
            decayingObject.Expired.Should().BeTrue();
        }

        [Fact]
        public void ExpireOnCount_ExpiresAfter1_WhenNegativeNumberIsSet()
        {
            var value = "SomeConfigValue";
            var decayingObject = new Decay<string>(value, expireAfterCount: -10);

            decayingObject.Value.Should().Be(value);
            decayingObject.Expired.Should().BeFalse();

            decayingObject.Value.Should().BeNull();
            decayingObject.Expired.Should().BeTrue();
        }

        [Fact]
        public void ExpireAfterTime_ExpiresIn3Seconds()
        {
            var value = "SomeConfigValue";
            var decayingObject = new Decay<string>(value, expireAfterTime: TimeSpan.FromMilliseconds(10));

            decayingObject.Value.Should().Be(value);
            decayingObject.Expired.Should().BeFalse();

            // Wait at least as long as the object's time horizon and we should be past it by now.
            Thread.Sleep(TimeSpan.FromMilliseconds(20));

            decayingObject.Value.Should().BeNull();
            decayingObject.Expired.Should().BeTrue();
        }

        [Fact]
        public void ExpireOnCondition_ExpiresWhenCustomConditionIsTrue()
        {
            var value = "SomeConfigValue";

            var decayingObject = new Decay<string>(value, expireAfterCount: 25, expireOnCondition: ((creationTime, currentAccessCount, expireAfterTime) =>
            {
                creationTime.Should().NotBeOnOrAfter(DateTimeOffset.UtcNow);
                currentAccessCount.Should().Be(1);
                expireAfterTime.HasValue.Should().BeFalse();
                return true;
            }));

            decayingObject.Value.Should().BeNull();
            decayingObject.Expired.Should().BeTrue();
        }

        [Fact]
        [UnitTest]
        public void CompareTo_SortsAscendingByNextToDecay_CountBased()
        {
            var list = new List<Decay<int>>()
            {
                new(1, expireAfterCount: 30),
                new(2, expireAfterCount: 20),
                new(3, expireAfterCount: 10)
            };

            list.Sort();

            list.Should().BeInDescendingOrder(p => p.Value)
                .And.AllSatisfy(d => d.Expired.Should().BeFalse());
        }

        [Fact]
        [UnitTest]
        public void CompareTo_SortsAscendingByNextToDecay_TimeBased()
        {
            var list = new List<Decay<int>>()
            {
                new(1, expireAfterTime: TimeSpan.FromMinutes(30)),
                new(2, expireAfterTime: TimeSpan.FromMinutes(20)),
                new(3, expireAfterTime: TimeSpan.FromMinutes(10))
            };

            list.Sort();

            list.Should().BeInDescendingOrder(p => p.Value)
                .And.AllSatisfy(d => d.Expired.Should().BeFalse());
        }

        [Fact]
        [UnitTest]
        public void ThrowExceptionOnExpiration_ThrowsException()
        {
            var value = 123;
            var decayingObject = new Decay<int>(value, expireAfterCount: 1, throwExceptionOnExpiration: true);

            decayingObject.Value.Should().Be(value);
            decayingObject.Expired.Should().BeFalse();

            decayingObject.Invoking(o => o.Value).Should().Throw<ObjectExpiredException>()
                .Where(e => e.Data.Contains("CounterExpired"),
                    "the exception dictionary should contain a 'CounterExpired' key.")
                .Where(e => e.Data.Contains("TimeExpired"),
                    "the exception dictionary should contain a 'TimeExpired' key.")
                .Where(e => e.Data.Contains("ConditionExpired"),
                    "the exception dictionary should contain a 'ConditionExpired' key.")
                .Where(e => e.Data.Contains("AccessCount"),
                    "the exception dictionary should contain a 'AccessCount' key.")
                .Where(e => e.Data.Contains("AccessCountLimit"),
                    "the exception dictionary should contain a 'AccessCountLimit' key.");

            decayingObject.Expired.Should().BeTrue();
        }
    }
}
