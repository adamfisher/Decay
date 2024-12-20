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
        public void GetDecayedItems()
        {
            var nonDecayedItems = _collection.GetDecayedItems();
            nonDecayedItems.Should().HaveCount(1);
        }

        [Fact]
        public void GetNonDecayedItems()
        {
            var nonDecayedItems = _collection.GetNonDecayedItems();
            nonDecayedItems.Should().HaveCount(3);
        }

        [Fact]
        public void ClearDecayedItems()
        {
            var clearedItems = _collection.ClearDecayedItems();
            clearedItems.Should().Be(1);
        }
    }
}