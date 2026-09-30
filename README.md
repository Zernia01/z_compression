# z_compression

z_compression is a privacy-friendly Windows desktop archive utility built with C#, .NET 10, WPF, MVVM-style presentation, and a replaceable archive-engine interface. Files stay on the local computer: the application has no analytics, advertising, account system, or file-upload feature.

## What works

- Create and extract ZIP, 7Z, TAR, and TAR.GZ archives with streaming I/O.
- Read and extract ZIP, 7Z, RAR/RAR5, TAR, GZip, BZip2, XZ, and Zstandard formats supported by SharpCompress.
- Browse archive entries, search, sort, test checksums, drag and drop, monitor progress, and cancel between entries.
- Preserve Korean, English, Japanese, Simplified/Traditional Chinese, emoji, and mixed-language names.
- Choose legacy ZIP filename encodings through the core encoding service (CP949, EUC-KR, CP932, GBK, GB18030, and Big5).
- Block absolute paths, `..` traversal, and extraction through existing reparse-point ancestors. Expanded-size and entry-count limits reduce archive-bomb risk.
- Switch among Korean, English, Japanese, Simplified Chinese, Traditional Chinese, and a separate Korean mixed-script resource. Light, dark, and system themes are saved per user.
- Store settings as UTF-8 JSON in `%LOCALAPPDATA%\z_compression` and recover safely from malformed settings.
- Check GitHub Releases and verify downloaded update packages using manifest size and SHA-256 before the separate updater replaces files.

## Deliberate limitations in 1.0

SharpCompress 0.50.4 does not expose encrypted archive creation through its writers, so password-protected archives can be read but are not created. RAR creation is intentionally unavailable. XZ, TAR.ZST, and standalone BZ2 creation are disabled because the selected engine does not provide safe matching writers. Split archives, archive mutation, Explorer context-menu commands, and pause/resume are not exposed in the UI yet. No button is presented for those unavailable operations.

## Requirements and installation

Windows 11 is the primary target. A framework-dependent developer build requires the .NET 10 Desktop Runtime. Release artifacts are self-contained and do not require a separate runtime.

- Portable: download the ZIP for `win-x64` or `win-arm64`, extract it, and run `z_compression.exe`.
- Installer: run the Inno Setup installer and optionally select z_compression for supported archive extensions in the Windows Default Apps page opened at the end. The definition is in `installer/z_compression.iss`.
- The same Default Apps page can be opened later from Settings > General > Windows Default Apps.

## Build and test

Install the .NET 10 SDK, then run:

```powershell
dotnet restore z_compression.slnx --configfile NuGet.Config
dotnet build z_compression.slnx -c Release --no-restore
dotnet test tests/ZCompression.Tests/ZCompression.Tests.csproj -c Release --no-build
```

Create a self-contained portable build:

```powershell
dotnet publish src/ZCompression.App/ZCompression.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

Replace `win-x64` with `win-arm64` for ARM64. The GitHub Actions release workflow performs restore, build, tests, publishing, ZIP packaging, checksums, and release upload when a semantic tag such as `v1.0.0` is pushed.

## Project structure

- `src/ZCompression.App`: WPF UI, localization resources, archive browsing and operations.
- `src/ZCompression.Core`: archive abstraction/engine, secure extraction, settings, and encoding support.
- `src/ZCompression.Update`: GitHub release discovery, download, and SHA-256 verification.
- `src/ZCompression.Updater`: rollback-aware out-of-process file replacement.
- `tests/ZCompression.Tests`: ZIP/7Z Unicode round trips, Zip Slip defense, cancellation, settings, localization, versions, and hashes.
- `installer`: Inno Setup definition.
- `.github/workflows`: continuous integration and release packaging.

## Updating and privacy

Update checks make the minimum request needed to GitHub's Releases API. A package is not accepted unless its byte length and SHA-256 match `update-manifest.json`. Production releases should additionally Authenticode-sign the executable and installer; signing secrets are intentionally not stored in this repository.

## License and support

z_compression is released under the [MIT License](LICENSE). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for dependencies. If the project helps you, you can support development at [Buy Me a Coffee](https://buymeacoffee.com/zernia).
