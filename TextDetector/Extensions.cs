using System;
using System.Collections.Generic;
using System.Linq;

namespace TextDetector
{
    public static class Extensions
    {
        // https://docs.microsoft.com/ja-jp/dotnet/csharp/programming-guide/concepts/linq/how-to-add-custom-methods-for-linq-queries
        public static int Median(this IEnumerable<int> source)
        {
            if (source.Count() == 0)
            {
                throw new InvalidOperationException("Cannot compute median for an empty set.");
            }

            var sortedList = from number in source
                             orderby number
                             select number;

            int itemIndex = sortedList.Count() / 2;

            if (sortedList.Count() % 2 == 0)
            {
                // Even number of items.  
                return (sortedList.ElementAt(itemIndex) + sortedList.ElementAt(itemIndex - 1)) / 2;
            }
            else
            {
                // Odd number of items.  
                return sortedList.ElementAt(itemIndex);
            }
        }
    }
}
