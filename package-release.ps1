param(
  [string]$Dotnet = 'dotnet'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$project = Join-Path $PSScriptRoot 'MiniDeepCoolDigital/MiniDeepCoolDigital.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$versionNode = $projectXml.SelectSingleNode('/Project/PropertyGroup/Version')
$frameworkNode = $projectXml.SelectSingleNode('/Project/PropertyGroup/TargetFramework')
$version = if ($null -eq $versionNode) { '' } else { $versionNode.InnerText }
$framework = if ($null -eq $frameworkNode) { '' } else { $frameworkNode.InnerText }
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
  throw "项目版本号无效：$version"
}
if ([string]::IsNullOrWhiteSpace($framework)) { throw '项目缺少 TargetFramework' }

$bin = Join-Path $PSScriptRoot 'MiniDeepCoolDigital/bin'
$output = Join-Path $bin "Release/$framework/win-x64"
$zip = Join-Path $bin "MiniDeepCoolDigital-$version-x64.zip"
$temporaryZip = "$zip.partial.zip"

Push-Location $PSScriptRoot
try {
  & $Dotnet build $project -c Release -r win-x64 --no-self-contained -o $output
  if ($LASTEXITCODE -ne 0) { throw "Release 构建失败，退出码：$LASTEXITCODE" }

  $exe = Join-Path $output 'MiniDeepCoolDigital.exe'
  foreach ($required in @($exe, (Join-Path $output 'MiniDeepCoolDigital.dll'),
      (Join-Path $output 'MiniDeepCoolDigital.deps.json'),
      (Join-Path $output 'MiniDeepCoolDigital.runtimeconfig.json'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "构建产物缺失：$required" }
  }
  $productVersion = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
  if (-not $productVersion.StartsWith($version, [StringComparison]::Ordinal)) {
    throw "EXE 版本不匹配：$productVersion"
  }

  if (Test-Path -LiteralPath $temporaryZip) { Remove-Item -LiteralPath $temporaryZip -Force }
  try {
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
      $output, $temporaryZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    $archive = [System.IO.Compression.ZipFile]::Open($temporaryZip, 'Update')
    try {
      [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $archive, (Join-Path $PSScriptRoot 'README.md'), 'README.md') | Out-Null
    }
    finally { $archive.Dispose() }
    Move-Item -LiteralPath $temporaryZip -Destination $zip -Force
  }
  finally {
    if (Test-Path -LiteralPath $temporaryZip) { Remove-Item -LiteralPath $temporaryZip -Force }
  }
  Write-Output $zip
}
finally { Pop-Location }
