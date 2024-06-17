Write-Host "Building and packaging the NuGet package..."

# Restore Libraries
dotnet restore

# Build
dotnet build `
	--no-restore `
	--configuration Release `
	./shmtu-dotnet-lib/shmtu-dotnet-lib.csproj

# Remove Old Package
Remove-Item -Path ./Output/shmtu-dotnet-lib.*.nupkg -ErrorAction Ignore

# Pack
dotnet pack `
	--configuration Release `
	--output ./Output `
	./shmtu-dotnet-lib/shmtu-dotnet-lib.csproj

Write-Host "NUGET_KEY: $env:NUGET_KEY"

# Push
nuget push `
	"Output/shmtu-dotnet-lib.1.0.0.1.nupkg" `
	"$env:NUGET_KEY" `
	-Source https://api.nuget.org/v3/index.json
