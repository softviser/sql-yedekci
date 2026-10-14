<#
.SYNOPSIS
    Softviser Yazılım adına kendinden imzalı (self-signed) kod imzalama sertifikası üretir.

.DESCRIPTION
    - Sertifikayı CurrentUser\My deposunda oluşturur.
    - certs\softviser-codesign.pfx (özel anahtarlı, parolalı) ve
      certs\softviser-codesign.cer (yalnızca açık anahtar, dağıtılabilir) dosyalarını yazar.
    - PFX dosyasını ASLA depoya göndermeyin (.gitignore bunu engeller).

.EXAMPLE
    .\scripts\new-selfsigned-cert.ps1 -Password (Read-Host -AsSecureString "PFX parolası")
#>
[CmdletBinding()]
param(
    [SecureString]$Password,
    [string]$Subject = 'CN=Softviser Yazılım, O=Softviser Yazılım, C=TR',
    [string]$OutDir,
    [int]$Years = 5
)
$ErrorActionPreference = 'Stop'
if (-not $OutDir) { $OutDir = Join-Path $PSScriptRoot '..\certs' }
if (-not $Password) { $Password = Read-Host -AsSecureString 'PFX parolası' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -FriendlyName 'Softviser Yazılım Kod İmzalama (Self-Signed)' `
    -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 `
    -KeyExportPolicy Exportable `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -NotAfter (Get-Date).AddYears($Years)

$pfx = Join-Path $OutDir 'softviser-codesign.pfx'
$cer = Join-Path $OutDir 'softviser-codesign.cer'
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $Password | Out-Null
Export-Certificate -Cert $cert -FilePath $cer | Out-Null

Write-Host "Sertifika oluşturuldu"
Write-Host "  Konu        : $($cert.Subject)"
Write-Host "  Parmak izi  : $($cert.Thumbprint)"
Write-Host "  Geçerlilik  : $($cert.NotAfter)"
Write-Host "  PFX (gizli) : $pfx"
Write-Host "  CER (genel) : $cer"
Write-Host ""
Write-Host "GitHub Actions için PFX'i base64 olarak secret'a eklemek isterseniz:"
Write-Host "  [Convert]::ToBase64String([IO.File]::ReadAllBytes('$pfx')) | Set-Clipboard"
