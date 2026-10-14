using System.IO.Compression;
using SqlYedekci;

var fails = 0;
void Assert(bool condition, string name)
{
    Console.WriteLine((condition ? "OK    " : "HATA  ") + name);
    if (!condition) fails++;
}

var dir = Path.Combine(Path.GetTempPath(), "sql-yedekci-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
try
{
    // Dosya adları
    Assert(BackupFiles.SafeName("Satış:2026/Q1") == "Satış_2026_Q1", "SafeName geçersiz karakterleri değiştirir");
    Assert(Path.GetFileName(BackupFiles.NewBakPath(dir, "Satis", new DateTime(2026, 10, 15, 2, 0, 5))) == "Satis_20261015_020005.bak", "NewBakPath biçimi");

    // Sıkıştırma ve geri açma
    var bak = Path.Combine(dir, "Db_20260101_010101.bak");
    File.WriteAllText(bak, new string('x', 10000));
    var zip = BackupFiles.Compress(bak);
    Assert(File.Exists(zip) && !File.Exists(bak), "Compress zip oluşturur, .bak siler");
    var extracted = BackupFiles.ExtractBak(zip);
    Assert(File.ReadAllText(extracted).Length == 10000, "ExtractBak içeriği geri verir");
    File.Delete(extracted);
    File.Delete(zip);

    // Listeleme ve döndürme
    foreach (var name in new[]
    {
        "Satis_20260101_020000.zip", "Satis_20260102_020000.zip", "Satis_20260103_020000.bak", "Satis_20260104_020000.zip",
        "Satis_Arsiv_20260101_020000.zip", "Stok_20250101_020000.zip", "baska-dosya.zip",
    })
        File.WriteAllText(Path.Combine(dir, name), "x");

    var list = BackupFiles.List(dir);
    Assert(list.Count == 6 && list[0].Database == "Satis" && list[0].Time == new DateTime(2026, 1, 4, 2, 0, 0), "List yalnızca yedekleri, yeniden eskiye döner");
    Assert(list.Any(b => b.Database == "Satis_Arsiv"), "List alt çizgili veritabanı adını doğru ayırır");

    var deleted = BackupFiles.Rotate(dir, "Satis", 2).Select(Path.GetFileName).OrderBy(n => n).ToList();
    Assert(string.Join(",", deleted) == "Satis_20260101_020000.zip,Satis_20260102_020000.zip", $"Rotate en yeni 2 yedeği bırakır (silinen: {string.Join(", ", deleted)})");
    Assert(File.Exists(Path.Combine(dir, "Satis_Arsiv_20260101_020000.zip")), "Rotate başka veritabanına dokunmaz");
    Assert(BackupFiles.Rotate(dir, "Stok", 0).Count == 0, "Rotate keep=0 olsa da son yedeği bırakır");
    Assert(File.Exists(Path.Combine(dir, "baska-dosya.zip")), "Rotate araca ait olmayan dosyaya dokunmaz");

    // Zamanlama argümanları
    var s = new AppSettings { Frequency = Frequency.Weekly, Day = DayOfWeek.Monday, Time = "03:30" };
    var taskArgs = string.Join(" ", Scheduler.CreateArgs(s, @"C:\Program Files\Softviser\SQL Yedekci\SqlYedekci.exe"));
    Assert(taskArgs.Contains("/SC WEEKLY /D MON") && taskArgs.Contains("/ST 03:30") && taskArgs.Contains("/RU SYSTEM"), "Haftalık görev argümanları");
    Assert(taskArgs.Contains("\"C:\\Program Files\\Softviser\\SQL Yedekci\\SqlYedekci.exe\" --run"), "Görev komutu yolu tırnaklı");
    s.Frequency = Frequency.Hourly; s.IntervalHours = 40;
    Assert(string.Join(" ", Scheduler.CreateArgs(s, "x.exe")).Contains("/SC HOURLY /MO 23"), "Saatlik aralık 1-23 ile sınırlı");
    s.Frequency = Frequency.Daily;
    Assert(string.Join(" ", Scheduler.CreateArgs(s, "x.exe")).Contains("/SC DAILY /ST 03:30"), "Günlük görev argümanları");

    // Ayarlar: parola şifreli saklanır, geri okunur
    var path = Path.Combine(dir, "settings.json");
    new AppSettings { SqlAuth = true, User = "yedek", Password = "Gizli!123", Databases = ["A", "B"], Frequency = Frequency.Weekly }.Save(path);
    var json = File.ReadAllText(path);
    var loaded = AppSettings.Load(path);
    Assert(!json.Contains("Gizli!123") && json.Contains("\"Weekly\""), "Parola düz metin yazılmaz, enum adıyla yazılır");
    Assert(loaded.Password == "Gizli!123" && loaded.Databases.Count == 2 && loaded.Frequency == Frequency.Weekly, "Ayarlar geri okunur");
    Assert(loaded.ConnectionString().Contains("User ID=yedek") && !new AppSettings().ConnectionString().Contains("User ID"), "Bağlantı cümlesi kimlik doğrulamaya göre");
}
finally
{
    Directory.Delete(dir, true);
}

Console.WriteLine(fails == 0 ? "Tüm testler geçti" : $"{fails} test başarısız");
return fails == 0 ? 0 : 1;

