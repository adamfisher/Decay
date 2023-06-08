using System;
using System.Threading;
using CategoryTraits.Xunit2;
using FluentAssertions;
using Xunit;

namespace AF.Decay.Tests
{
    public class DecayTests
    {
        [Fact]
        public void Constructor_NoExpirationPolicy_DoesNotExpire()
        {
            var value = 4523;
            var decayingObject = new Decay<int>(value);

            decayingObject.Value.Should().Be(value);
            decayingObject.Expired.Should().BeFalse();
        }

        [Fact]
        public void Constructor_ExpireOnCount_ExpiresAfter3()
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
        public void Constructor_ExpireOnCount_ExpiresAfter1_WhenNegativeNumberIsSet()
        {
            var value = "SomeConfigValue";
            var decayingObject = new Decay<string>(value, expireAfterCount: -10);

            decayingObject.Value.Should().Be(value);
            decayingObject.Expired.Should().BeFalse();

            decayingObject.Value.Should().BeNull();
            decayingObject.Expired.Should().BeTrue();
        }

        [Fact]
        public void Constructor_ExpireAfterTime_ExpiresIn3Seconds()
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
        public void Constructor_ExpireOnCondition_ExpiresWhenCustomConditionIsTrue()
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
        public void Constructor_ThrowExceptionOnExpiration_ThrowsException()
        {
            var value = 123;
            var decayingObject = new Decay<int>(value, expireAfterCount: 1, throwExceptionOnExpiration: true);

            decayingObject.Value.Should().Be(value);
            decayingObject.Expired.Should().BeFalse();

            decayingObject.Invoking(o => o.Value).Should().Throw<ObjectDecayedException>()
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
