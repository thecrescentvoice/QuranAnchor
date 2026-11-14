namespace QuranAnchor.Presentation.Helpers;

/// <summary>
/// Saves announcement flyers. Only JPG, PNG, WebP and PDF are accepted, the file's real content is checked
/// (not just its name), the size is limited, and the stored name is random.
/// </summary>
public static class FlyerStorage
{
    public const long MaxBytes = 5_000_000;
    private const string UrlFolder = "/uploads/flyers/";

    public sealed record Saved(string Path, string Name, string Kind);

    public static async Task<(Saved? File, string? Error)> SaveAsync(IFormFile file, string webRootPath)
    {
        if (file.Length == 0) return (null, "The file is empty.");
        if (file.Length > MaxBytes) return (null, "The flyer must be smaller than 5 MB.");

        var header = new byte[12];
        await using (var peek = file.OpenReadStream())
        {
            var read = await peek.ReadAsync(header.AsMemory(0, 12));
            if (read < 4) return (null, "That file type is not supported.");
        }

        string? ext = null, kind = null;
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) { ext = ".jpg"; kind = "image"; }
        else if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) { ext = ".png"; kind = "image"; }
        else if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50) { ext = ".webp"; kind = "image"; }
        else if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46) { ext = ".pdf"; kind = "pdf"; }
        if (ext is null || kind is null) return (null, "Use a JPG, PNG, WebP image or a PDF.");

        var folder = System.IO.Path.Combine(webRootPath, "uploads", "flyers");
        Directory.CreateDirectory(folder);

        var stored = Guid.NewGuid().ToString("N") + ext;
        await using (var target = System.IO.File.Create(System.IO.Path.Combine(folder, stored)))
        {
            await file.CopyToAsync(target);
        }

        var display = System.IO.Path.GetFileNameWithoutExtension(file.FileName);
        display = new string(display.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray()).Trim();
        if (display.Length == 0) display = "flyer";
        if (display.Length > 80) display = display[..80];

        return (new Saved(UrlFolder + stored, display + ext, kind), null);
    }

    /// <summary>Deletes a stored flyer. Only files inside the flyers folder can be removed.</summary>
    public static void Delete(string? path, string webRootPath)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith(UrlFolder, StringComparison.Ordinal)) return;
        var name = System.IO.Path.GetFileName(path);
        var full = System.IO.Path.Combine(webRootPath, "uploads", "flyers", name);
        if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
    }
}
