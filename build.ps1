<#
.SYNOPSIS
    SQL Yedekçi'yi derler, (isteğe bağlı) imzalar; taşınabilir ZIP ve kurulum dosyası üretir.

.EXAMPLE
    .\build.ps1                         # imzasız derleme + portable zip (+ Inno Setup varsa kurulum)
.EXAMPLE
    $env:CODESIGN_PFX_PASSWORD = '...'; .\build.ps1 -Sign -Version 3.0.1
#>
[CmdletBinding()]
param(
    [string]$Version = '3.0.1',
    [switch]$Sign,
    [string]$PfxPath,
    [string]$PfxPassword = $env:CODESIGN_PFX_PASSWORD,
    [switch]$NoTimestamp,
    [switch]$NoInstaller,
    [switch]$FrameworkDependent
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $PfxPath) { $PfxPath = Join-Path $root 'certs\softviser-codesign.pfx' }
$artifacts = Join-Path $root 'artifacts'
$publish = Join-Path $artifacts 'publish'
$rid = 'win-x64'

Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publish | Out-Null

# Derleme (tek dosya exe)
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
dotnet publish (Join-Path $root 'src\SqlYedekci\SqlYedekci.csproj') `
    -c Release -r $rid --self-contained $selfContained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=$selfContained `
    -p:DebugType=none `
    -p:Version=$Version `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish başarısız.' }
$exe = Join-Path $publish 'SqlYedekci.exe'

# İmzalama
function Invoke-Sign([string[]]$files) {
    $signArgs = @{ Files = $files; PfxPath = $PfxPath; Password = $PfxPassword }
    if ($NoTimestamp) { $signArgs.NoTimestamp = $true }
    & (Join-Path $root 'scripts\sign.ps1') @signArgs
}
if ($Sign) { Invoke-Sign @($exe) }

# Taşınabilir ZIP
$portableDir = Join-Path $artifacts 'portable'
New-Item -ItemType Directory -Force -Path $portableDir | Out-Null
Copy-Item $exe, (Join-Path $root 'LICENSE'), (Join-Path $root 'README.md') $portableDir
$cer = Join-Path $root 'certs\softviser-codesign.cer'
if (Test-Path $cer) {
    Copy-Item $cer, (Join-Path $root 'scripts\trust-cert.ps1') $portableDir
}
$zip = Join-Path $artifacts "SqlYedekci-$Version-portable-$rid.zip"
Compress-Archive -Path (Join-Path $portableDir '*') -DestinationPath $zip -Force
Write-Host "Taşınabilir paket: $zip"

# Kurulum (Inno Setup)
if (-not $NoInstaller) {
    $iscc = @(
        (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source,
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

    if (-not $iscc) {
        Write-Warning 'Inno Setup 6 bulunamadı; kurulum dosyası atlandı. (winget install JRSoftware.InnoSetup)'
    } else {
        $isccArgs = @("/DAppVersion=$Version", "/DPublishDir=$publish", "/O$artifacts")
        if (Test-Path $cer) { $isccArgs += "/DCertFile=$cer" }
        & $iscc @isccArgs (Join-Path $root 'installer\SqlYedekci.iss')
        if ($LASTEXITCODE -ne 0) { throw 'Inno Setup derlemesi başarısız.' }
        $setup = Join-Path $artifacts "SqlYedekci-$Version-setup-$rid.exe"
        if ($Sign) { Invoke-Sign @($setup) }
        Write-Host "Kurulum dosyası : $setup"
    }
}

# Sağlama toplamları
Remove-Item $portableDir -Recurse -Force
$sums = Get-ChildItem $artifacts -File | ForEach-Object {
    "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name
}
Set-Content -Path (Join-Path $artifacts 'SHA256SUMS.txt') -Value $sums -Encoding ASCII
if (Test-Path $cer) { Copy-Item $cer $artifacts }
Write-Host "`nÇıktılar:"; Get-ChildItem $artifacts -File | Format-Table Name, Length -AutoSize

