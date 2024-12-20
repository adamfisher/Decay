using System.Collections.Generic;
using System.Linq;

namespace AF.Decay
{
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
                foreach (var item in collection.Where(i => i.Expired).ToList())
                {
                    collection.Remove(item);
                }
            }

            var sortedItems = collection.ToList();
            sortedItems.Sort();
            return sortedItems.FirstOrDefault(i => !i.Expired);
        }

        /// <summary>
        /// Gets all expired items from the collection.
        /// </summary>
        public static IEnumerable<Decay<T>> GetDecayedItems<T>(this IEnumerable<Decay<T>> collection) 
            => collection?.ToList().Where(i => i.Expired) ?? Enumerable.Empty<Decay<T>>();

        /// <summary>
        /// Gets all non-expired items from the collection.
        /// </summary>
        public static IEnumerable<Decay<T>> GetNonDecayedItems<T>(this IEnumerable<Decay<T>> collection) 
            => collection?.ToList().Where(i => !i.Expired) ?? Enumerable.Empty<Decay<T>>();

        /// <summary>
        /// Removes expired items from the collection if it supports removal.
        /// </summary>
        /// <returns>The number of items removed.</returns>
        public static int ClearDecayedItems<T>(this ICollection<Decay<T>> collection)
        {
            if (collection == null)
            {
                return 0;
            }

            var itemsToRemove = collection.Where(i => i.Expired).ToList();

            foreach (var item in itemsToRemove)
            {
                collection.Remove(item);
            }

            return itemsToRemove.Count;
        }
    }
}