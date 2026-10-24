using QuranAnchor.Domain.Entities;

namespace QuranAnchor.Presentation.ViewModels;

public class DashboardViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool CanEditBranding { get; set; }
    public bool CanViewAuditLog { get; set; }
}

public class AuditLogPageViewModel
{
    public IReadOnlyList<AuditLog> Items { get; set; } = Array.Empty<AuditLog>();
    public int Page { get; set; }
    public int TotalPages { get; set; }
}
