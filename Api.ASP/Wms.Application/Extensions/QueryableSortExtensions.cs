using System.Linq.Expressions;

namespace Wms.Application.Extensions;

public static class QueryableSortExtensions
{
    public static IOrderedQueryable<T> OrderByDirection<T, TKey>(
        this IQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        bool descending)
        => descending
            ? source.OrderByDescending(keySelector)
            : source.OrderBy(keySelector);

    public static IOrderedQueryable<T> ThenByDirection<T, TKey>(
        this IOrderedQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        bool descending)
        => descending
            ? source.ThenByDescending(keySelector)
            : source.ThenBy(keySelector);
}
