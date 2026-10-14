# SQL Server gerektirmeyen kısımların testi: powershell -ExecutionPolicy Bypass -File tests\test.ps1
. (Join-Path $PSScriptRoot '..\sql-yedekci.ps1')

$dir = Join-Path ([IO.Path]::GetTempPath()) ('sql-yedekci-test-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $dir | Out-Null
$fails = 0
function Assert($Condition, [string]$Name) {
    if ($Condition) { Write-Host "OK    $Name" } else { Write-Host "HATA  $Name"; $script:fails++ }
}

try {
    Assert ((Get-SafeName 'Satış:2026/Q1') -eq 'Satış_2026_Q1') 'Get-SafeName geçersiz karakterleri değiştirir'

    # Sıkıştırma: .bak silinir, zip içinde aynı içerik olur
    $bak = Join-Path $dir 'Db_20260101_010101.bak'
    Set-Content -Path $bak -Value ('x' * 10000) -NoNewline
    $zip = Compress-Backup $bak
    Assert ((Test-Path $zip) -and -not (Test-Path $bak)) 'Compress-Backup zip oluşturur, .bak siler'
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    Assert ($archive.Entries[0].Name -eq 'Db_20260101_010101.bak' -and $archive.Entries[0].Length -eq 10000) 'Zip içeriği doğru'
    $archive.Dispose()
    Remove-Item $zip

    # Temizlik
    $old = (Get-Date).AddDays(-30)
    $files = @{
        'Satis_20260101_020000.zip' = $old                  # eski -> silinmeli
        'Satis_20260102_020000.zip' = $old                  # eski -> silinmeli
        'Satis_20260301_020000.zip' = Get-Date              # yeni -> kalmalı
        'Stok_20260101_020000.zip'  = $old                  # eski ama tek/en yeni -> kalmalı
        'Stok_20250101_020000.bak'  = $old                  # eski .bak -> silinmeli
        'baska-dosya.zip'           = $old                  # bize ait değil -> kalmalı
    }
    foreach ($f in $files.Keys) { $p = Join-Path $dir $f; Set-Content $p 'x'; (Get-Item $p).LastWriteTime = $files[$f] }
    Remove-OldBackups $dir 14
    $left = (Get-ChildItem $dir).Name | Sort-Object
    Assert (($left -join ',') -eq 'baska-dosya.zip,Satis_20260301_020000.zip,Stok_20260101_020000.zip') "Remove-OldBackups doğru dosyaları siler (kalan: $($left -join ', '))"
} finally {
    Remove-Item $dir -Recurse -Force
}

if ($fails) { Write-Host "$fails test başarısız"; exit 1 }
Write-Host 'Tüm testler geçti'
