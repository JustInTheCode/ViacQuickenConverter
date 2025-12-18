param (
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$projectPath = ".\ViacQuickenConverter"
$publishRoot = ".\download\$Version"
$solutionRoot = Get-Location
$targets = @(
    @{ Runtime = "win-x64"; Output = "$publishRoot\win-x64" },
    @{ Runtime = "win-x86"; Output = "$publishRoot\win-x86" },
    @{ Runtime = "win-arm64"; Output = "$publishRoot\win-arm64" },
    @{ Runtime = "linux-x64"; Output = "$publishRoot\linux-x64" }
    @{ Runtime = "linux-arm64"; Output = "$publishRoot\linux-arm64" }
    @{ Runtime = "linux-arm"; Output = "$publishRoot\linux-arm" }
)

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
foreach ($target in $targets) {
    $runtime = $target.Runtime
    $outputPath = $target.Output
    if (Test-Path $outputPath) { Remove-Item $outputPath -Recurse -Force }

    Write-Host "Publishing for $runtime..."
    dotnet publish $projectPath `
        -r $runtime `
        -c Release `
        -o $outputPath

    Get-ChildItem -Path $outputPath -Filter *.pdb -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

    foreach ($file in @("README.md", "CHANGELOG.md", "LICENSE")) {
        $sourcePath = Join-Path $solutionRoot $file
        $destPath = Join-Path $outputPath $file
        if (-not (Test-Path $sourcePath)) {
            Write-Error "Missing required file in solution root: $sourcePath"
            exit 1
        }
        Copy-Item -Path $sourcePath -Destination $destPath -Force
    }

    $zipPath = "$publishRoot\$runtime.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

    foreach ($requiredFile in @("README.md", "CHANGELOG.md", "LICENSE")) {
        $requiredFilePath = "$outputPath\$requiredFile"
        if (-not (Test-Path $requiredFilePath)) {
            Write-Error "Missing required file: $requiredFilePath"
            exit 1
        }
    }

    Compress-Archive -Path "$outputPath\*" -DestinationPath $zipPath

    Write-Host "Done: $runtime.zip"
}

Write-Host "All targets published and zipped successfully"