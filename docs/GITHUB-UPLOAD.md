# Publish ExaltHelper

Repository: https://github.com/dach6/ExaltHelper

## Build and validate

Use .NET SDK 10.0.401 on Windows with the .NET Framework 4.8 reference assemblies.

```powershell
dotnet build ExaltHelper.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Test-GameAssetBundle.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Test-PackagedAssets.ps1
```

Run the relevant additional suites in [tests/README.md](../tests/README.md).
The game asset test accepts `-SourceDirectory` pointing to the installed game's
`RotMG Exalt_Data` folder to verify its original resource fingerprints and version.

## Update source and publish the release

1. Commit the validated source to `main` and push to the existing repository over HTTPS.
2. Create tag **v1.1.2** at that exact commit and title the release
   **ExaltHelper v1.1.2 - combat tracker / game 7.2.0.1.0**.
3. Use [the prepared release notes](RELEASE-NOTES-1.1.2.md).
4. Attach `ExaltHelper-1.1.2-Release.zip`, `ExaltHelper-1.1.2-Source.zip`, and
   `SHA256SUMS.txt`. Verify uploaded checksums before publishing the stable release.

The application ZIP must contain the executable, configuration, dependency DLLs,
`assets/`, `ExaltHelper_Data/`, notices, and HUD guide. Extract and validate the
ZIP with the packaged-asset tests before publication. The source ZIP should come
from the tagged commit and include the required binary build inputs.

`lib/`, `CompiledResources/`, and `ExaltHelper_Data/` are required build inputs.
The native `ExaltHelper_Data/version.dll` hook is supplied as a binary; its source
is not included. Exclude build intermediates, runtime settings, account data,
diagnostics, and packet captures from both downloads. Do not replace assets of
an existing published release; publish a new version for subsequent updates.
