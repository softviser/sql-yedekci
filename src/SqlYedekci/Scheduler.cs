using System.Diagnostics;

namespace SqlYedekci;

/// <summary>Windows Görev Zamanlayıcı'ya schtasks.exe ile görev kurar. Görev SYSTEM hesabıyla "SqlYedekci.exe --run" çalıştırır.</summary>
internal static class Scheduler
{
    public static List<string> CreateArgs(AppSettings s, string exePath)
    {
        var args = new List<string> { "/Create", "/F", "/TN", AppInfo.TaskName, "/TR", $"\"{exePath}\" --run", "/RU", "SYSTEM", "/RL", "HIGHEST" };
        switch (s.Frequency)
        {
            case Frequency.Hourly:
                args.AddRange(["/SC", "HOURLY", "/MO", Math.Clamp(s.IntervalHours, 1, 23).ToString()]);
                break;
            case Frequency.Daily:
                args.AddRange(["/SC", "DAILY"]);
                break;
            case Frequency.Weekly:
                args.AddRange(["/SC", "WEEKLY", "/D", s.Day.ToString()[..3].ToUpperInvariant()]);
                break;
        }
        args.AddRange(["/ST", s.Time]);
        return args;
    }

    public static string Describe(AppSettings s) => s.Frequency switch
    {
        Frequency.Hourly => $"{s.Time} itibarıyla her {s.IntervalHours} saatte bir",
        Frequency.Weekly => $"her {Culture.DayNames[(int)s.Day]} {s.Time}",
        _ => $"her gün {s.Time}",
    };

    public static void Install(AppSettings s) => Run(CreateArgs(s, Environment.ProcessPath!));

    public static void Remove() => Run(["/Delete", "/F", "/TN", AppInfo.TaskName]);

    public static bool Exists() => Run(["/Query", "/TN", AppInfo.TaskName], throwOnError: false) == 0;

    private static int Run(List<string> args, bool throwOnError = true)
    {
        var psi = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        args.ForEach(psi.ArgumentList.Add);
        using var p = Process.Start(psi)!;
        var error = p.StandardError.ReadToEnd();
        p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0 && throwOnError) throw new InvalidOperationException("Görev Zamanlayıcı hatası: " + error.Trim());
        return p.ExitCode;
    }
}

internal static class Culture
{
    public static readonly string[] DayNames = ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"];
}
