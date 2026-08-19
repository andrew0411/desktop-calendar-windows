# Desktop Calendar

Windows 11 바탕화면에 고정되는 개인용 월간 캘린더 프로토타입입니다. 네트워크 연결이나 계정 없이 일정과 설정을 로컬에 저장합니다.

## 구현된 기능

- 일반 앱 아래·`Progman` 바로 위의 바탕화면 전용 Z 계층 유지, 최상위 창 방지 및 Explorer 상태 감시
- 일요일 시작, 6주 고정 월간 달력과 이전/다음 달·오늘 이동
- 날짜 셀 더블클릭으로 전체 일정 추가 창 열기
- 제목, 메모, 장소, 종일/시간, 색상 팔레트, 반복 및 종료일 상세 편집
- 공부·업무·생활·건강·약속·여행 등 7개 카테고리의 일정 이모지 선택 및 캘린더 표시
- 일정 제목 굵게·기울임·크기 조절과 키보드 단축키
- 일정 선택 후 날짜 칸 하단 `+` 메뉴에서 하이라이트·완료선 적용
- 대한민국·미국 공휴일 표시
- 직접 입력한 도시 또는 Windows 현재 위치를 사용하는 16일 날씨 예보, 날짜별 상태 아이콘과 최고·최저 기온 표시
- 12시간 자동 날씨 갱신, 수동 새로고침과 오프라인 캐시 폴백
- 요소별 글꼴·크기와 시각적 색상 팔레트, 주말·오늘·배경·투명도·구분선 설정
- 설정 및 일정 추가·편집 창 공통 다크·라이트 모드
- 캘린더를 바탕화면에 유지한 채 조정 결과를 바로 확인하는 실시간 설정 미리보기
- 선택 모니터 위치·크기 저장, 전용 이동·가장자리·모서리 드래그 핸들과 잠금형 배치 편집
- 트레이 메뉴, 단일 인스턴스, 선택적 Windows 자동 실행
- 실행 파일·설정 창·작업표시줄·트레이에 통일된 전용 아이콘
- SQLite WAL 저장, 일일 7세대 백업, 복원 전 안전 백업, JSON 내보내기·복원

## 프로젝트 구조

- `src/DesktopCalendar.Core`: 모델, 인터페이스, 42일 그리드와 반복 일정 규칙
- `src/DesktopCalendar.Infrastructure`: SQLite, 설정, 백업 구현
- `src/DesktopCalendar.App`: WPF UI, MVVM, 바탕화면·트레이·자동 실행 통합
- `tests/DesktopCalendar.Tests`: 핵심 및 저장소 테스트

원래 계획한 WinUI 3 개발 워크로드가 현재 PC에 없어, 계획에 정의된 폴백인 WPF 호스트를 사용합니다. Core와 Infrastructure는 UI 프레임워크와 분리되어 있어 이후 WinUI 호스트를 추가할 수 있습니다.

## 개발 실행

```powershell
dotnet restore DesktopCalendar.sln
dotnet test DesktopCalendar.sln
dotnet run --project src/DesktopCalendar.App/DesktopCalendar.App.csproj -c Debug
```

설정 창을 함께 열어 시작하려면 다음을 사용합니다.

```powershell
dotnet run --project src/DesktopCalendar.App/DesktopCalendar.App.csproj -c Debug -- --settings
```

## 사용법

- 날짜 칸 더블클릭: 전체 일정 추가 창 열기
- 일정 더블클릭: 상세 편집 또는 삭제
- 일정 클릭 또는 누른 채 드래그: 상태를 변경할 일정 선택
- 일정이 있는 날짜 칸 하단 `+`: 선택 일정 하이라이트·완료선 메뉴
- 일정 추가·편집 창: 카테고리별 이모지 선택·해제, `Ctrl+B` 굵게, `Ctrl+I` 기울임, `Ctrl+Shift+<`/`>` 글자 크기 조절, `Esc` 취소
- 상단에 마우스 올리기: 월 이동·오늘·설정 도구 표시
- 설정의 `날씨`에서 도시를 검색하거나 현재 위치를 확인한 뒤 날씨 표시 활성화
- 트레이 아이콘 우클릭: 설정, 배치 편집, 재결합, 자동 실행, 종료
- 배치 편집 상태: `⠿ 위치 이동`을 끌어 이동하고 파란 가장자리·모서리를 끌어 크기 조절한 뒤 `배치 완료`

## 데이터 위치

데이터는 `%LOCALAPPDATA%\DesktopCalendar`에 저장됩니다.

- `calendar.db`: 일정 데이터
- `settings.json`: 테마·창·자동 실행 설정
- `weather-cache.json`: 마지막으로 정상 수신한 날씨 예보
- `Backups`: 자동 및 복원 전 백업

현재 DesktopCal 데이터는 읽거나 변경하지 않습니다.

## 외부 서비스 및 라이선스

- 날씨 데이터: [Open-Meteo](https://open-meteo.com/), [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)
- 위치 검색 데이터: Open-Meteo Geocoding API를 통해 제공되는 [GeoNames](https://www.geonames.org/) 데이터
- Open-Meteo 무료 API는 비상업적 사용 조건이 적용됩니다. 상업적 배포 시 해당 서비스의 최신 이용 조건과 요금제를 확인하세요.
- 오픈소스 패키지의 상세 고지는 [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)를 참조하세요.

## 작업 인수인계

현재 구현 상태, 주요 설계 결정, 데이터 마이그레이션과 다음 작업 시 주의사항은
[`docs/SESSION_HANDOFF_2026-08-16.md`](docs/SESSION_HANDOFF_2026-08-16.md)에 정리되어 있습니다.

## 릴리스 빌드

```powershell
.\build-release.ps1
```

결과는 기본적으로 `artifacts\win-x64`에 생성됩니다. 현재 PC에는 MSIX 제작·서명 도구가 없으므로 이 스크립트는 self-contained x64 배포 폴더를 만듭니다. MSIX 패키징은 Windows SDK의 MakeAppx/SignTool 또는 Visual Studio Packaging workload가 준비된 환경에서 추가할 수 있습니다.
