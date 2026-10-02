# z_compression

Windows 11을 위한 현대적인 무료 압축·압축 해제 프로그램입니다. 파일은 사용자의 PC에서만 처리되며 계정, 광고, 분석 기능 또는 파일 업로드 기능이 없습니다.

> English instructions are available below.

## 주요 기능

- ZIP, 7Z, TAR, TAR.GZ 압축 파일 생성
- ZIP, 7Z, RAR/RAR5, TAR, GZip, BZip2, XZ, Zstandard 파일 열기·목록 보기·검사·압축 해제
- 압축 파일 안을 폴더처럼 탐색하고 파일을 두 번 클릭하여 열기
- 열린 ZIP·7Z·TAR·TAR.GZ에 파일을 끌어다 놓아 안전하게 다시 압축
- 파일 및 폴더 드래그 앤 드롭, 검색, 정렬, 진행률 표시와 작업 취소
- 설정에서 압축하기·압축 풀기 단축키를 원하는 단일 키 또는 키 조합으로 지정
- 한국어, 영어, 일본어, 중국어 간체·번체, 국한문혼용 UI
- 라이트·다크·시스템 테마와 저장되는 성능 설정
- 앱 시작 시 GitHub Releases 기반 자동 업데이트 확인 및 SHA-256 검증
- Zip Slip 경로 탈출 방지와 압축 폭탄 완화용 크기·항목 수 제한
- Windows 기본 앱 등록 및 파일·폴더 우클릭 압축 메뉴

RAR/RAR5는 열기와 압축 해제만 지원합니다. RAR 생성은 독점 포맷 제약으로 지원하지 않습니다.

## 설치 및 사용법

1. [Releases](https://github.com/Zernia01/z_compression/releases)에서 최신 `win-x64-setup.exe`를 받습니다.
2. 설치기를 실행합니다. 바탕화면 바로가기가 기본으로 생성됩니다.
3. `새 압축`을 눌러 파일 또는 폴더를 추가하고 ZIP, 7Z, TAR, TAR.GZ 중 하나를 선택합니다.
4. 기존 압축 파일은 `압축 파일 열기` 또는 드래그 앤 드롭으로 열 수 있습니다.
5. 압축을 풀려면 `압축 풀기`를 누르고 대상 폴더를 선택합니다.
6. 자동 업데이트는 `설정 > 일반 > 자동 업데이트`에서 켜거나 끌 수 있으며, `지금 업데이트 확인`으로 수동 검사할 수 있습니다.
7. CPU 스레드와 작업 우선순위는 `설정 > 성능`에서 선택하며 다음 실행에도 유지됩니다.
8. `설정 > 단축키`에서 압축하기와 압축 풀기 키를 지정할 수 있습니다. 열린 압축 파일에는 도구 모음의 `파일 추가` 또는 드래그 앤 드롭으로 파일을 넣을 수 있습니다.

Windows 11의 우클릭 압축 메뉴는 시스템 설정에 따라 `추가 옵션 표시` 안에 나타날 수 있습니다.

## 개발 빌드

.NET 10 SDK가 필요합니다.

```powershell
dotnet restore z_compression.slnx --configfile NuGet.Config
dotnet build z_compression.slnx -c Release --no-restore
dotnet test tests/ZCompression.Tests/ZCompression.Tests.csproj -c Release --no-build
```

---

## English

z_compression is a modern, free archive manager for Windows 11. All files are processed locally; the app has no accounts, advertising, analytics, or file-upload features.

### Features

- Create ZIP, 7Z, TAR, and TAR.GZ archives.
- Open, browse, test, and extract ZIP, 7Z, RAR/RAR5, TAR, GZip, BZip2, XZ, and Zstandard archives.
- Browse an archive like a folder and double-click a file to open it.
- Safely rebuild an open ZIP, 7Z, TAR, or TAR.GZ archive after files are dropped into it.
- Drag and drop, search, sort, progress reporting, and cancellation.
- Assign a single key or key combination for create/extract shortcuts in Settings.
- Korean, English, Japanese, Simplified/Traditional Chinese, and Korean mixed-script interfaces.
- Light, dark, and system themes with persistent performance settings.
- Automatic update checks through GitHub Releases with package size and SHA-256 verification.
- Zip Slip protection and limits that reduce archive-bomb risk.
- Windows Default Apps registration and an Explorer right-click compression command.

RAR/RAR5 support is read/extract only. Creating RAR archives is unavailable because RAR is a proprietary format.

### Install and use

1. Download the latest `win-x64-setup.exe` from [Releases](https://github.com/Zernia01/z_compression/releases).
2. Run the installer. A desktop shortcut is selected by default.
3. Select `New archive`, add files or folders, and choose ZIP, 7Z, TAR, or TAR.GZ.
4. Open an existing archive with `Open archive` or drag and drop it onto the window.
5. Select `Extract` and choose a destination folder.
6. Turn update checks on or off under `Settings > General > Automatic updates`, or use `Check for updates now`.
7. CPU thread and operation-priority selections under `Settings > Performance` are saved for future sessions.
8. Assign create/extract keys under `Settings > Shortcuts`. Add files to an open archive with the toolbar or drag and drop.

On Windows 11, the Explorer compression command may appear under `Show more options`.

## Privacy and license

Settings are stored as UTF-8 JSON under `%LOCALAPPDATA%\z_compression`. Update checks only contact this repository's GitHub Releases API. Downloaded update packages must match the manifest size and SHA-256 hash before installation.

Licensed under the [MIT License](LICENSE). Third-party notices are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
