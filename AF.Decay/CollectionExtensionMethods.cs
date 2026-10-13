using System.Collections.Generic;
using System.Linq;

namespace AF.Decay
{
    /// <summary>
    /// Extension methods for working with collections of <see cref="Decay{T}"/> objects.
    /// </summary>
    public static class CollectionExtensionMethods
    {
        /// <summary>
        /// Gets the next active item that is due to decay in the collection.
        /// Also, optionally removes expired items from the collection if it supports removal (default is true).
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="collection">The collection containing the decaying item.</param>
        /// <param name="clearDecayedItems">If true, will attempt to remove expired items from the collection if it supports removal. Default is true.</param>
        /// <returns>The next item due to decay, or null if no non-expired items exist.</returns>
        public static Decay<T> GetNextDecayingItem<T>(this ICollection<Decay<T>> collection, bool clearDecayedItems = true)
        {
            if (collection == null) return null;

            if (clearDecayedItems)
            {
                collection.ClearDecayedItems();
            }

            // Evaluate expiration once per item, then do a single linear scan for the minimum.
            Decay<T> next = null;
            foreach (var item in collection.ToList())
            {
                if (item == null || item.Expired) continue;
                if (next == null || item.CompareTo(next) < 0) next = item;
            }

            return next;
        }

        /// <summary>
        /// Gets all expired items from the collection.
        /// </summary>
        public static IEnumerable<Decay<T>> GetDecayedItems<T>(this IEnumerable<Decay<T>> collection) 
            => collection?.Where(i => i != null && i.Expired).ToList() ?? Enumerable.Empty<Decay<T>>();

        /// <summary>
        /// Gets all non-expired items from the collection.
        /// </summary>
        public static IEnumerable<Decay<T>> GetNonDecayedItems<T>(this IEnumerable<Decay<T>> collection) 
            => collection?.Where(i => i != null && !i.Expired).ToList() ?? Enumerable.Empty<Decay<T>>();

        /// <summary>
        /// Removes expired items from the collection if it supports removal. Read-only collections are left untouched.
        /// </summary>
        /// <returns>The number of items removed.</returns>
        public static int ClearDecayedItems<T>(this ICollection<Decay<T>> collection)
        {
            if (collection == null || collection.IsReadOnly)
            {
                return 0;
            }

            if (collection is List<Decay<T>> list)
            {
                return list.RemoveAll(i => i != null && i.Expired);
            }

            var removed = 0;

            foreach (var item in collection.Where(i => i != null && i.Expired).ToList())
            {
                if (collection.Remove(item))
                {
                    removed++;
                }
            }

            return removed;
        }
    }
}
