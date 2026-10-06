# z_compression

Windows 11을 위한 현대적인 무료 압축·압축 해제 프로그램입니다. 파일은 사용자의 PC에서만 처리되며 계정, 광고, 분석 기능 또는 파일 업로드 기능이 없습니다.

> English instructions are available below.

## 주요 기능

- ZIP, 7Z, TAR, TAR.GZ 압축 파일 생성 및 설치된 WinRAR를 이용한 RAR 생성
- ZIP, 7Z, RAR/RAR5, TAR, GZip, BZip2, XZ, Zstandard 파일 열기·목록 보기·검사·압축 해제
- 압축 파일 안을 폴더처럼 탐색하고 파일을 두 번 클릭하여 열기
- 열린 ZIP·7Z·TAR·TAR.GZ·RAR에 파일을 끌어다 놓아 안전하게 다시 압축
- 파일 및 폴더 드래그 앤 드롭, 검색, 정렬, 진행률 표시와 작업 취소
- 설정에서 압축하기·압축 풀기 단축키를 원하는 단일 키 또는 키 조합으로 지정
- 한국어, 영어, 일본어, 중국어 간체·번체, 국한문혼용 UI
- 라이트·다크·시스템 테마와 저장되는 성능 설정
- 앱 시작 시 GitHub Releases 기반 자동 업데이트 확인 및 SHA-256 검증
- Zip Slip 경로 탈출 방지와 압축 폭탄 완화용 크기·항목 수 제한
- Windows 기본 앱 등록 및 파일·폴더 우클릭 압축 메뉴

### 탐색기 메뉴에서 바로 압축·풀기 (1.1.1)

`설정 > 단축키`를 변경하면 바로 저장되고 탐색기 우클릭 메뉴의 키도 즉시 갱신됩니다. 예를 들어 압축하기를 `U`로 지정하면 메뉴에서 `U`를 눌러 옵션 창 없이 원본 옆에 압축 파일을 만듭니다. 기본값은 ZIP·높은 압축입니다. 압축 풀기는 지정한 메뉴 키로 원본 옆의 파일 이름 폴더에 바로 풉니다. 같은 이름이 있으면 `(2)`, `(3)`을 붙이며 기존 파일과 폴더를 덮어쓰지 않습니다. 작업 중에는 진행 창에서 취소할 수 있고 암호화 파일은 암호를 묻습니다. Delete로 단축키를 해제하면 메뉴의 키도 해제되지만 메뉴 클릭은 계속 사용할 수 있습니다.

Windows 11에서는 `추가 옵션 표시` 메뉴에서 사용하세요. `Shift+F10`으로 이 메뉴를 바로 열 수도 있습니다. 탐색기 기본 메뉴는 문자·숫자 접근 키만 지원하므로 `Ctrl+E`는 앱 안에서 Ctrl+E, 메뉴에서는 E로 사용합니다. F키·기호 키는 앱 안에서만 동작합니다. 앱이 제공하는 두 메뉴의 접근 키가 겹치는 설정은 허용하지 않습니다. 다른 프로그램의 메뉴에 같은 키가 있으면 Windows가 항목을 순환 선택하므로 원하는 항목에서 Enter를 누르세요.

RAR/RAR5 열기·검사·압축 풀기·개별 파일 열기는 WinRAR 설치 없이 지원합니다. 솔리드·분할·암호화 RAR도 지원하며, 분할 파일은 모든 조각을 같은 폴더에 두고 첫 번째 파일을 여세요. 암호화 파일은 필요한 시점에 암호 입력 창이 표시됩니다.

분할 RAR에 파일을 추가하려면 먼저 압축을 푼 뒤 추가할 파일과 함께 새 RAR를 만드세요.

RAR 생성·파일 추가에는 별도로 설치한 [WinRAR](https://www.rarlab.com/download.htm)의 `rar.exe`가 필요합니다. 기본 설치 폴더와 PATH에서 자동으로 찾으며, 다른 위치는 `Z_COMPRESSION_RAR_PATH` 환경 변수로 지정할 수 있습니다. 새 압축 창에서 RAR와 압축 수준을 선택하고 선택적으로 암호를 입력하세요. 암호는 파일 내용과 이름을 보호하며 설정에 저장되지 않습니다. WinRAR 사용에는 해당 제품의 라이선스가 적용됩니다. RAR 도구를 앱에 포함하거나 자동 설치하지 않습니다.

## 설치 및 사용법

1. [Releases](https://github.com/Zernia01/z_compression/releases)에서 최신 `win-x64-setup.exe`를 받습니다.
2. 설치기를 실행합니다. 바탕화면 바로가기가 기본으로 생성됩니다.
3. `새 압축`을 눌러 파일 또는 폴더를 추가하고 ZIP, 7Z, TAR, TAR.GZ, RAR 중 하나를 선택합니다.
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

- Create ZIP, 7Z, TAR, TAR.GZ, and RAR archives (RAR creation uses an installed WinRAR).
- Open, browse, test, and extract ZIP, 7Z, RAR/RAR5, TAR, GZip, BZip2, XZ, and Zstandard archives.
- Browse an archive like a folder and double-click a file to open it.
- Safely rebuild an open ZIP, 7Z, TAR, TAR.GZ, or RAR archive after files are dropped into it.
- Drag and drop, search, sort, progress reporting, and cancellation.
- Assign a single key or key combination for create/extract shortcuts in Settings.
- Korean, English, Japanese, Simplified/Traditional Chinese, and Korean mixed-script interfaces.
- Light, dark, and system themes with persistent performance settings.
- Automatic update checks through GitHub Releases with package size and SHA-256 verification.
- Zip Slip protection and limits that reduce archive-bomb risk.
- Windows Default Apps registration and an Explorer right-click compression command.

### Quick Explorer menu actions (1.1.1)

Changing a shortcut in Settings saves it and updates the Explorer menu immediately. For example, assigning `U` to compression lets you press U in the context menu to create an archive beside the source without an options dialog (ZIP/high compression by default). The extraction menu key extracts into a sibling folder named after the archive. Existing files and folders are preserved by adding `(2)`, `(3)`, and so on. The progress window allows cancellation; encrypted archives prompt for a password. Clearing a shortcut with Delete removes its menu key while preserving the clickable command.

On Windows 11 use Show more options, or open the classic menu directly with `Shift+F10`. Static Explorer menus support letter/digit mnemonics: Ctrl+E remains Ctrl+E inside the app and uses E in the menu. Function and punctuation keys apply inside the app only. Settings prevent the app's two menu keys from conflicting. If another application's menu uses the same letter, Windows cycles through matching entries; press Enter on the desired entry.

Opening, testing, extracting, and previewing RAR/RAR5 archives works without WinRAR, including solid, multipart, and encrypted archives. Keep all volumes together and open the first volume. The app prompts for passwords when needed.

To add files to a multipart RAR, extract it first and create a new RAR with the additional files.

RAR creation and modification require a separately installed [WinRAR](https://www.rarlab.com/download.htm), subject to its license. The app finds `rar.exe` in the standard installation folders or PATH; set `Z_COMPRESSION_RAR_PATH` for a custom location. Select RAR and a compression level in New archive and optionally enter a password to encrypt both content and names. Passwords are kept only in memory. No RAR tool is bundled or automatically installed.

### Install and use

1. Download the latest `win-x64-setup.exe` from [Releases](https://github.com/Zernia01/z_compression/releases).
2. Run the installer. A desktop shortcut is selected by default.
3. Select `New archive`, add files or folders, and choose ZIP, 7Z, TAR, TAR.GZ, or RAR.
4. Open an existing archive with `Open archive` or drag and drop it onto the window.
5. Select `Extract` and choose a destination folder.
6. Turn update checks on or off under `Settings > General > Automatic updates`, or use `Check for updates now`.
7. CPU thread and operation-priority selections under `Settings > Performance` are saved for future sessions.
8. Assign create/extract keys under `Settings > Shortcuts`. Add files to an open archive with the toolbar or drag and drop.

On Windows 11, the Explorer compression command may appear under `Show more options`.

## Privacy and license

Settings are stored as UTF-8 JSON under `%LOCALAPPDATA%\z_compression`. Update checks only contact this repository's GitHub Releases API. Downloaded update packages must match the manifest size and SHA-256 hash before installation.

Licensed under the [MIT License](LICENSE). Third-party notices are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

### 파일 형식 아이콘 (1.1.2)

ZIP은 파란색, RAR은 보라색, 7Z는 회색의 전용 아이콘을 사용합니다. 설치 파일과 포터블 배포에 아이콘이 포함되며 앱 실행 시 파일 형식 등록을 갱신합니다. 이전 공통 아이콘이 계속 표시되면 설정의 기본 앱 선택에서 ZIP·RAR·7Z를 z_compression에 다시 연결하세요.

### 탐색기 새로고침 수정 (1.1.3)

실행할 때 파일 연결과 메뉴의 현재 값을 확인하고, 변경된 값만 기록합니다. 등록 내용이 같으면 탐색기에 갱신 알림을 보내지 않습니다. 최초 등록, 설치 경로 변경, 메뉴 단축키 변경 등 실제 변경이 있을 때만 알림을 한 번 보냅니다.

### Archive format icons (1.1.2)

ZIP uses the blue icon, RAR the purple icon, and 7Z the gray icon. Both installed and portable packages include the icons and refresh registration on startup. If the old shared icon remains, use the default-app action in Settings to associate each format with z_compression again.

### Explorer refresh fix (1.1.3)

Startup compares existing file associations and menu values and writes only changes. Unchanged registration sends no Explorer refresh notification. First registration, installation path changes, and menu shortcut changes send a single notification after registration completes.

### 밖으로 끌어서 압축 풀기 (1.1.4)

압축 파일 안에서 파일·폴더를 선택한 뒤 바탕화면이나 탐색기 폴더로 끌어놓으면 해당 위치로 복사됩니다. Ctrl·Shift로 여러 항목을 선택할 수 있고, 폴더는 하위 내용과 빈 폴더까지 함께 풀립니다. 현재 보고 있는 폴더 기준으로 이름을 유지하며, 원본 압축 파일은 그대로 보존됩니다. 받는 프로그램이 파일 데이터를 요청할 때 선택 항목을 임시 폴더에 풀고 Windows 파일 드래그로 전달합니다. 준비 중에는 진행 표시·암호 입력·취소를 지원하고, 이름 충돌은 탐색기의 복사 대화상자에서 처리합니다.

### Drag files out to extract (1.1.4)

Select files or folders inside an archive and drag them to the desktop or an Explorer folder. Ctrl/Shift multi-selection, nested contents, and empty folders are supported. Names are relative to the currently viewed archive folder; the original archive stays intact. When the target requests file data, selected entries are extracted to a temporary folder and passed through Windows file drag-and-drop. Preparation supports progress, passwords, and cancellation; Explorer handles destination name conflicts.

### 드래그 멈춤 수정 (1.1.5)

드래그 데이터 요청 중 압축 해제와 UI 대기를 수행하던 코드를 제거했습니다. 먼저 비동기로 선택 항목을 준비한 뒤 완성된 파일 목록만 탐색기에 전달합니다. 준비 중 마우스를 놓았다면 준비 완료 안내 후 같은 항목을 다시 끌어놓으세요. 같은 선택은 준비된 파일을 재사용합니다.

### Drag freeze fix (1.1.5)

Selected entries are prepared asynchronously before starting Windows drag-and-drop. File data requests perform no extraction or UI dispatch. If you release the mouse while preparation is running, wait for the ready message and drag the same selection again; completed files are reused.
