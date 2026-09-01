param(
    [string]$PublishDir = "publish",
    [string]$Output = "installer/AppFiles.wxs"
)
$ErrorActionPreference = "Stop"
$publishDir = (Resolve-Path $PublishDir).Path
Write-Host "Gerando $Output a partir de $publishDir"
$files = Get-ChildItem -Path $publishDir -File -Recurse | Sort-Object FullName
# Mapa de diretórios relativos
$dirs = @{}
foreach ($f in $files) {
    $relDir = Split-Path -Parent $f.FullName.Substring($publishDir.Length + 1) -ErrorAction SilentlyContinue
    if ([string]::IsNullOrEmpty($relDir)) { $relDir = "" }
    $dirs[$relDir] = $true
}
# Gerar wxs
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<?xml version="1.0" encoding="UTF-8"?>')
[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <ComponentGroup Id="AppFiles" Directory="INSTALLFOLDER">')
foreach ($f in $files) {
    $rel = $f.FullName.Substring($publishDir.Length + 1)
    $relDir = Split-Path -Parent $rel -ErrorAction SilentlyContinue
    $dirId = if ([string]::IsNullOrEmpty($relDir)) { "INSTALLFOLDER" } else { "dir_" + ($relDir -replace '[^a-zA-Z0-9]', '_') }
    $id = "cmp_" + ($rel -replace '[^a-zA-Z0-9]', '_')
    # Evitar ids muito longos ou duplicados - usar GUID estável baseado no path
    $guid = [guid]::NewGuid().ToString().ToUpper()
    $fileId = "fil_" + ($rel -replace '[^a-zA-Z0-9]', '_')
    $source = '$(var.PublishDir)\' + $rel
    if ([string]::IsNullOrEmpty($relDir)) {
        [void]$sb.AppendLine("      <Component Id=`"$id`" Guid=`"$guid`">")
        [void]$sb.AppendLine("        <File Id=`"$fileId`" Source=`"$source`" KeyPath=`"yes`" />")
        [void]$sb.AppendLine("      </Component>")
    } else {
        [void]$sb.AppendLine("      <Component Id=`"$id`" Directory=`"$dirId`" Guid=`"$guid`">")
        [void]$sb.AppendLine("        <File Id=`"$fileId`" Source=`"$source`" KeyPath=`"yes`" />")
        [void]$sb.AppendLine("      </Component>")
    }
}
[void]$sb.AppendLine('    </ComponentGroup>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('</Wix>')
$dir = Split-Path -Parent $Output
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
Set-Content -LiteralPath $Output -Value $sb.ToString() -Encoding UTF8
Write-Host "AppFiles.wxs gerado com $($files.Count) arquivos"
