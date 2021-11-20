using System.Collections.Generic;
using System.Linq;

namespace VMC
{
    public static class IEnumerableExtensions
    {
        public static bool ContainsArray<T>(this IEnumerable<T> source, IEnumerable<T> target)
        {
            foreach (var d in target)
            {
                if (source.Contains(d) == false) return false;
            }
            return true;
        }
    }
}