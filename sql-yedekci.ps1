<#
.SYNOPSIS
    SQL Yedekçi - SQL Server veritabanlarını yedekler, sıkıştırır ve eski yedekleri temizler.

.DESCRIPTION
    Ayarlar config.json dosyasından okunur (bkz. config.example.json).
    Zamanlanmış çalıştırma için Windows Görev Zamanlayıcı kullanılır: -Install / -Uninstall.

.EXAMPLE
    .\sql-yedekci.ps1                         # config.json ile bir kez çalıştırır
    .\sql-yedekci.ps1 -Config D:\yedek.json   # farklı ayar dosyası
    .\sql-yedekci.ps1 -Install -At 02:30      # her gün 02:30'da çalışacak görevi kurar (yönetici)
    .\sql-yedekci.ps1 -Uninstall              # görevi kaldırır (yönetici)
#>
[CmdletBinding()]
param(
    [string]$Config = (Join-Path $PSScriptRoot 'config.json'),
    [switch]$Install,
    [string]$At = '02:00',
    [switch]$Uninstall
)

$Version = '2.1.4'
$TaskName = 'SQL Yedekci'
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$BackupPattern = '_\d{8}_\d{6}\.(zip|bak)$'   # yalnızca bu araçla oluşturulmuş dosyalar

function Write-Log([string]$Message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $Message
    Write-Host $line
    if ($script:LogFile) { Add-Content -Path $script:LogFile -Value $line -Encoding UTF8 }
}

function Get-SafeName([string]$Name) {
    $invalid = [Regex]::Escape(-join [IO.Path]::GetInvalidFileNameChars())
    $Name -replace "[$invalid]", '_'
}

function New-SqlCommand($Connection, [string]$Query) {
    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $Query
    $cmd.CommandTimeout = 0   # sınırsız: büyük veritabanlarının yedeği dakikalar sürebilir
    $cmd
}

function Get-UserDatabases($Connection) {
    $cmd = New-SqlCommand $Connection 'SELECT name FROM sys.databases WHERE database_id > 4 AND state = 0 AND source_database_id IS NULL ORDER BY name'
    $reader = $cmd.ExecuteReader()
    try { while ($reader.Read()) { $reader.GetString(0) } } finally { $reader.Dispose() }
}

function Backup-Database($Connection, [string]$Database, [string]$Dir) {
    $file = Join-Path $Dir ('{0}_{1:yyyyMMdd_HHmmss}.bak' -f (Get-SafeName $Database), (Get-Date))
    $cmd = New-SqlCommand $Connection 'BACKUP DATABASE @db TO DISK = @file WITH INIT, CHECKSUM'
    [void]$cmd.Parameters.AddWithValue('@db', $Database)
    [void]$cmd.Parameters.AddWithValue('@file', $file)
    [void]$cmd.ExecuteNonQuery()
    $file
}

function Compress-Backup([string]$BakPath) {
    $zipPath = [IO.Path]::ChangeExtension($BakPath, '.zip')
    try {
        # ZipArchive Zip64 destekler; Compress-Archive (PS 5.1) 2 GB üzerinde hata verir
        $zip = [IO.Compression.ZipFile]::Open($zipPath, 'Create')
        try { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $BakPath, (Split-Path $BakPath -Leaf), 'Optimal') }
        finally { $zip.Dispose() }
    } catch {
        Remove-Item $zipPath -ErrorAction SilentlyContinue   # yarım zip kalmasın, .bak korunur
        throw
    }
    Remove-Item $BakPath
    $zipPath
}

function Remove-OldBackups([string]$Dir, [int]$Days) {
    if ($Days -le 0) { return }
    $limit = (Get-Date).AddDays(-$Days)
    Get-ChildItem -Path $Dir -File |
        Where-Object { $_.Name -match $BackupPattern } |
        Group-Object { $_.Name -replace $BackupPattern } |
        ForEach-Object {
            # her veritabanının en yeni yedeği yaşına bakılmadan korunur (yedekler uzun süre başarısız olsa bile)
            $_.Group | Sort-Object Name -Descending | Select-Object -Skip 1 |
                Where-Object { $_.LastWriteTime -lt $limit } |
                ForEach-Object { Remove-Item $_.FullName; Write-Log "Silindi: $($_.Name)" }
        }
}

function Read-Config([string]$Path) {
    if (-not (Test-Path $Path)) { throw "Ayar dosyası bulunamadı: $Path (config.example.json dosyasını config.json olarak kopyalayın)" }
    $cfg = Get-Content $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $cfg.ConnectionString) { throw 'ConnectionString boş olamaz.' }
    if (-not $cfg.BackupDir) { throw 'BackupDir boş olamaz.' }
    if ($null -eq $cfg.RetentionDays) { $cfg | Add-Member RetentionDays 14 }
    if ($null -eq $cfg.Compress) { $cfg | Add-Member Compress $true }
    $cfg
}

function Start-Backup($Cfg) {
    New-Item -ItemType Directory -Force -Path $Cfg.BackupDir | Out-Null
    $script:LogFile = Join-Path $Cfg.BackupDir ('sql-yedekci_{0:yyyyMM}.log' -f (Get-Date))
    Write-Log "SQL Yedekçi $Version başladı"

    $failed = 0
    $conn = New-Object System.Data.SqlClient.SqlConnection $Cfg.ConnectionString
    try {
        $conn.Open()
        $databases = @($Cfg.Databases | Where-Object { $_ })
        if (-not $databases) { $databases = @(Get-UserDatabases $conn) }
        if (-not $databases) { Write-Log 'Yedeklenecek veritabanı yok.' }

        foreach ($db in $databases) {
            try {
                $started = Get-Date
                $file = Backup-Database $conn $db $Cfg.BackupDir
                if ($Cfg.Compress) { $file = Compress-Backup $file }
                $sizeMb = (Get-Item $file).Length / 1MB
                Write-Log ('OK    {0} -> {1} ({2:N1} MB, {3:N0} sn)' -f $db, (Split-Path $file -Leaf), $sizeMb, ((Get-Date) - $started).TotalSeconds)
            } catch {
                $failed++
                Write-Log "HATA  $db : $($_.Exception.Message)"
            }
        }
    } finally { $conn.Dispose() }

    Remove-OldBackups $Cfg.BackupDir $Cfg.RetentionDays
    Write-Log ('Bitti: {0} veritabanı, {1} hata' -f $databases.Count, $failed)
    $failed
}

function Install-Task([string]$ConfigPath, [string]$Time) {
    $configFull = (Resolve-Path $ConfigPath).Path
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Config `"$configFull`""
    $trigger = New-ScheduledTaskTrigger -Daily -At $Time
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -User 'SYSTEM' -RunLevel Highest -Force | Out-Null
    Write-Host "Görev kuruldu: '$TaskName' her gün $Time (SYSTEM hesabıyla)"
}

# Testler fonksiyonları kullanmak için dosyayı nokta ile yükler (. .\sql-yedekci.ps1); o durumda çalıştırma
if ($MyInvocation.InvocationName -eq '.') { return }

try {
    if ($Uninstall) { Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false; Write-Host "Görev kaldırıldı: '$TaskName'"; exit 0 }
    $cfg = Read-Config $Config
    if ($Install) { Install-Task $Config $At; exit 0 }
    $failed = Start-Backup $cfg
    exit [int]($failed -gt 0)
} catch {
    Write-Log "HATA  $($_.Exception.Message)"
    exit 1
}
