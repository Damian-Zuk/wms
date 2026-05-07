using Wms.Domain.Primitives;

namespace Wms.Application.Extensions;

public static class QueryableExtensions
{   
    public static IQueryable<T> ApplyIsDeletedFilter<T>(this IQueryable<T> query, bool isDeleted = false) 
        where T : Entity
    {
        return query.Where(x => x.IsDeleted == isDeleted);
    }
}
