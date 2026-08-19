# Desktop Calendar 작업 인수인계 — 2026-08-16

이 문서는 이후 피드백·버그 수정·UI/UX 개선 작업을 같은 프로젝트에서 바로 이어가기 위한 기준 문서다.

## 1. 현재 상태

- 대상: Windows 11 x64 개인용 바탕화면 캘린더
- 기술: C# / .NET 10 / WPF / MVVM / SQLite
- 실행 파일: `artifacts/win-x64/DesktopCalendar.exe`
- 최종 검증: Release 빌드 경고 0·오류 0, 자동 테스트 16개 전부 통과
- 최종 데이터 확인: SQLite 스키마 버전 2, 기존 일정 2개 보존
- 세션 종료 시 앱은 최신 실행본으로 실행 중
- 사용자 데이터와 DesktopCal 원본 데이터는 이 앱이 읽거나 변경하지 않음

원래 계획은 WinUI 3였지만 현재 PC에 필요한 워크로드가 없어 계획에 정의된 WPF 폴백을 사용했다. `Core`와 `Infrastructure`는 UI와 분리되어 있다.

## 2. 이번 세션에서 완성한 기능

### 바탕화면 동작과 안정성

- 캘린더는 `Topmost`가 아닌 일반 앱보다 낮은 바탕화면 전용 Z 밴드에 둔다.
- WPF 투명 HWND를 `WorkerW`의 교차 프로세스 자식으로 직접 넣으면 렌더링과 입력이 불안정해, `Progman` 바로 위이면서 일반 창 아래인 최상위 저층 밴드를 사용한다.
- Explorer 상태를 주기적으로 확인하며 연결이 끊기면 다시 결합한다.
- 일반 모드에서는 클릭으로 캘린더가 다른 앱 위로 올라오지 않도록 `WM_MOUSEACTIVATE`에서 활성화를 막는다.
- 설정·일정 편집 창은 `Topmost`가 아니며 일반 Windows 창처럼 동작한다.
- 창 위치를 다시 적용하거나 투명도를 미리 볼 때 불필요한 재결합을 줄여 깜빡임을 완화했다.

### 위치·크기 조정

- 상단 `위치·크기` 버튼으로 배치 편집 모드에 들어간다.
- 배치 편집 중 왼쪽 위 `⠿ 위치 이동` 핸들을 드래그해 창을 이동한다.
- 파란 가장자리와 네 모서리를 드래그해 창 크기를 변경한다.
- WPF 기본 `ResizeMode`나 `DragMove`에 의존하지 않고 Win32 시스템 이동·크기 조정을 직접 시작한다. 관련 구현은 `DesktopHost.BeginSystemDrag`와 `DesktopHost.SetLayoutEditing`이다.
- 드래그가 끝나면 모니터 기준 위치·크기를 비율로 저장한다.
- 창 높이에 맞춰 날짜 칸에서 보이는 일정 개수를 자동 재계산한다.

### 월간 캘린더와 일정 입력

- 일요일 시작, 항상 6행×7열인 42일 월간 달력이다.
- 상단의 이전 달·오늘·다음 달·설정 버튼이 동작한다.
- 날짜 칸을 더블클릭하면 작은 빠른 입력칸 대신 전체 `일정 추가` 창이 열린다.
- 일정 더블클릭은 `일정 편집` 창을 연다.
- 일정 추가·편집 창에서 `Esc`는 저장 없이 취소한다.
- 기존 일정은 위에 유지되고 새 일정은 생성 시각 순서에 따라 아래에 추가된다.
- 공간을 넘는 일정은 `+N개`로 표시하고 날짜별 전체 목록 창을 연다.

### 일정 데이터와 표현

- 지원 필드: 제목, 메모, 장소, 종일 여부, 시작·종료, 시간대, 색상, 반복 규칙, 생성·수정 시각
- 추가 필드: 완료, 하이라이트, 굵게, 기울임, 제목 글자 크기
- `Ctrl+B`: 제목 굵게
- `Ctrl+I`: 제목 기울임
- `Ctrl+Shift+<` / `Ctrl+Shift+>`: 제목 글자 크기 감소·증가
- 현재 서식은 선택 문자 일부가 아니라 일정 제목 전체에 적용된다.
- 반복 일정의 수정·삭제·하이라이트·완료선은 현재 전체 시리즈에 적용된다.

### 일정 선택과 날짜 하단 액션 메뉴

- 일정 행을 클릭하면 파란 테두리와 배경으로 선택 상태가 표시된다.
- 마우스를 누른 채 다른 일정 행으로 이동해도 선택 대상이 따라가도록 `MouseEnter`와 `PreviewMouseMove`를 함께 처리한다.
- 일정별 오른쪽 버튼은 제거했다.
- 일정이 있는 날짜 칸 오른쪽 아래에만 `+` 버튼을 표시한다.
- `+`를 누르면 선택 일정 이름과 `하이라이트`, `완료선` 버튼이 작은 팝업으로 나타난다.
- 일정을 선택하지 않은 상태에서는 안내 문구가 나오고 액션 버튼은 비활성화된다.

### 공휴일

- 대한민국 공휴일은 `KR`, 미국 공휴일은 `US`로 표시한다.
- 공휴일 계산은 `HolidayService`에 있으며 네트워크를 사용하지 않는다.

### 설정과 테마

- 모니터 이름은 `\\.\DISPLAY1` 대신 `디스플레이1`처럼 표시한다.
- 글꼴 목록은 설치된 대표 한글·영문 글꼴 9개 미만으로 제한하고 기본 글꼴을 명시한다.
- 색상은 HEX 직접 입력이 아니라 24색 시각적 팔레트로 선택한다.
- 배경 투명도와 구분선 두께는 캘린더를 뒤에 유지한 상태에서 실시간 미리보기가 적용된다.
- 슬라이더는 클릭 위치 이동과 세밀한 조정이 가능하다.
- 백업 동작 설명을 설정 화면에 포함했다.
- 설정·일정 추가·일정 편집·날짜 전체 일정 창에 공통 다크/라이트 모드를 적용했다.
- 선택 모드는 `AppSettings.AppearanceMode`에 `Dark` 또는 `Light`로 저장된다.
- 색상 리소스는 `UiThemeService`가 동적으로 교체하며 DWM 제목 표시줄도 모드에 맞춘다.
- 다크/라이트 모드는 편집용 창 UI에 적용된다. 캘린더 본체는 사용자가 설정한 배경·글꼴·색상 테마를 유지한다.

### 저장·마이그레이션·백업

- SQLite는 WAL 모드를 사용한다.
- DB 스키마는 버전 2다.
- 버전 1에서 2로 이동할 때 장소·상태·서식 필드를 안전하게 추가한다.
- 실제 사용자 DB 마이그레이션 전 백업:
  `%LOCALAPPDATA%\DesktopCalendar\Backups\calendar-pre-migration-20260816-004204.db`
- 마이그레이션 전후 기존 일정 수 2개를 확인했다.
- 일일 자동 백업은 최근 7개를 보관하며, JSON 내보내기·검증 후 복원을 지원한다.

## 3. 주요 파일

- `src/DesktopCalendar.App/MainWindow.xaml`: 달력, 일정 선택 표시, 날짜 하단 `+` 메뉴, 배치 핸들
- `src/DesktopCalendar.App/MainWindow.xaml.cs`: 날짜·일정 입력, 선택 액션, 위치·크기 저장, 바탕화면 입력 처리
- `src/DesktopCalendar.App/EventEditorWindow.xaml(.cs)`: 일정 추가·편집, 장소, 팔레트, 제목 서식, Esc 취소
- `src/DesktopCalendar.App/SettingsWindow.xaml(.cs)`: 모니터, 글꼴, 색상, 투명도, 화면 모드, 백업 UI
- `src/DesktopCalendar.App/Controls/PaletteColorPicker.xaml(.cs)`: 24색 공통 팔레트
- `src/DesktopCalendar.App/Services/DesktopHost.cs`: 저층 Z 순서, 바탕화면 재결합, 시스템 이동·크기 조정
- `src/DesktopCalendar.App/Services/UiThemeService.cs`: 다크/라이트 리소스 및 제목 표시줄
- `src/DesktopCalendar.App/ViewModels/CalendarDayViewModel.cs`: 일정 선택 상태와 하단 메뉴 상태
- `src/DesktopCalendar.Infrastructure/SqliteEventRepository.cs`: 스키마 2와 마이그레이션
- `tests/DesktopCalendar.Tests`: 달력·공휴일·반복·저장·마이그레이션·백업 테스트

## 4. 데이터 위치와 안전 수칙

데이터 루트는 `%LOCALAPPDATA%\DesktopCalendar`이다.

- `calendar.db`: 실제 일정
- `settings.json`: 테마, 모니터, 창 위치, 배치 잠금, 자동 실행, 화면 모드
- `Backups`: 일일 백업, 마이그레이션 전 백업, 복원 전 안전 백업
- `error.log`: 설정 창 등에서 처리된 오류 기록

후속 작업에서는 실제 사용자 DB의 일정을 임의로 저장·토글·삭제하지 않는다. UI 자동 검사는 새 일정 창을 열고 `취소`로 닫거나, 액션 버튼을 누르지 않는 방식으로 진행한다. 상태 버튼을 시험해야 한다면 별도 임시 DB를 사용한다.

## 5. 빌드와 검증

```powershell
dotnet restore DesktopCalendar.sln
dotnet build src/DesktopCalendar.App/DesktopCalendar.App.csproj --configuration Release --no-restore
dotnet test DesktopCalendar.sln --configuration Release --no-restore
dotnet publish src/DesktopCalendar.App/DesktopCalendar.App.csproj `
  --configuration Release --runtime win-x64 --self-contained true `
  --output artifacts/win-x64 --no-restore
```

또는 다음 스크립트를 사용한다.

```powershell
.\build-release.ps1
```

배포본을 교체할 때는 실행 중인 `DesktopCalendar` 프로세스를 종료한 뒤 publish하고, 개인용 실행은 `artifacts\win-x64\DesktopCalendar.exe`를 사용한다.

검증 체크리스트:

1. 캘린더가 Chrome 등 일반 앱을 덮지 않는지 확인
2. 이전 달·오늘·다음 달·설정 버튼 확인
3. 날짜 더블클릭으로 일정 추가 창이 한 번만 열리는지 확인
4. 일정 선택 후 날짜 하단 `+` 메뉴의 선택 이름과 버튼 활성화 확인
5. 일정 편집 창에서 장소·팔레트·서식 단축키·Esc 확인
6. 다크/라이트 모드 전환 후 설정·추가·편집 창 확인
7. 위치 이동과 네 방향·네 모서리 크기 조정 후 재시작 복원 확인
8. `sqlite3`로 `PRAGMA user_version`과 일정 수 확인
9. 전체 테스트 통과 후 실행본 교체

## 6. 이후 개선 후보

- 선택 문자 단위 리치 텍스트 서식 저장
- 여러 일정을 동시에 선택해 상태 일괄 변경
- 반복 일정 개별 회차 예외 처리
- Windows 알림, 할 일, 검색
- 한국 공휴일·음력·절기 고도화
- ICS·Google Calendar·Outlook 연동
- 인쇄와 선택적 클라우드 동기화

## 7. 개발 환경 참고

- 저장소 상위 디렉터리의 Git 상태에서는 이 프로젝트 전체가 아직 추적되지 않은 폴더로 표시될 수 있다. 후속 작업에서 `git reset`, `git clean` 등 파괴적인 명령을 사용하지 않는다.
- 자동 포인터 검사 중 NVIDIA GeForce Overlay가 화면 입력을 가로막을 수 있었다. 앱의 이동·크기 조정은 오버레이를 잠시 숨긴 상태에서 실제 좌표·크기 변경으로 검증했다. 일반 사용자 피드백이 없다면 이를 앱 자체 입력 버그로 단정하지 않는다.
