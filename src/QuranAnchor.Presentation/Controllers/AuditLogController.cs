using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

[Authorize(Policy = Permissions.ViewAuditLog)]
public class AuditLogController(QuranAnchorDbContext db) : Controller
{
    private const int PageSize = 50;

    public async Task<IActionResult> Index(int page = 1)
    {
        var total = await db.AuditLogs.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.OccurredAtUtc)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        return View(new AuditLogPageViewModel { Items = items, Page = page, TotalPages = totalPages });
    }
}
