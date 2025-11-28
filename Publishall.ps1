# This script publishes tha project into .\publish\net10.0-windows for x86, x64 and arm64
# and both self-contained and framework-dependent, and makes zip files for each

$proj = [xml](Get-Content .\mobzquery.csproj)
$version = [System.Version]::new($proj.Project.PropertyGroup.Version)
$versionString = $version.ToString(3)

Write-Output "Version detected: $version"

$modes = @('Self-contained', 'Framework-dependent')
$runtimes = @('x86', 'x64', 'arm64')

foreach ($mode in $modes) {
	foreach ($runtime in $runtimes) {
		# Build the version:
		Write-Output "Building $runtime ($mode)..."
		dotnet publish mobzquery.csproj /p:PublishProfile=$mode-win-$runtime
		# Create zip-file:
		Write-Output "Compressing $runtime ($mode)..."
		Compress-Archive ".\publish\net10.0-windows\$mode\win-$runtime" ".\publish\mobzquery.$versionString-$runtime-$($mode.ToLower()).zip" -CompressionLevel Optimal -Force
	}
}
