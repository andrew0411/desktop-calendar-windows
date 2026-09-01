# 문서 안내

이 디렉터리에는 현재 프로젝트 기준, 릴리스별 기록과 과거 작업 자료를 목적별로 분리해 보관한다.

## 현재 기준 문서

| 문서 | 역할 | 갱신 시점 |
| --- | --- | --- |
| [ARCHITECTURE.md](ARCHITECTURE.md) | 프로젝트 경계, 런타임 흐름, 데이터와 Windows 통합 구조 | 구조나 핵심 설계가 바뀔 때 |
| [DEVELOPMENT.md](DEVELOPMENT.md) | 개발 명령, 검증, 저장소 운영, 의존성과 자동화 정책 | 개발 절차가 바뀔 때 |
| [VERSIONING.md](VERSIONING.md) | 버전 단일 기준, 릴리스 절차와 산출물 규칙 | 버전·배포 정책이 바뀔 때 |

루트 [README.md](../README.md)는 제품 소개와 빠른 시작만 담당한다. 상세 구현 설명이나 세션별 작업 기록을 누적하지 않는다.

## 릴리스 기록

- [v0.2.1](releases/v0.2.1.md)
- [v0.2.0](releases/v0.2.0.md)

새 릴리스 노트는 `docs/releases/vX.Y.Z.md` 형식으로 추가한다. 사용자에게 보이는 기능·수정·실행 조건만 기록하고 내부 작업 일지는 넣지 않는다.

## 보관 자료

- [2026-08-16 작업 인수인계](archive/SESSION_HANDOFF_2026-08-16.md)

`archive`의 문서는 당시 맥락을 확인하기 위한 기록이며 현재 기준이 아니다. 유효한 장기 정보는 현재 기준 문서에 반영하고, 보관 문서를 다시 누적 갱신하지 않는다.

## 라이선스 문서

법적 문서의 유일한 원본은 저장소 루트의 [LICENSE](../LICENSE)와 [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md)다. `artifacts/win-x64`와 `artifacts/win-x64-slim`의 동명 파일은 독립 배포에 포함되어야 하는 생성 사본이다.

- 루트 파일만 직접 수정한다.
- 배포 사본을 직접 편집하지 않는다.
- `build-release.ps1`이 원본을 복사하고 SHA-256 일치를 검사한다.
- `artifacts`는 Git에서 제외되므로 소스 관리상 중복 원본은 존재하지 않는다.
