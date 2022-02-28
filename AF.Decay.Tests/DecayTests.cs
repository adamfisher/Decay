using System;
using System.Threading;
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
            decayingObject.IsValueExpired.Should().BeFalse();
        }

        [Fact]
        public void Constructor_ExpireOnCount_ExpiresAfter3()
        {
            var value = "SomeConfigValue";
            var decayingObject = new Decay<string>(value, expireAfterCount: 3);

            decayingObject.Value.Should().Be(value);
            decayingObject.IsValueExpired.Should().BeFalse();

            decayingObject.Value.Should().Be(value);
            decayingObject.IsValueExpired.Should().BeFalse();

            decayingObject.Value.Should().Be(value);
            decayingObject.IsValueExpired.Should().BeFalse();

            decayingObject.Value.Should().BeNull();
            decayingObject.IsValueExpired.Should().BeTrue();
        }

        [Fact]
        public void Constructor_ExpireAfterTime_ExpiresIn3Seconds()
        {
            var value = "SomeConfigValue";
            var decayingObject = new Decay<string>(value, expireAfterTime: TimeSpan.FromMilliseconds(100));

            decayingObject.Value.Should().Be(value);
            decayingObject.IsValueExpired.Should().BeFalse();

            // Wait at least as long as the object's time horizon and we should be past it by now.
            Thread.Sleep(TimeSpan.FromMilliseconds(500));

            decayingObject.Value.Should().BeNull();
            decayingObject.IsValueExpired.Should().BeTrue();
        }

        [Fact]
        public void Constructor_ExpireOnCondition_ExpiresWhenCustomConditionIsTrue()
        {
            var value = "SomeConfigValue";

            var decayingObject = new Decay<string>(value, expireOnCondition: ((creationTime, currentAccessCount, expireAfterTime) =>
            {
                (creationTime <= DateTimeOffset.UtcNow).Should()
                    .BeTrue("the creation time of th decaying object should be earlier than the current time.");
                currentAccessCount.Should().Be(1);
                expireAfterTime.Should().BeNull();

                return true;
            }));

            decayingObject.Value.Should().BeNull();
            decayingObject.IsValueExpired.Should().BeTrue();
        }

        [Fact]
        public void Constructor_ThrowExceptionOnExpiration_ThrowsException()
        {
            var value = 123;
            var decayingObject = new Decay<int>(value, expireAfterCount: 0, throwExceptionOnExpiration: true);

            decayingObject.Invoking(o => o.Value).Should().Throw<ObjectDecayedException>()
                .Where(e => e.Data.Contains("CounterExpired"),
                    "the exception dictionary should contain a 'CounterExpired' key.")
                .Where(e => e.Data.Contains("TimeExpired"),
                    "the exception dictionary should contain a 'TimeExpired' key.")
                .Where(e => e.Data.Contains("ConditionExpired"),
                    "the exception dictionary should contain a 'ConditionExpired' key.")
                .Where(e => e.Data.Contains("AccessCount"),
                    "the exception dictionary should contain a 'AccessCount' key.")
                .Where(e => e.Data.Contains("ExpireAfterCountLimit"),
                    "the exception dictionary should contain a 'ExpireAfterCountLimit' key.");

            decayingObject.IsValueExpired.Should().BeTrue();
        }
    }
}
