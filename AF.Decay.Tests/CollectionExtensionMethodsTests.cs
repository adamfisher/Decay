using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace AF.Decay.Tests
{
    public class CollectionExtensionMethodsTests
    {
        private readonly List<Decay<int>> _collection = new()
        {
            new (1, expirationDateTime: DateTime.Now.AddSeconds(5)),
            new (2, expireAfterCount: 3),
            new (3, expireOnCondition: (_, _) => true), // Expire immediately
            new (4, expireAfterCount: 2)
        };

        [Fact]
        public void GetNextDecayingItem_ReturnsNextDecayingItem()
        {
            var nextDecayingItem = _collection.GetNextDecayingItem();
            nextDecayingItem.Should().NotBeNull();
            nextDecayingItem.Value.Should().Be(4);

            // Access item enough times to expire it
            _ = nextDecayingItem.Value;
            _ = nextDecayingItem.Value;

            nextDecayingItem = _collection.GetNextDecayingItem();
            nextDecayingItem.Should().NotBeNull();
            nextDecayingItem.Value.Should().Be(2);
        }

        [Fact]
        public void GetExpired()
        {
            _collection.GetExpired().Should().HaveCount(1);
        }

        [Fact]
        public void GetActive()
        {
            _collection.GetActive().Should().HaveCount(3);
        }

        [Fact]
        public void RemoveExpired()
        {
            _collection.RemoveExpired().Should().Be(1);
            _collection.Should().HaveCount(3);
        }

        [Fact]
        public void ObsoleteAliases_StillWork()
        {
#pragma warning disable CS0618
            _collection.GetDecayedItems().Should().HaveCount(1);
            _collection.GetNonDecayedItems().Should().HaveCount(3);
            _collection.ClearDecayedItems().Should().Be(1);
#pragma warning restore CS0618
        }
    }
}