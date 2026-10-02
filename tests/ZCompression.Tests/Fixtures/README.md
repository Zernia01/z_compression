# RAR regression fixtures

These RAR archives are from [SharpCompress 0.50.4](https://github.com/adamhathcock/sharpcompress/tree/0.50.4/tests/TestArchives/Archives).
Encrypted fixtures use the password `test`. They cover RAR4, RAR5, missing directory CRC metadata, BLAKE2, solid compression, multipart volumes, and encrypted headers/data.

The tests exercise listing, integrity verification, full extraction, and individual-file extraction. Writer integration tests also run when WinRAR is installed or `Z_COMPRESSION_RAR_PATH` points to the user's official `rar.exe`; otherwise those tests are reported as skipped.

Upstream MIT license:

Copyright (c) 2014  Adam Hathcock

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
