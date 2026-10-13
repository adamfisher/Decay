using System;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace AF.Decay.Tests
{
    public class ApiTests
    {
        private readonly FakeTimeProvider _time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        [Fact]
        public void Factory_InfersTheType()
        {
            Decay<string> decay = Decay.OnCount("token", 2);

            decay.Value.Should().Be("token");
        }

        [Fact]
        public void Builder_CombinesPolicies_FirstToTriggerWins()
        {
            var decay = Decay.Of("token")
                .ExpireAfterAccesses(10)
                .ExpireAfter(TimeSpan.FromMinutes(5))
                .UseTimeProvider(_time)
                .Build();

            decay.ExpiresAt.Should().Be(_time.GetUtcNow().AddMinutes(5));
            decay.Value.Should().Be("token");

            _time.Advance(TimeSpan.FromMinutes(6));

            decay.Expired.Should().BeTrue();
            decay.ExpirationReason.Should().Be(ExpirationReasons.Time);
        }

        [Fact]
        public void Builder_ConvertsImplicitly()
        {
            Decay<int> decay = Decay.Of(42).ExpireAfterAccesses(1);

            decay.AccessLimit.Should().Be(1);
        }

        [Fact]
        public void Builder_ExpireAtAndExpireAfter_LastOneWins()
        {
            var at = _time.GetUtcNow().AddHours(1);

            Decay.Of(1).UseTimeProvider(_time).ExpireAfter(TimeSpan.FromMinutes(1)).ExpireAt(at).Build()
                .ExpiresAt.Should().Be(at);
            Decay.Of(1).UseTimeProvider(_time).ExpireAt(at).ExpireAfter(TimeSpan.FromMinutes(1)).Build()
                .ExpiresAt.Should().Be(_time.GetUtcNow().AddMinutes(1));
        }

        [Fact]
        public void Builder_ThrowOnExpiration_ThrowsWithTypedReason()
        {
            var decay = Decay.Of("token").ExpireAfterAccesses(1).ThrowOnExpiration().Build();
            _ = decay.Value;

            decay.Invoking(d => d.Value).Should().Throw<ObjectExpiredException>()
                .Which.Reason.Should().Be(ExpirationReasons.Count);
        }

        [Fact]
        public void TryGetValue_ReturnsFalseOnExpiry_WithoutThrowing()
        {
            var decay = Decay.OnCount(0, 1, throwExceptionOnExpiration: true);

            decay.TryGetValue(out var first).Should().BeTrue();
            first.Should().Be(0);

            decay.TryGetValue(out var second).Should().BeFalse();
            second.Should().Be(0);
        }

        [Fact]
        public void GetValueOrDefault_ReturnsFallbackOnExpiry()
        {
            var decay = Decay.OnCount("live", 1);

            decay.GetValueOrDefault("fallback").Should().Be("live");
            decay.GetValueOrDefault("fallback").Should().Be("fallback");
        }

        [Fact]
        public void StateProperties_ReflectAccesses()
        {
            var decay = Decay.OnCount("x", 3);

            decay.AccessCount.Should().Be(0);
            decay.RemainingAccesses.Should().Be(3);

            _ = decay.Value;
            _ = decay.Value;

            decay.AccessCount.Should().Be(2);
            decay.RemainingAccesses.Should().Be(1);
            decay.ExpiresAt.Should().BeNull();
        }

        [Fact]
        public void AccessCount_IsTrackedWithoutAnAccessLimit()
        {
            var decay = new Decay<string>("x");
            _ = decay.Value;
            _ = decay.Value;

            decay.AccessCount.Should().Be(2);
            decay.RemainingAccesses.Should().BeNull();
        }

        [Fact]
        public void Expire_ExpiresImmediately()
        {
            var decay = new Decay<string>("x");

            decay.Expire();

            decay.Expired.Should().BeTrue();
            decay.ExpirationReason.Should().Be(ExpirationReasons.Manual);
            decay.Value.Should().BeNull();
        }

        [Fact]
        public void Condition_CanInspectTheValue()
        {
            var cart = new System.Collections.Generic.List<int> { 1, 2 };
            var decay = Decay.Of(cart).ExpireWhen(s => s.Value.Count == 0).Build();

            decay.Expired.Should().BeFalse();
            cart.Clear();
            decay.Expired.Should().BeTrue();
        }

        [Fact]
        public void Condition_ReceivesTheInjectedClock()
        {
            var decay = Decay.Of("x").UseTimeProvider(_time).ExpireWhen(s => s.Now.Hour >= 9).Build();

            decay.Expired.Should().BeFalse();
            _time.Advance(TimeSpan.FromHours(9));
            decay.Expired.Should().BeTrue();
        }

        [Fact]
        public void ExpirationReason_IsNoneWhileActive()
        {
            Decay.OnCount("x", 1).ExpirationReason.Should().Be(ExpirationReasons.None);
        }

        [Fact]
        public void NullValue_IsRejected()
        {
            FluentActions.Invoking(() => Decay.Of<string>(null!)).Should().Throw<ArgumentNullException>();
        }
    }
}
