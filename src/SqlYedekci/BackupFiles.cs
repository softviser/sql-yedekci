using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace SqlYedekci;

internal sealed record BackupFile(string Path, string Database, DateTime Time, long Size);

/// <summary>Yedek dosyalarının adlandırılması, listelenmesi, sıkıştırılması ve döndürülmesi (SQL Server gerektirmez).</summary>
internal static partial class BackupFiles
{
    // yalnızca bu araçla oluşturulmuş dosyalar: <veritabanı>_YYYYMMDD_HHMMSS.zip|bak
    [GeneratedRegex(@"^(?<db>.+)_(?<ts>\d{8}_\d{6})\.(zip|bak)$", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();

    public static string SafeName(string database)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return new string(database.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    public static string NewBakPath(string dir, string database, DateTime now) =>
        System.IO.Path.Combine(dir, $"{SafeName(database)}_{now:yyyyMMdd_HHmmss}.bak");

    public static List<BackupFile> List(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        var result = new List<BackupFile>();
        foreach (var f in new DirectoryInfo(dir).EnumerateFiles())
        {
            var m = Pattern().Match(f.Name);
            if (m.Success)
                result.Add(new BackupFile(f.FullName, m.Groups["db"].Value,
                    DateTime.ParseExact(m.Groups["ts"].Value, "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture), f.Length));
        }
        return result.OrderByDescending(b => b.Time).ToList();
    }

    /// <summary>Veritabanının en yeni <paramref name="keep"/> yedeğini bırakır, gerisini siler. En az 1 yedek her zaman kalır.</summary>
    public static List<string> Rotate(string dir, string database, int keep)
    {
        var name = SafeName(database);
        var old = List(dir)
            .Where(b => string.Equals(b.Database, name, StringComparison.OrdinalIgnoreCase))
            .Skip(Math.Max(1, keep))
            .Select(b => b.Path)
            .ToList();
        old.ForEach(File.Delete);
        return old;
    }

    public static string Compress(string bakPath)
    {
        var zipPath = System.IO.Path.ChangeExtension(bakPath, ".zip");
        try
        {
            // ZipArchive gerektiğinde Zip64 kullanır; 4 GB üzeri yedekler sorun değil
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(bakPath, System.IO.Path.GetFileName(bakPath), CompressionLevel.Optimal);
        }
        catch
        {
            File.Delete(zipPath);   // yarım zip kalmasın, .bak korunur
            throw;
        }
        File.Delete(bakPath);
        return zipPath;
    }

    /// <summary>Zip içindeki .bak dosyasını zip'in yanına açar (SQL Server servisi okuyabilsin diye). Çağıran siler.</summary>
    public static string ExtractBak(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Zip içinde .bak dosyası yok.");
        var dest = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(zipPath)!,
            System.IO.Path.GetFileNameWithoutExtension(entry.Name) + $"_restore_{Guid.NewGuid():N}.bak");
        entry.ExtractToFile(dest);
        return dest;
    }
}
