# 아키텍처

Desktop Calendar는 Windows 11 x64 바탕화면에서 동작하는 로컬 우선 WPF 애플리케이션이다. 계정과 서버 없이 일정·설정·백업을 `%LOCALAPPDATA%\DesktopCalendar`에 저장한다.

## 프로젝트 경계

```text
DesktopCalendar.App ───────────────→ DesktopCalendar.Core
        │
        └→ DesktopCalendar.Infrastructure ─→ DesktopCalendar.Core

DesktopCalendar.Tests → App / Infrastructure / Core
```

- `DesktopCalendar.Core`: UI와 저장 기술에 독립적인 모델, 추상화, 달력·반복·공휴일 규칙
- `DesktopCalendar.Infrastructure`: SQLite 일정 저장소, JSON 설정·백업, Open-Meteo 날씨 구현
- `DesktopCalendar.App`: WPF 화면, ViewModel, Windows 바탕화면·트레이·자동 실행·위치 서비스 통합
- `DesktopCalendar.Tests`: 핵심 규칙, 저장·마이그레이션, 백업, 날씨와 ViewModel 테스트

App은 [App.xaml.cs](../src/DesktopCalendar.App/App.xaml.cs)에서 필요한 구현을 직접 조립한다. 현재 규모에서는 별도 DI 컨테이너를 사용하지 않는다.

## 시작과 종료 흐름

1. `SingleInstanceService`가 중복 실행을 차단한다.
2. `AppPaths`를 기준으로 저장소, 설정, 백업과 날씨 서비스를 만든다.
3. `MainWindowViewModel.InitializeAsync`가 DB·설정·백업과 최초 화면 데이터를 준비한다.
4. `MainWindow`를 표시하고 `DesktopHost`가 캘린더 HWND를 바탕화면 Z 순서에 배치한다.
5. 트레이, Windows 자동 실행, 날씨 갱신 타이머와 기존 인스턴스 활성화 신호를 연결한다.
6. 명시적 종료 시 타이머와 네이티브 리소스를 해제하고 창을 닫는다.

## Windows 바탕화면 통합

WPF 투명 HWND를 `WorkerW`의 교차 프로세스 자식으로 직접 넣으면 렌더링과 입력이 불안정하다. 따라서 [DesktopHost.cs](../src/DesktopCalendar.App/Services/DesktopHost.cs)는 창을 `Progman` 바로 위이면서 일반 애플리케이션 창 아래인 최상위 저층 Z 밴드에 둔다.

- 일반 모드에서는 캘린더가 다른 앱 위로 활성화되지 않게 한다.
- Explorer 상태를 주기적으로 확인하고 연결이 끊기면 다시 결합한다.
- 배치 편집은 Win32 시스템 이동·크기 조정을 사용한다.
- 위치와 크기는 선택 모니터 기준 비율로 저장해 해상도 변화에 대응한다.

이 동작은 Windows Shell과 실제 입력 상태에 의존하므로 자동 테스트만으로 완전히 검증할 수 없다. 릴리스 전 수동 확인 항목은 [DEVELOPMENT.md](DEVELOPMENT.md)에 둔다.

## 도메인과 데이터

`CalendarEvent`는 일정 본문, 장소, 종일/시간, 시간대, 색상, 이모지, 반복, 완료·하이라이트와 제목 서식을 저장한다. 반복 일정은 원본 이벤트와 반복 규칙으로 회차를 계산하며 현재 수정·삭제·상태 변경은 전체 시리즈에 적용된다.

SQLite는 WAL 모드를 사용한다. 현재 DB 스키마는 버전 4다.

- v1→v2: 장소, 상태와 서식 필드
- v2→v3: 일정 이모지
- v3→v4: 일정 기본색과 분리된 하이라이트 색상

마이그레이션 전에 `Backups/calendar-pre-migration-*.db`를 생성한다. 설정 스키마는 버전 2이며 애플리케이션 버전과 독립적으로 증가한다.

## 사용자 데이터

데이터 루트는 `%LOCALAPPDATA%\DesktopCalendar`이다.

- `calendar.db`: 일정 데이터
- `settings.json`: 테마, 날씨, 창과 자동 실행 설정
- `weather-cache.json`: 마지막 정상 날씨 응답
- `Backups`: 일일·마이그레이션 전·복원 전 백업
- `error.log`: 처리된 UI 오류 기록

개발·UI 검증은 실제 사용자 일정을 생성·변경·삭제하지 않는다. 상태 변경 검증이 필요하면 별도 임시 데이터 경로를 사용한다.

## 외부 서비스와 배포 경계

날씨와 위치 검색은 Open-Meteo 및 GeoNames 데이터를 사용한다. 네트워크 실패 시 마지막 정상 캐시를 표시한다. 서비스와 패키지 고지는 루트 [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md)를 기준으로 한다.

배포는 다음 두 형태다.

- portable: .NET 런타임을 포함한 단일 실행 파일
- slim: .NET 10 Desktop Runtime x64가 필요한 다중 파일 배포

두 배포 폴더와 ZIP은 라이선스 원본의 생성 사본을 포함해야 한다.

## 주요 변경 지점

- [MainWindow.xaml](../src/DesktopCalendar.App/MainWindow.xaml), [MainWindow.xaml.cs](../src/DesktopCalendar.App/MainWindow.xaml.cs): 달력 화면, 입력, 배치와 바탕화면 상호작용
- [MainWindowViewModel.cs](../src/DesktopCalendar.App/ViewModels/MainWindowViewModel.cs): 월 데이터, 설정, 일정·날씨 갱신
- [EventEditorWindow.xaml](../src/DesktopCalendar.App/EventEditorWindow.xaml): 일정 추가·편집
- [SettingsWindow.xaml](../src/DesktopCalendar.App/SettingsWindow.xaml): 테마, 위치, 날씨와 백업 설정
- [SqliteEventRepository.cs](../src/DesktopCalendar.Infrastructure/SqliteEventRepository.cs): SQLite 스키마와 마이그레이션
- [OpenMeteoWeatherService.cs](../src/DesktopCalendar.Infrastructure/OpenMeteoWeatherService.cs): 위치 검색, 예보와 캐시
