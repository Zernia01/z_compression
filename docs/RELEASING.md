# Releasing

1. Update the single `<Version>` value in `Directory.Build.props` and commit it.
2. Run the Release build and all tests locally.
3. Create and push a matching annotated tag, for example `v1.0.0`.
4. GitHub Actions publishes self-contained x64 and ARM64 portable ZIP files and SHA-256 checksum files.
5. Configure repository URL constants in `GitHubUpdateService` if the final GitHub owner or repository differs from `zernia/z_compression`.
6. For production distribution, sign the binaries and installer with an Authenticode certificate before publishing. Never store the certificate or password in the repository.

The updater keeps the previous installation in a sibling backup directory until the new process launches. Package verification occurs before the updater is started.
