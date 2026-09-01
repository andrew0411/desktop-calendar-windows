# 버전 관리 정책

이 문서는 Desktop Calendar의 프로젝트 버전, 실행 파일 버전, 배포 패키지 이름을 하나의 규칙으로 관리하기 위한 기준이다.

## 버전 형식

애플리케이션 버전은 Semantic Versioning의 `MAJOR.MINOR.PATCH` 형식을 사용한다.

- `MAJOR`: 사용자 데이터나 사용 방식에 호환되지 않는 변경
- `MINOR`: 기존 호환성을 유지하는 기능 추가
- `PATCH`: 기존 호환성을 유지하는 수정과 작은 개선
- 시험 배포가 필요하면 `0.3.0-beta.1`과 같은 접미사를 사용할 수 있다.

SQLite `PRAGMA user_version`, 설정의 `SchemaVersion`, 백업의 `SchemaVersion`은 애플리케이션 버전과 독립적으로 관리한다. 데이터 형식이 변경될 때만 각각 증가시킨다.

## 단일 버전 기준

릴리스 버전의 단일 기준은 `src/DesktopCalendar.App/DesktopCalendar.App.csproj`의 `VersionPrefix`다.

```xml
<VersionPrefix>0.2.1</VersionPrefix>
```

MSBuild는 이 값으로 다음 값을 만든다.

- 제품/정보 버전: `0.2.1`
- 파일 버전: `0.2.1.0`
- 어셈블리 버전: `0.2.1.0`
- ZIP 이름: `DesktopCalendar-v0.2.1-win-x64-{portable|slim}.zip`

`build-release.ps1`은 게시 전에 이 값들이 서로 일치하는지 확인하고, 게시된 두 실행 파일의 제품·파일 버전도 다시 검사한다. 패키지 이름에 별도의 버전을 직접 입력하지 않는다.

## 릴리스 절차

1. 변경 성격에 따라 `VersionPrefix`를 올린다.
2. `docs/releases/vX.Y.Z.md`를 작성한다.
3. 실행 중인 Desktop Calendar를 종료한다.
4. 저장소 루트에서 `./build-release.ps1`을 실행한다.
5. Release 테스트, portable/slim 게시, 버전 검사와 ZIP 검사가 모두 통과하는지 확인한다.
6. 생성된 두 ZIP을 실행 검증한 뒤 커밋과 `vX.Y.Z` Git 태그를 만든다.

기본 출력은 다음과 같다.

- `artifacts/win-x64`: .NET 런타임 포함 portable 실행 폴더
- `artifacts/win-x64-slim`: .NET Desktop Runtime이 필요한 slim 실행 폴더
- `artifacts/packages`: 위 두 폴더의 ZIP 패키지

검증 전용 출력이 필요하면 프로젝트 내부의 다른 경로를 지정할 수 있다.

```powershell
.\build-release.ps1 -ArtifactsDirectory artifacts\tmp\release-validation
```

## 변경 원칙

- 이미 배포한 버전 번호로 다른 바이너리를 다시 배포하지 않는다.
- 코드가 바뀌어도 모든 커밋에서 버전을 올릴 필요는 없다. 실제 배포를 준비할 때 올린다.
- 릴리스 ZIP, 실행 파일 속성, 릴리스 노트의 버전은 반드시 일치해야 한다.
- 데이터 마이그레이션이 포함된 릴리스는 이전 스키마에서의 자동 테스트와 백업 동작을 함께 검증한다.
