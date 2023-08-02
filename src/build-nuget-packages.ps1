Param(
[Parameter(Mandatory=$True)]
[string]$versionSuffix
)
cd .\NLog.Targets.Splunk
Write-Output "Building release $versionSuffix nuget packages..."
Write-Output "Restoring NuGet packages..."
dotnet restore
dotnet pack --configuration Release --include-symbols --version-suffix $versionSuffix
Write-Output "Moving $versionSuffix nuget packages to releases folder..."
Move-Item .\bin\Release\*.nupkg ..\..\releases -Force
Write-Output "Done."
