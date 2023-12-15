using System.Collections.Generic;

namespace wipbot.Utils
{
    public class ExtendedQueue<T> : Queue<T>
    {
        private readonly object lockObject = new object();

        /// <summary>
        /// Removes the first occurrence of a specific object from the ExtendedQueue<T>.
        /// </summary>
        /// <param name="item">The object to remove from the ExtendedQueue<T>.</param>
        /// <returns>True if item is successfully removed; otherwise, false. This method also returns false if item was not found in the ExtendedQueue<T>.</returns>
        public bool Remove(T item)
        {
            lock (lockObject)
            {
                if (!Contains(item))
                {
                    return false;
                }

                var buffer = new List<T>(Count);
                while (Count > 0)
                {
                    var current = Dequeue();
                    if (!current.Equals(item))
                    {
                        buffer.Add(current);
                    }
                }

                foreach (var element in buffer)
                {
                    Enqueue(element);
                }

                return true;
            }
        }
    }

}
