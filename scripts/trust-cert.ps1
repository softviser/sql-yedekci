<#
.SYNOPSIS
    Softviser Yazılım self-signed sertifikasını bu bilgisayarda güvenilir yapar.

.DESCRIPTION
    softviser-codesign.cer dosyasını "Güvenilen Kök Sertifika Yetkilileri" ve
    "Güvenilen Yayımcılar" depolarına ekler. Böylece imzalı SQL Yedekçi dosyaları
    "Bilinmeyen yayımcı" yerine "Softviser Yazılım" olarak görünür.

    Yalnızca sertifikanın gerçekten Softviser Yazılım'a ait olduğunu doğruladıysanız çalıştırın
    (parmak izini GitHub sürüm notlarındaki değerle karşılaştırın).

    -Scope LocalMachine (varsayılan) yönetici yetkisi ister ve tüm kullanıcılar için geçerlidir.
    -Scope CurrentUser yalnızca geçerli kullanıcı içindir (kök ekleme sırasında Windows onay penceresi açar).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File trust-cert.ps1 -CerPath .\softviser-codesign.cer
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File trust-cert.ps1 -Remove
#>
[CmdletBinding()]
param(
    [string]$CerPath,
    [ValidateSet('LocalMachine', 'CurrentUser')][string]$Scope = 'LocalMachine',
    [switch]$Remove
)
$ErrorActionPreference = 'Stop'
if (-not $CerPath) { $CerPath = Join-Path $PSScriptRoot 'softviser-codesign.cer' }

if (-not (Test-Path $CerPath)) {
    $alt = Join-Path $PSScriptRoot '..\certs\softviser-codesign.cer'
    if (Test-Path $alt) { $CerPath = $alt } else { throw "Sertifika bulunamadı: $CerPath" }
}

if ($Scope -eq 'LocalMachine') {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) { throw 'LocalMachine kapsamı için PowerShell''i "Yönetici olarak çalıştır" ile açın veya -Scope CurrentUser kullanın.' }
}

$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2((Resolve-Path $CerPath).Path)
Write-Host "Sertifika : $($cert.Subject)"
Write-Host "Parmak izi: $($cert.Thumbprint)"

foreach ($store in 'Root', 'TrustedPublisher') {
    $location = "Cert:\$Scope\$store"
    if ($Remove) {
        Get-ChildItem $location | Where-Object Thumbprint -eq $cert.Thumbprint | Remove-Item
        Write-Host "Kaldırıldı: $location"
    } else {
        Import-Certificate -FilePath $CerPath -CertStoreLocation $location | Out-Null
        Write-Host "Eklendi   : $location"
    }
}

