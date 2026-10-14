using System.Data;
using Microsoft.Data.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace SqlYedekci;

internal sealed record ServerInfo(string Version, string MachineName, bool IsLocal, bool IsSysAdmin);

/// <summary>SQL Server işlemleri: sunucu arama, bağlantı testi, yedekleme, doğrulama ve geri yükleme.</summary>
internal sealed class SqlEngine(AppSettings settings)
{
    private const string SystemLogin = @"NT AUTHORITY\SYSTEM";

    private SqlConnection Open()
    {
        var conn = new SqlConnection(settings.ConnectionString());
        conn.Open();
        return conn;
    }

    private static SqlCommand Command(SqlConnection conn, string sql, params (string Name, object Value)[] parameters)
    {
        var cmd = new SqlCommand(sql, conn) { CommandTimeout = 0 };   // büyük veritabanlarında dakikalar sürebilir
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        return cmd;
    }

    private static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";

    /// <summary>Bu makinedeki örnekler (kayıt defteri) + ağdaki örnekler (SQL Browser yayını).</summary>
    public static List<string> DiscoverServers()
    {
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL");
            foreach (var instance in key?.GetValueNames() ?? [])
                found.Add(instance.Equals("MSSQLSERVER", StringComparison.OrdinalIgnoreCase)
                    ? Environment.MachineName : $@"{Environment.MachineName}\{instance}");
        }
        catch { /* kayıt defteri okunamazsa ağ taramasıyla devam */ }

        try
        {
            foreach (DataRow row in SqlDataSourceEnumerator.Instance.GetDataSources().Rows)
            {
                var server = row["ServerName"] as string;
                var instance = row["InstanceName"] as string;
                if (!string.IsNullOrEmpty(server))
                    found.Add(string.IsNullOrEmpty(instance) ? server : $@"{server}\{instance}");
            }
        }
        catch { /* SQL Browser kapalı ya da güvenlik duvarı engelliyor olabilir */ }

        return found.ToList();
    }

    public ServerInfo Test()
    {
        using var conn = Open();
        using var r = Command(conn, "SELECT @@VERSION, CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)), IS_SRVROLEMEMBER('sysadmin')").ExecuteReader();
        r.Read();
        var machine = r.GetString(1);
        return new ServerInfo(r.GetString(0).Split('\n')[0].Trim(), machine,
            machine.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase), r.GetInt32(2) == 1);
    }

    public List<string> ListDatabases()
    {
        using var conn = Open();
        using var r = Command(conn, "SELECT name FROM sys.databases WHERE database_id > 4 AND state = 0 AND source_database_id IS NULL ORDER BY name").ExecuteReader();
        var list = new List<string>();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    /// <summary>Yedek alır, isteğe bağlı doğrular ve sıkıştırır, eski yedekleri döndürür. Son dosyanın yolunu döner.</summary>
    public string Backup(string database, Logger log)
    {
        Directory.CreateDirectory(settings.BackupDir);
        var file = BackupFiles.NewBakPath(settings.BackupDir, database, DateTime.Now);
        using (var conn = Open())
        {
            Command(conn, "BACKUP DATABASE @db TO DISK = @file WITH INIT, CHECKSUM", ("@db", database), ("@file", file)).ExecuteNonQuery();
            if (settings.Verify)
                Command(conn, "RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM", ("@file", file)).ExecuteNonQuery();
        }
        if (settings.Compress) file = BackupFiles.Compress(file);
        foreach (var deleted in BackupFiles.Rotate(settings.BackupDir, database, settings.KeepCount))
            log.Write($"Silindi: {Path.GetFileName(deleted)}");
        return file;
    }

    /// <summary>.bak ya da .zip yedeğini geri yükler. Hedef varsa ve <paramref name="overwrite"/> ise üzerine yazar.</summary>
    public void Restore(string backupPath, string target, bool overwrite, Logger log)
    {
        var isZip = backupPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        var bak = isZip ? BackupFiles.ExtractBak(backupPath) : backupPath;
        try
        {
            using var conn = Open();
            var existing = new Dictionary<int, string>();
            using (var r = Command(conn, "SELECT file_id, physical_name FROM sys.master_files WHERE database_id = DB_ID(@db)", ("@db", target)).ExecuteReader())
                while (r.Read()) existing[r.GetInt32(0)] = r.GetString(1);
            if (existing.Count > 0 && !overwrite)
                throw new InvalidOperationException($"'{target}' veritabanı zaten var. Üzerine yazmak için seçeneği işaretleyin.");

            string dataPath, logPath;
            using (var r = Command(conn, """
                SELECT CAST(ISNULL(SERVERPROPERTY('InstanceDefaultDataPath'), '') AS nvarchar(260)),
                       CAST(ISNULL(SERVERPROPERTY('InstanceDefaultLogPath'), '') AS nvarchar(260)),
                       (SELECT physical_name FROM sys.master_files WHERE database_id = 1 AND file_id = 1)
                """).ExecuteReader())
            {
                r.Read();
                var fallback = Path.GetDirectoryName(r.GetString(2))!;
                dataPath = r.GetString(0) is { Length: > 0 } d ? d : fallback;
                logPath = r.GetString(1) is { Length: > 0 } l ? l : fallback;
            }

            // Hedefte aynı file_id'li dosya varsa onun yerine, yoksa varsayılan klasöre <hedef>[_mantıksalad].uzantı
            var moves = new List<(string Logical, string Physical)>();
            using (var r = Command(conn, "RESTORE FILELISTONLY FROM DISK = @file", ("@file", bak)).ExecuteReader())
                while (r.Read())
                {
                    var logical = (string)r["LogicalName"];
                    var fileId = Convert.ToInt32(r["FileId"]);
                    var physical = existing.GetValueOrDefault(fileId) ?? Path.Combine(
                        (string)r["Type"] == "L" ? logPath : dataPath,
                        BackupFiles.SafeName(target) + (fileId == 1 ? "" : "_" + BackupFiles.SafeName(logical)) + Path.GetExtension((string)r["PhysicalName"]));
                    moves.Add((logical, physical));
                }

            var sql = "RESTORE DATABASE @db FROM DISK = @file WITH RECOVERY"
                + (existing.Count > 0 ? ", REPLACE" : "")
                + string.Concat(moves.Select((_, i) => $", MOVE @l{i} TO @p{i}"));
            var parameters = new List<(string, object)> { ("@db", target), ("@file", bak) };
            for (var i = 0; i < moves.Count; i++) { parameters.Add(($"@l{i}", moves[i].Logical)); parameters.Add(($"@p{i}", moves[i].Physical)); }

            if (existing.Count > 0)
                Command(conn, $"ALTER DATABASE {Quote(target)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE").ExecuteNonQuery();
            try
            {
                Command(conn, sql, parameters.ToArray()).ExecuteNonQuery();
            }
            finally
            {
                if (existing.Count > 0)
                    Command(conn, $"IF DB_ID(@db) IS NOT NULL ALTER DATABASE {Quote(target)} SET MULTI_USER", ("@db", target)).ExecuteNonQuery();
            }
            log.Write($"Geri yüklendi: {Path.GetFileName(backupPath)} -> {target}");
        }
        finally
        {
            if (isZip) File.Delete(bak);
        }
    }

    /// <summary>
    /// Zamanlanmış görev SYSTEM hesabıyla çalışır. Windows kimlik doğrulamasında SYSTEM'in seçili veritabanlarını
    /// yedekleyip doğrulayabildiğini EXECUTE AS ile kontrol eder. null: kontrol edilemedi (ör. yetki yok).
    /// </summary>
    public bool? SystemCanBackup(IEnumerable<string> databases)
    {
        try
        {
            using var conn = Open();
            if (Command(conn, "SELECT SUSER_ID(@l)", ("@l", SystemLogin)).ExecuteScalar() is DBNull) return false;
            Command(conn, "EXECUTE AS LOGIN = @l", ("@l", SystemLogin)).ExecuteNonQuery();
            try
            {
                if (settings.Verify && !HasPerm(conn, "master", "CREATE DATABASE")) return false;   // RESTORE VERIFYONLY için
                return databases.All(db => HasPerm(conn, db, "BACKUP DATABASE"));
            }
            finally { Command(conn, "REVERT").ExecuteNonQuery(); }
        }
        catch (SqlException) { return null; }
    }

    private static bool HasPerm(SqlConnection conn, string db, string permission) =>
        Command(conn, "SELECT HAS_PERMS_BY_NAME(@db, 'DATABASE', @p)", ("@db", db), ("@p", permission)).ExecuteScalar() is 1;

    /// <summary>SYSTEM'e en az yetkiyi verir: her veritabanında db_backupoperator, doğrulama için master'da CREATE DATABASE.</summary>
    public void GrantSystemBackup(IEnumerable<string> databases)
    {
        using var conn = Open();
        var user = Quote(SystemLogin);
        Command(conn, $"IF SUSER_ID(@l) IS NULL CREATE LOGIN {user} FROM WINDOWS", ("@l", SystemLogin)).ExecuteNonQuery();
        var ensureUser = $"IF USER_ID(@l) IS NULL CREATE USER {user} FOR LOGIN {user};";
        Command(conn, $"EXEC master.sys.sp_executesql N'{(ensureUser + $" GRANT CREATE DATABASE TO {user};").Replace("'", "''")}', N'@l sysname', @l",
            ("@l", SystemLogin)).ExecuteNonQuery();
        foreach (var db in databases)
            Command(conn, $"EXEC {Quote(db)}.sys.sp_executesql N'{(ensureUser + $" ALTER ROLE db_backupoperator ADD MEMBER {user};").Replace("'", "''")}', N'@l sysname', @l",
                ("@l", SystemLogin)).ExecuteNonQuery();
    }
}
