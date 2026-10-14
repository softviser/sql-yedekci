using System.Diagnostics;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SqlYedekci;

internal static class AppInfo
{
    public const string Name = "SQL Yedekçi";
    public const string Company = "Softviser Yazılım";
    public const string Website = "https://softviser.com.tr";
    public const string Repository = "https://github.com/softviser/sql-yedekci";
    public const string TaskName = "SQL Yedekci";

    public static string Version { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "3.0.1";

    public static Icon AppIcon { get; } = LoadIcon();

    private static Icon LoadIcon()
    {
        using var stream = typeof(AppInfo).Assembly.GetManifestResourceStream("SqlYedekci.Assets.app.ico");
        return stream != null ? new Icon(stream) : SystemIcons.Application;
    }

    public static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch { /* tarayıcı/gezgin açılamazsa yoksay */ }
    }
}

/// <summary>Ayarlar ve loglar %ProgramData%\Softviser\SqlYedekci altında; zamanlanmış görev (SYSTEM) de aynı yeri okur.</summary>
internal static class AppPaths
{
    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Softviser", "SqlYedekci");

    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string LogDir => Path.Combine(DataDir, "logs");

    /// <summary>Klasörü yalnızca Administrators ve SYSTEM erişebilecek şekilde oluşturur (ayarlarda şifreli SQL parolası durur).</summary>
    public static void EnsureDataDir()
    {
        var dir = new DirectoryInfo(DataDir);
        if (!dir.Exists) dir.Create();
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { WellKnownSidType.BuiltinAdministratorsSid, WellKnownSidType.LocalSystemSid })
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        dir.SetAccessControl(acl);
    }
}

/// <summary>Aylık log dosyasına yazar; isteğe bağlı olarak satırı ekrana da iletir.</summary>
internal sealed class Logger(string dir, Action<string>? echo = null)
{
    public string CurrentFile => Path.Combine(dir, $"sql-yedekci_{DateTime.Now:yyyyMM}.log");

    public void Write(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}";
        echo?.Invoke(line);
        try
        {
            Directory.CreateDirectory(dir);
            File.AppendAllText(CurrentFile, line + Environment.NewLine);
        }
        catch { /* log yazılamaması yedeklemeyi durdurmamalı */ }
    }
}
