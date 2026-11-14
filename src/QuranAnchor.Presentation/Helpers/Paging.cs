using Microsoft.EntityFrameworkCore;

namespace QuranAnchor.Presentation.Helpers;

public sealed record PageInfo(int Page, int TotalPages, int Total, int Size)
{
    public int From => Total == 0 ? 0 : (Page - 1) * Size + 1;
    public int To => Math.Min(Total, Page * Size);
}

public static class Paging
{
    /// <summary>Returns one page of results and the numbers the pager needs.</summary>
    public static async Task<(List<T> Items, PageInfo Info)> PageAsync<T>(this IQueryable<T> query, int page, int size = 15)
    {
        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        page = Math.Clamp(page, 1, pages);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, new PageInfo(page, pages, total, size));
    }
}
