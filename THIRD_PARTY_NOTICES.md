# Third-party notices

z_compression uses [SharpCompress](https://github.com/adamhathcock/sharpcompress), Copyright Adam Hathcock and contributors, under the MIT License.

The test project uses MSTest packages from Microsoft under their respective open-source licenses. NuGet package license metadata is the authoritative source for the exact version restored by this repository.

RAR reading and extraction use SharpCompress. RAR creation invokes the user's separately installed WinRAR/rar.exe, which is subject to the RARLAB license: https://www.rarlab.com/license.htm. This project does not bundle WinRAR or unrar binaries.

RAR regression fixtures in tests/ZCompression.Tests/Fixtures originate from SharpCompress 0.50.4 (https://github.com/adamhathcock/sharpcompress/tree/0.50.4/tests/TestArchives/Archives), distributed under its MIT license. The fixture attribution and license are in that directory's README.md.
