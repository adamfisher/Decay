using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace AF.Decay.Tests
{
    public class RegressionTests
    {
        [Fact]
        public void DefaultValue_IsNotTreatedAsExpired()
        {
            var zero = new Decay<int>(0);
            var no = new Decay<bool>(false);

            zero.Expired.Should().BeFalse();
            zero.Value.Should().Be(0);
            no.Expired.Should().BeFalse();
        }

        [Fact]
        public void DefaultValue_ExpiresOnCount()
        {
            var zero = Decay<int>.OnCount(0, 1, throwExceptionOnExpiration: true);

            zero.Value.Should().Be(0);
            zero.Invoking(d => d.Value).Should().Throw<ObjectExpiredException>();
        }

        [Fact]
        public void ClearDecayedItems_RemovesTheExpiredInstance_NotAnEqualValuedActiveOne()
        {
            var active = Decay<int>.OnCount(5, 10);
            var expired = Decay<int>.OnExpiration(5, DateTimeOffset.UtcNow.AddSeconds(-1));
            var list = new List<Decay<int>> { active, expired };

            list.ClearDecayedItems().Should().Be(1);

            list.Should().ContainSingle().Which.Should().BeSameAs(active);
        }

        [Fact]
        public void ClearDecayedItems_WorksForHashSets_AfterValueIsReleased()
        {
            var decay = Decay<string>.OnCount("x", 1);
            var set = new HashSet<Decay<string>> { decay };
            _ = decay.Value;
            _ = decay.Value; // expires

            set.ClearDecayedItems().Should().Be(1);
            set.Should().BeEmpty();
        }

        [Fact]
        public void EqualValues_AreNotEqualDecayInstances()
        {
            var a = Decay<int>.OnCount(5, 1);
            var b = Decay<int>.OnExpiration(5, TimeSpan.FromHours(1));

            a.Equals(b).Should().BeFalse();
            (a == b).Should().BeFalse();
        }

        [Fact]
        public void ReadOnlyCollections_AreNotModified_AndDoNotThrow()
        {
            var expired = Decay<int>.OnExpiration(1, DateTimeOffset.UtcNow.AddSeconds(-1));
            var active = Decay<int>.OnCount(2, 5);
            ICollection<Decay<int>> array = new[] { expired, active };

            array.GetNextDecayingItem().Should().BeSameAs(active);
            array.ClearDecayedItems().Should().Be(0);
        }

        [Fact]
        public void ExpiredException_DoesNotLeakTheValue()
        {
            var secret = new Decay<string>("hunter2-api-key", expireAfterCount: 1, throwExceptionOnExpiration: true);
            _ = secret.Value;

            secret.Invoking(s => s.Value).Should().Throw<ObjectExpiredException>()
                .Which.Message.Should().NotContain("hunter2");
        }

        [Fact]
        public void Expiration_IsPermanent_EvenWhenConditionFlipsBack_AndThrowing()
        {
            var flag = true;
            var decay = new Decay<string>("v", expireOnCondition: (_, _) => flag, throwExceptionOnExpiration: true);

            decay.Invoking(d => d.Value).Should().Throw<ObjectExpiredException>();
            flag = false;

            decay.Expired.Should().BeTrue();
            decay.Invoking(d => d.Value).Should().Throw<ObjectExpiredException>();
        }

        [Fact]
        public void Expiration_IsPermanent_WhenNotThrowing()
        {
            var flag = true;
            var decay = new Decay<string>("v", expireOnCondition: (_, _) => flag);

            decay.Expired.Should().BeTrue();
            flag = false;

            decay.Expired.Should().BeTrue();
            decay.Value.Should().BeNull();
        }

        [Fact]
        public void CompareTo_DoesNotCompareSecondsAgainstAccessCounts()
        {
            var timeBound = Decay<int>.OnExpiration(1, TimeSpan.FromHours(1));
            var countBound = Decay<int>.OnCount(2, 5);
            var list = new List<Decay<int>> { countBound, timeBound };

            list.Sort();

            // Remaining accesses is the primary key and expiration time the secondary; the two are never mixed.
            list.Should().Equal(countBound, timeBound);
        }

        [Fact]
        public void CompareTo_OrdersByTime_WhenAccessCountsTie()
        {
            var later = Decay<int>.OnExpiration(1, TimeSpan.FromHours(2));
            var sooner = Decay<int>.OnExpiration(2, TimeSpan.FromHours(1));
            var list = new List<Decay<int>> { later, sooner };

            list.Sort();

            list.Should().Equal(sooner, later);
        }

        [Fact]
        public void CompareTo_PutsExpiredItemsFirst()
        {
            var active = Decay<int>.OnExpiration(1, TimeSpan.FromHours(1));
            var countExpired = Decay<int>.OnCount(2, 1);
            _ = countExpired.Value;
            _ = countExpired.Value;
            var list = new List<Decay<int>> { active, countExpired };

            list.Sort();

            list.First().Should().BeSameAs(countExpired);
        }

        [Fact]
        public void CompareTo_DoesNotInvokeCustomConditions()
        {
            var calls = 0;
            var list = Enumerable.Range(0, 50)
                .Select(i => Decay<int>.OnCondition(i, (_, _) => { calls++; return false; }))
                .ToList();

            list.Sort();

            calls.Should().Be(0);
        }

        [Fact]
        public void Value_IsHiddenFromDebugger_SoInspectionDoesNotConsumeAccesses()
        {
            var attribute = typeof(Decay<int>).GetProperty(nameof(Decay<int>.Value))!
                .GetCustomAttribute<DebuggerBrowsableAttribute>();

            attribute.Should().NotBeNull();
            attribute!.State.Should().Be(DebuggerBrowsableState.Never);
        }

        [Fact]
        public void ConcurrentAccess_HonorsCountLimitExactly()
        {
            const int limit = 1000;
            var decay = Decay<object>.OnCount(new object(), limit);
            var hits = 0;

            Parallel.For(0, limit * 4, _ =>
            {
                if (decay.Value != null) System.Threading.Interlocked.Increment(ref hits);
            });

            hits.Should().Be(limit);
            decay.Expired.Should().BeTrue();
        }
    }
}
