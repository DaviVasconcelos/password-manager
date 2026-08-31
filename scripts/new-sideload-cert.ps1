#Requires -Version 5.1
param(
    [string]$Publisher = "CN=PasswordManager",
    [int]$YearsValid = 5,
    [string]$Password = ""
)
$ErrorActionPreference = "Stop"
$projectDir = Join-Path $PSScriptRoot "..\src\PasswordManager.UI"
$projectDir = (Resolve-Path $projectDir).Path
$manifestPath = Join-Path $projectDir "Package.appxmanifest"
$csprojPath = Join-Path $projectDir "PasswordManager.UI.csproj"
if (-not (Test-Path $manifestPath)) { throw "Manifest nao encontrado: $manifestPath" }
[xml]$manifest = Get-Content $manifestPath
$manifestPublisher = $manifest.Package.Identity.Publisher
if ($manifestPublisher -ne $Publisher) {
    Write-Warning "Publisher no manifest e '$manifestPublisher' mas cert sera '$Publisher'. Atualize o manifest."
}
$existentes = Get-ChildItem "Cert:\CurrentUser\My" | Where-Object { $_.Subject -eq $Publisher }
if ($existentes) {
    Write-Host "Certificados existentes com Subject $Publisher encontrados: $($existentes.Count)" -ForegroundColor Yellow
}
$notAfter = (Get-Date).AddYears($YearsValid)
Write-Host "Gerando certificado $Publisher valido ate $notAfter ..." -ForegroundColor Cyan
$cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature -FriendlyName "PasswordManager Sideload ($Publisher)" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") -NotAfter $notAfter -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256
$thumb = $cert.Thumbprint
Write-Host "Thumbprint: $thumb" -ForegroundColor Green
Write-Host "Subject: $($cert.Subject) | NotAfter: $($cert.NotAfter)" -ForegroundColor Green
$cerPath = Join-Path $projectDir "PasswordManager.cer"
Export-Certificate -Cert "Cert:\CurrentUser\My\$thumb" -FilePath $cerPath -Force | Out-Null
Write-Host "Cer exportado: $cerPath" -ForegroundColor Green
$pfxPath = Join-Path $projectDir "PasswordManager_Sideload.pfx"
if ([string]::IsNullOrEmpty($Password)) {
    $securePwd = New-Object System.Security.SecureString
} else {
    $securePwd = ConvertTo-SecureString -String $Password -Force -AsPlainText
}
Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$thumb" -FilePath $pfxPath -Password $securePwd -Force | Out-Null
Write-Host "Pfx exportado: $pfxPath" -ForegroundColor Green
if ([string]::IsNullOrEmpty($Password)) {
    Write-Host "  (sem senha)" -ForegroundColor Yellow
}
if (Test-Path $csprojPath) {
    $csproj = Get-Content $csprojPath -Raw
    $pattern = "<PackageCertificateThumbprint>.*?</PackageCertificateThumbprint>"
    if ($csproj -match $pattern) {
        $novo = "<PackageCertificateThumbprint>$thumb</PackageCertificateThumbprint>"
        $csproj = $csproj -replace $pattern, $novo
        Set-Content -LiteralPath $csprojPath -Value $csproj -NoNewline
        Write-Host "csproj atualizado com novo thumbprint." -ForegroundColor Green
    } else {
        Write-Warning "Tag PackageCertificateThumbprint nao encontrada no csproj"
    }
    if ($csproj -notmatch "PackageCertificateKeyFile") {
        Write-Host "Dica: adicione PackageCertificateKeyFile no csproj para builds Release locais." -ForegroundColor Yellow
    }
}
Write-Host ""
Write-Host "Proximos passos:" -ForegroundColor Cyan
Write-Host "  1. dotnet build src/PasswordManager.UI/PasswordManager.UI.csproj -c Release -p:Platform=x64"
Write-Host "  2. Para testar: instalar .cer em Usuario atual > Pessoas Confiaveis, depois duplo clique no .msix"
Write-Host "  3. Ou execute Install.ps1 em AppPackages como Administrador."
Write-Host "  Nao commite o .pfx (gitignore *.pfx). Em CI o cert e gerado automaticamente."
