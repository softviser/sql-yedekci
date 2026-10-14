<#
.SYNOPSIS
    Dosyaları Authenticode ile imzalar (PFX dosyasıyla).

.DESCRIPTION
    Windows SDK / signtool gerekmez; PowerShell'in Set-AuthenticodeSignature komutunu kullanır.
    Inno Setup kurulum dosyasını imzalamak için de bu betik çağrılır.

.EXAMPLE
    .\scripts\sign.ps1 -Files artifacts\publish\SqlYedekci.exe -PfxPath certs\softviser-codesign.pfx -Password $env:CODESIGN_PFX_PASSWORD
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string[]]$Files,
    [string]$PfxPath,
    [string]$Password = $env:CODESIGN_PFX_PASSWORD,
    [string]$TimestampServer = 'http://timestamp.digicert.com',
    [switch]$NoTimestamp
)
$ErrorActionPreference = 'Stop'
if (-not $PfxPath) { $PfxPath = Join-Path $PSScriptRoot '..\certs\softviser-codesign.pfx' }

if (-not (Test-Path $PfxPath)) { throw "PFX bulunamadı: $PfxPath  (önce scripts\new-selfsigned-cert.ps1 çalıştırın)" }
$flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2((Resolve-Path $PfxPath).Path, $Password, $flags)
if (-not $cert.HasPrivateKey) { throw 'PFX içinde özel anahtar yok.' }

foreach ($file in $Files) {
    $path = (Resolve-Path $file).Path
    $sigArgs = @{ FilePath = $path; Certificate = $cert; HashAlgorithm = 'SHA256' }
    if (-not $NoTimestamp) { $sigArgs.TimestampServer = $TimestampServer }

    try {
        $result = Set-AuthenticodeSignature @sigArgs
        if (-not $result.SignerCertificate) { throw $result.StatusMessage }
    } catch {
        if ($NoTimestamp) { throw "İmzalama başarısız: $path — $_" }
        Write-Warning "Zaman damgası alınamadı ($TimestampServer), damgasız imzalanıyor: $path"
        $sigArgs.Remove('TimestampServer')
        $result = Set-AuthenticodeSignature @sigArgs
        if (-not $result.SignerCertificate) { throw "İmzalama başarısız: $path — $($result.StatusMessage)" }
    }
    # Self-signed kök güvenilir değilse Status 'UnknownError' olur; imza yine de dosyaya eklenmiştir.
    Write-Host "İmzalandı: $path  [$($result.Status)]"
}


