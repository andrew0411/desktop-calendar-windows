# 개발 및 저장소 운영

이 문서는 로컬 개발과 에이전트 협업에서 반복해서 적용할 실행 절차와 관리 결정을 정의한다.

## 개발 환경과 기본 명령

- Windows 11 x64
- .NET 10 SDK
- PowerShell

```powershell
dotnet restore DesktopCalendar.sln
dotnet test DesktopCalendar.sln
dotnet run --project src/DesktopCalendar.App/DesktopCalendar.App.csproj -c Debug
```

설정 창을 함께 열려면 마지막 명령에 `-- --settings`를 추가한다.

## 작업 원칙

- 작업 시작과 종료에 `git status --short`를 확인한다.
- 기존 사용자 변경과 무관한 파일을 되돌리거나 정리하지 않는다.
- `%LOCALAPPDATA%\DesktopCalendar`의 실제 사용자 데이터를 테스트 대상으로 사용하지 않는다.
- 구조나 런타임 흐름이 바뀌면 `ARCHITECTURE.md`를 함께 갱신한다.
- 사용자에게 보이는 변경은 해당 버전의 `releases/vX.Y.Z.md`에 기록한다.
- 세션별 진행 상황을 현재 기준 문서에 누적하지 않는다.

## 검증과 정리

일반 코드 변경의 최소 검증은 다음과 같다.

```powershell
dotnet build DesktopCalendar.sln --configuration Release
dotnet test DesktopCalendar.sln --configuration Release --no-restore
```

릴리스 생성은 [VERSIONING.md](VERSIONING.md)의 절차와 `./build-release.ps1`을 사용한다.

작업 종료 시 저장소 규칙에 따라 다음을 실행한다.

```powershell
.\scripts\Cleanup-DevelopmentArtifacts.ps1 -IncludeBuildCaches
```

이 스크립트는 검증용 캡처·임시 파일·테스트 결과와 `bin/obj`만 제거한다. `artifacts/packages`, `artifacts/win-x64`, `artifacts/win-x64-slim`의 의도된 배포 결과는 보존한다.

## 릴리스 수동 확인

자동 테스트 후에도 다음 Windows 통합 동작은 실제 화면에서 확인한다.

1. 캘린더가 일반 앱을 덮지 않는지 확인한다.
2. 이전 달·오늘·다음 달과 설정 창을 확인한다.
3. 일정 추가·편집·취소와 날짜 셀 스크롤을 확인한다.
4. 다크/라이트 모드와 설정 미리보기를 확인한다.
5. 위치 이동과 모든 방향의 크기 조정 후 재시작 복원을 확인한다.
6. `PRAGMA user_version`과 기존 일정 수를 확인한다.

## 줄바꿈

현재 저장소에는 LF, CRLF와 mixed 줄바꿈이 함께 있다. 기능에는 대체로 영향이 없지만 파일 전체가 바뀐 것처럼 보이는 diff, 불필요한 merge 충돌, IDE·에이전트별 반복 변환을 만든다.

소스, XAML, Markdown, XML과 PowerShell은 저장소 내부에서 LF로 통일하는 것을 권장한다. `.gitattributes`와 `git add --renormalize .` 적용은 기존 기능 변경과 섞지 않고 전용 커밋으로 수행한다.

## NuGet 패키지 관리

현재는 중앙 패키지 관리를 사용하지 않는다. 직접 참조가 Infrastructure 2개, Tests 3개뿐이고 같은 버전을 여러 프로젝트에 반복 선언하지 않아 `Directory.Packages.props`의 이점이 작다.

동일 패키지를 세 프로젝트 이상에서 사용하거나, 테스트/UI 프로젝트가 늘거나, 실제 버전 드리프트가 반복될 때 중앙 관리를 다시 검토한다.

## 법적 고지 파일

루트 `LICENSE`와 `THIRD-PARTY-NOTICES.md`만 직접 관리하는 원본이다. 배포 폴더의 동명 파일은 ZIP이 저장소와 분리되어 전달될 때 필요한 생성 사본이므로 삭제 대상이 아니다.

`build-release.ps1`은 원본을 배포 폴더에 복사한 뒤 SHA-256을 비교한다. 패키지 고지 내용을 바꿀 때는 루트 파일만 수정하고 릴리스를 다시 생성한다.

## 자동화 정책

현재 자동화는 Release 테스트, portable/slim 게시, 버전·법적 고지 검사, ZIP 생성과 개발 산출물 정리를 포함한다. 로컬 에이전트 협업에서는 원격 CI보다 누구나 같은 결과를 재현하는 저장소 스크립트를 우선한다.

추가 우선순위는 다음과 같다.

1. restore·format·build·test·diff 검사를 묶는 저장소 검증 스크립트
2. ZIP별 SHA-256과 소스 커밋을 기록하는 릴리스 매니페스트
3. 지원하는 모든 이전 DB·설정 스키마의 마이그레이션 fixture
4. 임시 데이터 경로를 사용하는 시작·설정·종료 UI smoke test
5. csproj 버전, 릴리스 노트와 ZIP 이름의 일치 검사
6. 정기적인 NuGet 취약점·지원 종료 상태 보고

이 자동화는 수동 누락, 에이전트별 검증 차이, 사용자 데이터 마이그레이션 위험과 릴리스 출처 불명확성을 줄인다. 외부 협업자와 배포 빈도가 늘면 동일 명령을 GitHub Actions 같은 원격 CI에서 호출하도록 확장한다.
