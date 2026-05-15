# GUI Workbench MVP Design

작성일: 2026-05-15
상태: Draft for Review

## 1. 목적

GUI 1차 작업은 기존 Python 실행 엔진을 버리지 않고 감싸는 Windows IDE shell을 만든다. 목표는 사용자가 워크스페이스를 열고 YAML 파일을 선택해 compile/run/report를 수행하며, 문제 목록과 실행 이벤트를 한 화면에서 확인하는 것이다.

이번 범위는 "IDE Workbench MVP"이다. GUI Testcase Editor와 완전한 YAML/GUI 양방향 편집은 다음 단계로 남긴다.

## 2. 범위

포함:

- Windows 데스크톱 GUI shell
- Project Explorer
- YAML Editor 영역
- Problems / Diagnostics view
- Console / Execution Trace view
- Variables view
- Report view 또는 report file opener
- Python 엔진을 호출하는 compile/run command path
- 실행 결과와 report 경로 표시

비포함:

- GUI Testcase Editor의 command form 편집
- 완전한 도킹 프레임워크 커스터마이징
- 원격 runner
- 실제 장비 연결 wizard
- 실시간 JSON-RPC 스트리밍의 완성형 구현

## 3. 권장 기술 방향

최종 제품은 C#/.NET Windows IDE이다. 1차 GUI는 `.NET 8` 기반 `WPF`로 시작하고, Python 엔진은 별도 프로세스로 호출한다.

초기 IPC는 CLI subprocess 호출을 사용한다. GUI가 `embsw-tester compile` 또는 `embsw-tester run`을 실행하고 JSON stdout 및 report output을 읽는다. 이후 실시간 streaming이 필요해지면 설계서의 JSON-RPC over stdio로 확장한다.

이 선택은 현재 Python 코드와 pytest 자산을 그대로 활용하면서, Windows 제품 방향과도 맞다.

## 4. 워크벤치 레이아웃

기본 화면은 Eclipse 스타일을 따른다.

- 왼쪽: Project Explorer
- 중앙: YAML Editor
- 오른쪽: Outline / Properties
- 하단: Problems, Console, Execution Trace 탭
- 하단 또는 오른쪽 보조: Variables, Report

MVP에서는 완전한 드래그 도킹보다 고정 split layout과 tab 영역을 먼저 제공한다. Perspective 저장/복원은 layout state JSON 저장으로 확장 가능하게 둔다.

## 5. 데이터 흐름

1. 사용자가 workspace folder를 선택한다.
2. GUI가 `tests`, `libs`, `tool-profiles`, `reports` 폴더를 탐색해 Project Explorer에 표시한다.
3. 사용자가 YAML 파일을 연다.
4. YAML Editor가 파일 내용을 표시한다.
5. Compile 버튼을 누르면 GUI가 Python engine compile command를 호출한다.
6. Diagnostics를 Problems view에 표시한다.
7. Run 버튼을 누르면 GUI가 Python engine run command를 호출한다.
8. Run JSON을 Execution Trace, Variables, Report view로 변환해 표시한다.

## 6. GUI와 Python 엔진 경계

GUI는 Python 내부 module을 직접 import하지 않는다. 모든 engine interaction은 process boundary를 지난다.

초기 contract:

- 입력: workspace path, selected YAML path, optional run id, reports root
- 출력: diagnostics JSON, run result JSON, report path
- 오류: process exit code, stderr, parse failure를 별도 GUI error로 표시

이후 contract:

- JSON-RPC request/response
- compile diagnostics notification
- run state notification
- command event notification
- variable snapshot notification
- report completed notification

## 7. 에러 처리

- Python executable 또는 package를 찾지 못하면 시작 화면에 engine setup error를 표시한다.
- compile error는 실행을 막고 Problems view로 이동한다.
- run failure는 Execution Trace에서 실패 command를 선택하고 error detail을 표시한다.
- report 생성 실패는 run 결과와 분리해서 표시한다.

## 8. 테스트 전략

Python 쪽은 기존 pytest를 유지한다.

GUI 쪽은 다음 수준으로 테스트한다.

- ViewModel 단위 테스트: workspace scan, command process result parsing, diagnostics mapping
- Process runner fake 테스트: stdout/stderr/exit code 처리
- Snapshot 성격의 layout state 테스트

WPF UI 자동화 테스트는 첫 단계에서 필수로 두지 않는다. 대신 ViewModel을 순수 C# class로 분리해 빠르게 검증한다.

## 9. 첫 구현 단위

Phase 23은 GUI skeleton과 engine subprocess bridge를 목표로 한다.

완료 기준:

- `apps/TesterWorkbench` WPF project 생성
- workspace folder path를 받아 sample workspace tree 표시
- YAML file text 표시
- compile command를 실행하고 diagnostics를 Problems view에 표시
- run command를 실행하고 run status/report path를 표시
- README에 GUI 실행 방법 추가

## 10. 다음 단계

Phase 24 이후 후보:

- Execution Trace와 Variables 상세 table
- Report HTML embedded viewer
- Monaco 기반 YAML editor 또는 AvalonEdit 적용
- JSON-RPC over stdio streaming bridge
- GUI Testcase Editor command form
