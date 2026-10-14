namespace SqlYedekci;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(a => a.Equals("--run", StringComparison.OrdinalIgnoreCase)))
            return RunJob(AppSettings.Load(AppPaths.SettingsFile), new Logger(AppPaths.LogDir));

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }

    /// <summary>Seçili veritabanlarını yedekler. Biri başarısız olsa da diğerlerine devam eder. Dönüş: hata sayısı (çıkış kodu).</summary>
    public static int RunJob(AppSettings settings, Logger log)
    {
        log.Write($"{AppInfo.Name} {AppInfo.Version} başladı ({settings.Server})");
        if (settings.Databases.Count == 0)
        {
            log.Write("HATA  Yedeklenecek veritabanı seçilmemiş.");
            return 1;
        }

        var engine = new SqlEngine(settings);
        var failed = 0;
        foreach (var db in settings.Databases)
        {
            var started = DateTime.Now;
            try
            {
                var file = engine.Backup(db, log);
                log.Write($"OK    {db} -> {Path.GetFileName(file)} ({new FileInfo(file).Length / 1048576.0:N1} MB, {(DateTime.Now - started).TotalSeconds:N0} sn)");
            }
            catch (Exception ex)
            {
                failed++;
                log.Write($"HATA  {db}: {ex.Message}");
            }
        }
        log.Write($"Bitti: {settings.Databases.Count} veritabanı, {failed} hata");
        return failed;
    }
}
