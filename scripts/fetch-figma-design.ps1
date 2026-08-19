param(
    [string]$FileKey = "IfOF27YvqWa67OoDvhcrWD",
    [string]$OutDir = "docs/design"
)

# Atualiza o snapshot do design do Figma (REST API).
# Requer a variável de ambiente FIGMA_PERSONAL_ACCESS_TOKEN (nunca gravar o token no repositório).
# Uso:  powershell -ExecutionPolicy Bypass -File scripts/fetch-figma-design.ps1
# O snapshot NÃO contém o token; apenas dados do design (nomes, textos, cores, tipografia).

$ErrorActionPreference = "Stop"
$token = [Environment]::GetEnvironmentVariable("FIGMA_PERSONAL_ACCESS_TOKEN", "User")
if (-not $token) {
    $token = $env:FIGMA_PERSONAL_ACCESS_TOKEN
}
if (-not $token) {
    Write-Error "Variável FIGMA_PERSONAL_ACCESS_TOKEN não definida. Configure antes: setx FIGMA_PERSONAL_ACCESS_TOKEN ""seu_token"""
    exit 1
}

$headers = @{ "X-Figma-Token" = $token }
$baseUrl = "https://api.figma.com/v1"

Write-Host "Baixando arquivo $FileKey ..."
$file = Invoke-RestMethod -Uri "$baseUrl/files/$FileKey" -Headers $headers -Method Get -TimeoutSec 120

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$jsonPath = Join-Path $OutDir "figma-snapshot.json"
[System.IO.File]::WriteAllText($jsonPath, ($file | ConvertTo-Json -Depth 100), [System.Text.Encoding]::UTF8)
Write-Host "JSON salvo: $jsonPath ($([math]::Round((Get-Item $jsonPath).Length/1KB,1)) KB)"

function Get-FillHex {
    param($node)
    if (-not $node.fills) { return "" }
    $f = $node.fills | Where-Object { $_.visible -ne $false } | Select-Object -First 1
    if ($f -and $f.color) {
        return "#{0:X2}{1:X2}{2:X2}" -f [int]([math]::Round($f.color.r * 255)), [int]([math]::Round($f.color.g * 255)), [int]([math]::Round($f.color.b * 255))
    }
    return ""
}

function Get-TypeSpec {
    param($node)
    if (-not $node.style) { return "" }
    $fs = $node.style.fontSize
    $fw = $node.style.fontWeight
    return "$fs px / $fw"
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Snapshot do Design - PasswordManager")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("> Gerado automaticamente por scripts/fetch-figma-design.ps1. Regenerar apos alteracoes no Figma.")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("Arquivo: $($file.name)  |  Versao: $($file.version)  |  Modificado: $($file.lastModified)")
[void]$sb.AppendLine("")

function Show-Node {
    param($node, $depth)
    $indent = "  " * $depth
    $fill = Get-FillHex $node
    $ts = Get-TypeSpec $node
    $extra = ""
    if ($node.characters) { $extra += "  texto=`"$($node.characters)`"" }
    if ($fill) { $extra += "  cor=$fill" }
    if ($ts) { $extra += "  tipografia=[$ts]" }
    if ($node.layoutMode) { $extra += "  layout=$($node.layoutMode)" }
    if ($node.opacity -ne $null -and $node.opacity -lt 1) { $extra += "  opacidade=$($node.opacity)" }
    [void]$sb.AppendLine("$indent- **$($node.type)**: $($node.name)$extra")
    if ($node.children) {
        foreach ($c in $node.children) { Show-Node $c ($depth + 1) }
    }
}

[void]$sb.AppendLine("## Estrutura")
[void]$sb.AppendLine("")
foreach ($canvas in $file.document.children) {
    [void]$sb.AppendLine("### Canvas: $($canvas.name)")
    [void]$sb.AppendLine("")
    foreach ($frame in $canvas.children) { Show-Node $frame 1 }
    [void]$sb.AppendLine("")
}

$mdPath = Join-Path $OutDir "figma-snapshot.md"
[System.IO.File]::WriteAllText($mdPath, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Host "Markdown salvo: $mdPath"

Write-Host "Done."