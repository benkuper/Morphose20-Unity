param([string]$UnityEditorPath)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $UnityEditorPath) {
    $versionLine = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion: '
    $version = ($versionLine.ToString() -split ': ', 2)[1]
    $UnityEditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor"
}
$editorData = Join-Path $UnityEditorPath 'Data'
$dotnet = Join-Path $editorData 'NetCoreRuntime/dotnet.exe'
$compiler = Join-Path $editorData 'DotNetSdkRoslyn/csc.dll'
$runtime = Get-ChildItem -LiteralPath (Join-Path $editorData 'NetCoreRuntime/shared/Microsoft.NETCore.App') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$outputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('PhotoCameraFitTests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $outputDirectory | Out-Null
$outputAssembly = Join-Path $outputDirectory 'PhotoCameraFitTests.dll'
$response = @('-nostdlib+', '-target:exe', '-nologo', '-langversion:9.0', ('-out:"' + $outputAssembly + '"'))
Get-ChildItem -LiteralPath $runtime.FullName -Filter '*.dll' | Where-Object { $_.Name -match '^(System\.|mscorlib\.|netstandard\.)' -and $_.Name -notmatch '\.Native\.dll$' } | ForEach-Object { $response += '-r:"' + $_.FullName + '"' }
$response += '"' + (Join-Path $projectRoot 'Assets/Scripts/Editor/PhotoCameraFit.cs') + '"'
$response += '"' + (Join-Path $PSScriptRoot 'PhotoCameraFitTests.cs') + '"'
$responsePath = Join-Path $outputDirectory 'compile.rsp'
$response | Set-Content -LiteralPath $responsePath -Encoding UTF8
& $dotnet $compiler '-noconfig' ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
@{ runtimeOptions = @{ framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime.Name } } } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputDirectory 'PhotoCameraFitTests.runtimeconfig.json') -Encoding UTF8
& $dotnet $outputAssembly
exit $LASTEXITCODE
