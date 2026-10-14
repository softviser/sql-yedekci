using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;

namespace SqlYedekci;

internal enum Frequency { Hourly, Daily, Weekly }

internal sealed class AppSettings
{
    public string Server { get; set; } = ".";
    public bool SqlAuth { get; set; }
    public string User { get; set; } = "";
    public string ProtectedPassword { get; set; } = "";
    public bool TrustServerCertificate { get; set; } = true;

    public List<string> Databases { get; set; } = [];
    public string BackupDir { get; set; } = @"C:\SqlYedek";
    public Frequency Frequency { get; set; } = Frequency.Daily;
    public int IntervalHours { get; set; } = 6;
    public string Time { get; set; } = "02:00";
    public DayOfWeek Day { get; set; } = DayOfWeek.Sunday;
    public int KeepCount { get; set; } = 7;
    public bool Compress { get; set; } = true;
    public bool Verify { get; set; } = true;

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Softviser.SqlYedekci");

    /// <summary>DPAPI (LocalMachine): SYSTEM hesabıyla çalışan görev de çözebilsin. Dosya erişimi ayrıca ACL ile sınırlı.</summary>
    [JsonIgnore]
    public string Password
    {
        get => ProtectedPassword == "" ? "" : Encoding.UTF8.GetString(
            ProtectedData.Unprotect(Convert.FromBase64String(ProtectedPassword), Entropy, DataProtectionScope.LocalMachine));
        set => ProtectedPassword = value == "" ? "" : Convert.ToBase64String(
            ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.LocalMachine));
    }

    public string ConnectionString(string database = "master")
    {
        var b = new SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = database,
            IntegratedSecurity = !SqlAuth,
            TrustServerCertificate = TrustServerCertificate,
            ConnectTimeout = 10,
            ApplicationName = "SQL Yedekci",
        };
        if (SqlAuth) { b.UserID = User; b.Password = Password; }
        return b.ConnectionString;
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static AppSettings Load(string path) =>
        File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new() : new();

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
}
