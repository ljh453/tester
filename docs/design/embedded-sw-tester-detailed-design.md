# 임베디드 SW 테스터 상세 설계서

작성일: 2026-05-15  
상태: Draft for Implementation  
기준 릴리스: 1차 릴리스

## 1. 문서 목적

본 문서는 단일 Windows PC 환경에서 사용하는 임베디드 SW 테스트 저작 및 실행 도구의 상세 설계를 정의한다. 제품은 YAML 기반 테스트케이스 저작, GUI/YAML 1:1 의미 대응, 외부 툴 연동 실행, 실시간 실행 가시화, 리포트 생성을 제공하는 개발자용 IDE 형태의 도구다.

1차 릴리스의 최우선 가치는 편집 편의보다 실행 신뢰성이다. 따라서 구현 순서는 IDE 전체 기능보다 DSL 컴파일러, 런타임, 어댑터 계약, 리포트 원본 구조를 먼저 안정화하는 방향으로 정한다.

## 2. 범위와 비범위

### 2.1 범위

- Windows 데스크톱 IDE
- YAML 텍스트 편집기와 GUI 테스트케이스 편집기
- 단일 워크스페이스 내 다수 YAML 파일 관리
- 선택한 YAML 파일 또는 특정 testcase 실행
- 함수 라이브러리 import
- 테스트 명령 DSL 파싱, 정규화, 검증, 컴파일, 실행
- Trace32, CANoe/CANalyzer, INCA, Serial 어댑터
- 실행 중 현재 위치, 지역 변수, 로그, assert 결과 표시
- 구조화 리포트와 증적 생성
- 자동완성, 진단, 실행 추적

### 2.2 비범위

- 중앙 서버 기반 다중 사용자 협업
- 원격 러너, 분산 실행, 스케줄링 서버
- 클라우드 기반 저장소
- Linux/macOS에서의 실제 장비 제어 실행
- 고급 테스트 관리 기능
- 외부 플러그인 마켓 수준 확장 구조

## 3. 핵심 제약 및 전제

- 사용자는 보안상 한 대의 PC에서만 사용한다.
- 실제 테스트 실행 환경은 Windows이다.
- 메인 애플리케이션은 C#/.NET 기반 Windows IDE로 구성한다.
- 실행 엔진은 Python으로 구성한다.
- INCA COM API는 32bit Python만 지원하므로 별도 32bit Python 헬퍼 프로세스로 격리한다.
- Trace32 RCL은 Python 기반으로 연동한다.
- GUI 편집과 YAML 편집은 동일한 테스트 의미를 편집해야 하며, YAML 포맷은 정규화 가능하다.
- 1차 릴리스는 단일 active run만 지원한다.

## 4. 사용자 및 주요 흐름

주요 사용자는 테스트 엔지니어, 자동화 파워 유저, YAML 직접 편집과 GUI 편집을 혼합 사용하는 사용자다.

기본 흐름:

1. 사용자가 워크스페이스를 연다.
2. YAML 에디터 또는 GUI 에디터로 테스트케이스를 작성한다.
3. 함수 라이브러리와 공통 툴 설정을 import 또는 참조한다.
4. 실행 전 스키마 검증과 진단을 확인한다.
5. 선택한 YAML 파일 또는 특정 testcase를 실행한다.
6. 실행 중 현재 단계, 지역 변수, 콘솔 로그, assert 결과를 본다.
7. 실행 종료 후 보고서와 증적을 확인한다.

## 5. 상위 아키텍처

제품은 3개 계층으로 구성한다.

1. Windows IDE 계층
2. Python 실행 엔진 계층
3. 외부 툴 어댑터 계층

### 5.1 Windows IDE 계층

책임:

- 워크스페이스 관리
- YAML 편집기
- GUI 테스트케이스 편집기
- 자동완성 및 진단 표시
- 실행 시작/중지/일시정지 제어
- 현재 실행 위치, 지역 변수, 로그, 리포트 표시

기술:

- C#/.NET 기반 Windows 데스크톱 애플리케이션
- Eclipse 스타일 도킹 가능한 워크벤치 UI

### 5.2 Python 실행 엔진 계층

책임:

- YAML 파싱 및 import 해석
- 테스트 모델 정규화
- 명령 DSL 검증 및 컴파일
- 테스트 실행 오케스트레이션
- 제어문, 함수 호출, 변수 스코프, assert, logging, eval 처리
- 구조화된 실행 이벤트 및 리포트 데이터 생성

### 5.3 외부 툴 어댑터 계층

책임:

- 외부 장비/도구 API 호출 캡슐화
- 공통 오류 처리 및 timeout 처리
- 명령별 입력/출력 구조 통일
- 실행 로그와 증적 생성

## 6. 프로세스 및 IPC 구조

### 6.1 프로세스

- C# IDE 프로세스: 사용자 상호작용, 편집, 도킹 UI, 실행 모니터링
- Python 실행 엔진 프로세스: YAML 로드, 컴파일, 실행, 이벤트 생성
- 32bit Python INCA 브리지 프로세스: INCA COM 세션, Calibration, Measurement read, Recording 제어

### 6.2 IPC 결정

1차 릴리스의 IDE와 Python 실행 엔진 사이 IPC는 JSON-RPC over stdio를 기본으로 한다.

이유:

- IDE가 Python 프로세스 생명주기를 직접 소유하기 쉽다.
- 네트워크 포트 관리가 필요 없다.
- 개발 중 로그와 raw message를 재현하기 쉽다.
- 단일 PC, 단일 active run 제약과 잘 맞는다.

이벤트 스트리밍은 JSON-RPC notification으로 처리한다. 실행 엔진은 compile diagnostics, run state, command event, variable snapshot, log line, report completed event를 순차적으로 내보낸다.

### 6.3 INCA 브리지 IPC

메인 실행 엔진과 32bit INCA 브리지 사이도 동일한 메시지 패턴을 따른다. 단, 브리지 경계 밖으로 COM object, 32bit 세부 타입, INCA 내부 예외 객체를 노출하지 않는다. 브리지는 일반 RPC 결과만 반환한다.

## 7. 워크스페이스 및 파일 모델

워크스페이스는 여러 YAML 파일과 공통 리소스를 묶는 관리 단위다.

예시 구조:

```text
workspace/
  project.workspace.json
  tool-profiles/
    vehicle-a.tools.yaml
  libs/
    common-power-sequence.yaml
    can-diagnostics.yaml
  tests/
    boot-smoke.yaml
    flash-and-verify.yaml
  reports/
    2026-05-15_101530_boot-smoke/
```

실제 실행 의미의 소스는 선택한 YAML 파일이다. 리포트는 원본 파일, 원본 파일 hash, import 해석 결과, resolved package, tool 설정 snapshot을 함께 남긴다.

## 8. YAML DSL 설계

### 8.1 명령 표기 규칙

사용자가 작성하는 표면 문법은 `cmd:` 키를 사용하지 않고 명령 이름을 키로 사용한다.

```yaml
steps:
  - inca.measure.read:
      signal: EngineSpeed
      save_as: rpm
  - assert.gt:
      left: "${rpm}"
      right: 0
```

인자가 없는 명령은 빈 mapping으로 쓴다.

```yaml
- canoe.measurement.start: {}
```

### 8.2 내부 정규화 규칙

파서는 명령을 다음 내부 모델로 정규화한다.

```json
{
  "type": "inca.measure.read",
  "args": {
    "signal": "EngineSpeed",
    "save_as": "rpm"
  },
  "path": ["testcases", 0, "steps", 0],
  "source": {
    "file": "tests/boot-smoke.yaml",
    "line": 12,
    "column": 5
  }
}
```

`source`는 1차 구현에서 best-effort로 제공한다. PyYAML 기반 초기 구현에서는 line/column이 없을 수 있으나, diagnostics와 GUI 연동을 위해 모델 필드는 유지한다.

### 8.3 표현식 규칙

- `${...}`는 값 표현식이다.
- 문자열 안의 `{{ ... }}`는 템플릿 보간이다.
- Python `eval` 직접 실행은 금지한다.
- 1차 릴리스는 제한된 expression evaluator를 제공하고, 허용 연산과 허용 함수만 명령 카탈로그에 명시한다.
- 평가 시점의 원본 표현식과 실제 값은 실행 이벤트에 남긴다.

### 8.4 제어문과 함수

`if`, `for`, `call`도 타입화된 명령이다. 함수는 `params`, `returns`, `steps`를 가진 재사용 명령 집합으로 정의한다. 하위 function으로 값 전달은 `args`로만 하고, 상위 scope로의 반환은 `returns`와 `out` 매핑으로만 한다.

### 8.5 Cleanup 분리

`postconditions`는 결과 검증 단계다. 실패나 사용자 중단에도 가능한 정리가 필요하면 `cleanup`을 별도 단계로 둔다. 1차 릴리스에서 YAML 구조는 `preconditions`, `steps`, `postconditions`, `cleanup`을 허용한다.

### 8.6 Serial 장비 명령 Profile

Serial로 직접 제어하는 장비는 low-level `serial.write/read`와 별개로 장비 의미 명령을 제공할 수 있다. 장비 의미 명령은 tool profile의 `serial.devices.<name>.command_profile`을 통해 실제 TX/RX sequence로 해석한다.

1차로 고정된 serial 조작 대상:

- `power_supply`: 입력 포맷 미확정. `command_profile: pending` 상태에서는 `power_supply.command` 사용 시 compile error로 차단한다.
- `mach_systems_sent_usb`: Mach Systems SENT-USB interface. `sent_usb.read`는 profile에 정의된 `write` template과 `read` matcher로 실행한다.

예시:

```yaml
serial:
  devices:
    sent_usb:
      device_type: mach_systems_sent_usb
      port: COM4
      baudrate: 115200
      command_profile: sent_usb_line
command_profiles:
  sent_usb_line:
    commands:
      sent_usb.read:
        write: "READ SENT {{ channel }}"
        read:
          until: "VALUE"
```

이 구조는 장비별 serial protocol이 확정되면 YAML profile만 교체해도 같은 테스트 의미를 유지하기 위한 경계다.

## 9. 파싱, 검증, 컴파일

실행 전 compile-like phase를 반드시 거친다.

단계:

1. YAML 로드
2. import 해석
3. 기본 스키마 검증
4. 명령 정규화
5. symbol table 생성
6. 함수 시그니처 바인딩
7. 변수 참조 검증
8. 명령별 인자 검증
9. resolved run package 생성

검증 오류 예:

- 존재하지 않는 function 호출
- 잘못된 명령 이름
- 필수 인자 누락
- 선언되지 않은 반환값 매핑
- import 충돌
- 잘못된 변수 참조
- 선언되지 않은 serial device
- `pending` command profile을 참조하는 장비 의미 명령
- 존재하지 않는 command profile 또는 profile 내 command 누락

## 10. 명령 카탈로그

명령 카탈로그는 컴파일러, 자동완성, GUI form renderer, 런타임 adapter dispatch가 공유하는 계약이다.

명령 정의 필드:

- command type
- category
- required args
- optional args
- arg type
- result variables
- timeout policy
- adapter name
- evidence policy

1차 릴리스는 최소 카탈로그로 시작한다.

- `set`
- `delay`
- `log.text`
- `log.value`
- `assert.eq`
- `assert.gt`
- `assert.fail`
- `call`
- `if`
- `for`
- `serial.write`
- `serial.read`
- `sent_usb.read`
- `power_supply.command`
- `trace32.command`
- `canoe.measurement.start`
- `canoe.measurement.stop`
- `inca.measure.read`

## 11. Resolved Run Package

실행 직전 최종 해석된 패키지를 고정한다.

포함 내용:

- `schema_version`
- `engine_version`
- `source_file`
- `source_file_hash`
- import 반영 후 최종 함수 정의
- 실행 대상 testcase 목록
- 정규화된 명령 트리
- tool 설정 snapshot
- compile diagnostics

리포트는 원본 YAML뿐 아니라 resolved run package를 함께 보관해야 한다.

## 12. 실행 엔진 상세

### 12.1 상태기계

상태:

- Idle
- Compiling
- Ready
- Running
- Paused
- Stopping
- Completed
- Failed
- Aborted

Pause는 명령 경계에서만 보장한다. Stop은 cooperative cancellation과 adapter timeout으로 처리한다.

### 12.2 실행 프레임

testcase와 function call마다 독립 프레임을 생성한다.

프레임 정보:

- local variables
- current command index
- parent reference
- start/end time
- phase

### 12.3 실패 정책

- Preconditions 실패: 현재 testcase 실패 처리, steps 미실행
- Steps 실패: 기본 즉시 중단, `on_step_failure: continue`인 경우 계속 수행
- Postconditions: 검증 단계로 실행
- Cleanup: 가능한 경우 실패/중단 이후에도 실행 시도

### 12.4 실행 이벤트

모든 명령은 구조화된 이벤트를 내보낸다.

필드:

- run_id
- testcase
- phase
- command_path
- command_type
- start_time
- end_time
- status
- resolved_inputs
- outputs
- error
- attachments

## 13. 외부 툴 어댑터 설계

공통 인터페이스:

- connect
- validate_configuration
- execute(command_type, args, context)
- cleanup
- health_check

공통 결과:

- success
- status
- message
- values
- raw_evidence_ref
- duration_ms

구현 순서는 mock adapter, Serial, Trace32, CANoe/CANalyzer, INCA 순서로 진행한다. 외부 장비 없이도 DSL 컴파일러와 런타임 테스트가 가능해야 한다.

## 14. IDE UX 및 워크벤치 구조

UI는 일반 대시보드형 앱이 아니라 Eclipse 스타일 개발자용 IDE 워크벤치로 설계한다.

기본 뷰:

- Project Explorer
- Test Explorer
- YAML Editor
- GUI Testcase Editor
- Outline / Properties
- Console / Execution Trace
- Variables
- Problems / Diagnostics
- Report Viewer

YAML이 유효하지 않거나 정규화가 불가능한 경우 GUI 편집기는 read-only 상태로 전환하고 Problems view에 진단을 표시한다.

## 15. 리포트 및 증적 구조

리포트는 사람용 요약과 기계용 원본을 함께 남긴다.

```text
reports/<run-id>/
  summary.html
  run.json
  resolved-package.yaml
  testcase-results/
  attachments/
  raw-logs/
```

`run.json`은 원본 데이터, `summary.html`은 파생 렌더링 결과다. 모든 report schema에는 `schema_version`을 포함한다.

## 16. 1차 릴리스 구현 슬라이스

- M0: CLI compiler + diagnostics
- M1: mock runtime + report
- M2: IDE에서 실행/로그/변수/문제 보기
- M3: Serial + Trace32 실제 연동
- M4: CANoe + INCA bridge 연동
- M5: GUI editor + YAML round-trip 안정화

초기 구현 착수점은 DSL Compiler Core다. YAML을 신뢰 가능한 resolved package로 바꾸는 부분이 안정되어야 IDE, GUI editor, adapter runtime이 흔들리지 않는다.

## 17. 테스트 전략

### 17.1 단위 테스트

- YAML parser
- DSL normalizer
- semantic validator
- symbol resolver
- expression evaluator
- function call scope handling

### 17.2 통합 테스트

- IDE와 실행 엔진 간 IPC
- 실행 엔진과 32bit INCA 브리지 간 통신
- 각 어댑터 mock 기반 실행 흐름

### 17.3 시스템 테스트

- 실제 Windows 환경
- 실제 Trace32, CANoe/CANalyzer, INCA, Serial 장비 연결
- 실패/중단/cleanup 시나리오

### 17.4 회귀 테스트

- 축약 YAML 문법이 GUI 편집 후에도 동일 의미를 유지하는지
- import 해석 결과가 재현 가능한지
- 리포트 구조가 버전 변경 후에도 파싱 가능한지

## 18. 남은 구현 전환 포인트

- IDE 도킹 UI 프레임워크 선정
- C# IPC client 상세 구현
- source span을 보존하는 YAML parser 도입 여부
- 각 외부 툴 실제 command catalog 확장
- 리포트 HTML 템플릿 세부 UI
