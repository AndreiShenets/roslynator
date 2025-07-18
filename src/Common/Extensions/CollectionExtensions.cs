#nullable enable

using System;
using System.Collections.Generic;

namespace Roslynator;

public static class CollectionExtensions
{
    public static int LastIndexOf<T>(this IReadOnlyList<T> list, Func<T, bool> predicate)
    {
        if (list is null)
        {
            throw new ArgumentNullException(nameof(list));
        }

        if (list.Count == 0)
        {
            return -1;
        }

        int index = list.Count - 1;

        while (index >= 0)
        {
            if (predicate(list[index]))
            {
                return index;
            }

            index--;
        }

        return -1;
    }
}
