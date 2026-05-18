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

1차 릴리스의 IDE와 Python 실행 엔진 사이 IPC는 IDE가 소유하는 CLI subprocess 기반 계약을 기본으로 한다.

계약:

- Compile: Python CLI stdout에 최종 compile JSON을 1회 출력한다.
- Run: Python CLI stdout에 최종 run JSON을 1회 출력한다.
- Event streaming: stderr의 `__EMBSW_EVENT__ ` prefix 뒤에 command event JSON을 JSONL로 출력한다.
- Standard error: prefix가 없는 stderr line은 일반 오류/로그로 유지한다.
- Debug control: IDE가 run별 `control.json` 파일을 만들고 Python runtime이 cooperative 방식으로 poll한다.

이유:

- IDE가 Python 프로세스 생명주기를 직접 소유하기 쉽다.
- 네트워크 포트 관리가 필요 없다.
- 개발 중 로그와 raw message를 재현하기 쉽다.
- 단일 PC, 단일 active run 제약과 잘 맞는다.

`control.json` 상태는 `running`, `paused`, `stopping`을 사용한다. `breakpoint_lines`는 실행 중에도 최신 파일 내용을 기준으로 적용한다. IDE는 한 번에 하나의 active run만 허용하며, 중복 Run 요청은 새 subprocess를 만들지 않는다.

이벤트 스트리밍은 command event 단위로 처리한다. 실행 엔진은 `running`, `paused`, `passed`, `failed`, `aborted` 상태 이벤트를 순차적으로 내보내고, IDE는 이를 Console, Execution Trace, Variables, YAML 현재 line 표시의 단일 근거로 사용한다.

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

- `power_supply`: VuPower K Series USB-to-Serial protocol. `power_supply.command`는 `protocol: vupower_k_usb`에서 SCPI 스타일 line command로 변환한다.
- `mach_systems_sent_usb`: Mach Systems SENT-USB interface. 1차 구현은 Mach Systems SENT Gateway Communication Protocol Specification의 `STX LEN ID DATA CHKSUM ETX` binary frame을 기준으로 `sent_usb.read` receive-side fast frame과 `sent_usb.command` control/transmit 명령을 처리한다.

예시:

```yaml
serial:
  devices:
    psu:
      device_type: power_supply
      port: COM3
      baudrate: 9600
      parity: none
      stop_bits: 1
      byte_size: 8
      command_profile: vupower_k_usb
    sent_usb:
      device_type: mach_systems_sent_usb
      port: COM4
      baudrate: 115200
      parity: none
      stop_bits: 1
      byte_size: 8
      command_profile: mach_sent_gateway
command_profiles:
  vupower_k_usb:
    commands:
      power_supply.command:
        protocol: vupower_k_usb
        channel: P1
  mach_sent_gateway:
    commands:
      sent_usb.read:
        protocol: mach_sent_gateway
      sent_usb.command:
        protocol: mach_sent_gateway
        read_ack: true
```

실제 장비 smoke용 profile은 최상위 `execution` 섹션을 둘 수 있다.

```yaml
execution:
  requires_real_hardware: true
  allow_env: EMBSW_TESTER_ALLOW_REAL_HARDWARE
```

`requires_real_hardware`가 true이면 CLI `run`은 기본적으로 실행을 차단하고 `REAL_HARDWARE_CONFIRMATION_REQUIRED` 진단을 반환한다. 사용자는 장비 연결, COM 포트, helper 실행 경로를 확인한 뒤 `--allow-real-hardware` 또는 `allow_env` 환경변수로 명시적으로 허용해야 한다. 이 guard는 mock registry로 실장비 smoke YAML이 false pass 되는 상황도 막는다.

Serial framing 설정은 `baudrate`, `parity`, `stop_bits`, `byte_size`, `timeout_ms`를 기본 필드로 가진다. `parity`는 `none/even/odd/mark/space`, `stop_bits`는 `1/1.5/2`, `byte_size`는 `5/6/7/8`을 허용한다. GUI Workbench는 `Tools > Settings...` 창에서 현재 YAML의 `tool_profile`이 가리키는 serial device framing 값을 편집하고 저장할 수 있어야 한다. 같은 Settings 창에는 theme처럼 자주 바뀌지 않는 workbench 설정을 함께 둔다.

`vupower_k_usb` protocol은 [VuPower K USB Manual Korea Ver3.2](http://www.vupower.com/download/K_USB_Manual_Korea_Ver3.2.pdf)를 따른다. 해당 장비는 USB-to-Serial 방식이며, 명령은 한 번에 하나씩 전송하고 LF로 종료한다. command와 첫 번째 parameter는 공백으로, parameter 사이는 쉼표로 구분한다. 1차 구현은 `APPL`, `SOUR:VOLT`, `SOUR:CURR`, `OUTP:STAT`, `OUTP:STAT?`, `MEAS:VOLT?`, `MEAS:VOLTA?`, `MEAS:CURR?`, `MEAS:CURRA?`, `*IDN?`, `*RST`, `SYST:ERR?`를 대상으로 한다. 측정/조회 응답은 float, bool, mode 문자열, error code 등 타입화된 값으로 변환해 `save_as`에 저장한다.

`mach_sent_gateway` protocol은 channel 1 fast frame message id `100`, channel 2 fast frame message id `200`을 수신 대상으로 한다. payload는 status nibble, data nibble count, data nibbles, received CRC, calculated CRC로 파싱하며 `save_as`에는 파싱된 dict를 저장한다. `sent_usb.command`는 configuration write, start, stop, fast transmit, single slow transmit, slow message multiplex buffer transmit을 지원한다. channel 1/2 message id는 config `2/12`, start `21/31`, stop `22/32`, fast transmit `41/51`, single slow transmit `42/52`, slow multiplex buffer transmit `43/53`이다. 단일 slow transmit payload는 slow message id, 16-bit data little-endian, 6-bit CRC received, slow/enhanced type bit, enhanced format bit, 6-bit calculated CRC로 직렬화한다. Slow message multiplex buffer payload는 5-bit buffer index, enable bit, enhanced config bit, 8-bit slow message id, 16-bit little-endian data로 직렬화한다. 버퍼 disable은 `enabled: false`와 `buffer_index`만으로 허용한다. ACK 응답은 동일 message id의 one-byte status로 해석하며 `1`은 OK, `0`은 ERR로 처리한다. binary evidence는 JSON 호환성을 위해 hex string으로 남긴다. 참고 사양은 [Mach Systems SENT Gateway Communication Protocol Specification](https://www.machsystems.cz/support/SENT%20Gateway-Communication%20Protocol%20Specification_latest.pdf)이다.

기존 line 기반 serial profile도 유지한다. command profile에 `write`/`read` mapping이 있으면 `write` template 전송 후 `read.until` substring, `read.match` regex, 선택적 `read.extract` regex로 응답을 판정한다. 둘 다 제공되면 둘 다 통과해야 한다. `read.extract`는 `save_as`에 저장할 값을 raw serial 응답에서 추출할 때 사용한다. named group `value`가 있으면 그 값을 저장하고, named group이 없으면 첫 번째 capture group, capture group도 없으면 전체 match를 저장한다. raw serial evidence와 실행 이벤트의 nested serial output은 그대로 유지한다.

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
- 잘못된 `read.match` regex
- 잘못된 `read.extract` regex

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
- `serial.write_bytes`
- `serial.read_bytes`
- `sent_usb.read`
- `sent_usb.command`
- `power_supply.command`
- `trace32.command`
- `canoe.measurement.start`
- `canoe.measurement.stop`
- `canoe.sysvar.set`
- `canoe.sysvar.read`
- `canoe.signal.read`
- `inca.measure.read`
- `inca.calibration.set`
- `inca.recording.start`
- `inca.recording.stop`

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

Pause는 명령 경계에서 보장한다. Breakpoint도 명령 line에서만 pause를 발생시킨다. Stop은 `stopping` control state를 통해 다음 명령 경계에서 `aborted` 이벤트로 종료한다. 장기 `delay` 명령은 control state를 짧은 interval로 poll하여 delay 중 stop/pause에도 UI가 응답성을 유지하도록 한다. 외부 adapter 호출 중인 작업은 adapter timeout과 helper process 경계로 격리한다.

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
- Cleanup: 가능한 경우 실패 이후에는 실행 시도한다. 사용자가 Stop을 요청한 경우에는 새 DSL cleanup command를 시작하지 않고, 장비 정리는 adapter/helper timeout 및 추후 adapter cleanup 경계에서 처리한다.

### 12.4 실행 이벤트

모든 명령은 구조화된 이벤트를 내보낸다.

필드:

- run_id
- testcase
- phase
- command_path
- command_type
- status
- resolved_inputs
- outputs
- local_variables
- error
- source_file
- source_line
- attachments

status 의미:

- `running`: 해당 명령 실행 시작
- `paused`: breakpoint 또는 pause 요청으로 명령 실행 전에 정지
- `passed`: 명령 성공
- `failed`: assertion failure, command error, adapter error
- `aborted`: 사용자의 stop 요청으로 실행 중단

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

구현 순서는 mock adapter, Serial, Trace32 contract, CANoe/CANalyzer contract, INCA 순서로 진행한다. 외부 장비 없이도 DSL 컴파일러와 런타임 테스트가 가능해야 한다.

### 13.1 Serial Adapter Contract

Serial adapter는 text line protocol과 binary protocol을 모두 지원한다.

초기 명령:

- `serial.write`: UTF-8 text line을 전송하고 `TX` evidence를 남긴다.
- `serial.read`: line을 읽고 substring/regex matcher로 응답을 판정하며 `RX` evidence를 남긴다.
- `serial.write_bytes`: hex string을 bytes로 전송하고 `TX_HEX` evidence를 남긴다.
- `serial.read_bytes`: 지정한 byte 수를 읽고 `RX_HEX` evidence와 `data_hex` 값을 남긴다.

Mach Systems SENT Gateway와 같이 binary frame을 사용하는 장비 의미 명령은 low-level byte command를 nested serial step으로 실행하고, 상위 device command output에는 파싱된 의미 값만 노출한다.

### 13.2 Trace32 Adapter Contract

1차 구현의 Trace32 adapter는 transport contract adapter로 시작한다. 기본 transport는 RCL이며, RCL transport가 실패하거나 사용할 수 없는 경우 UDP command transport로 fallback한다.

초기 명령:

- `trace32.command`: Trace32 command 문자열을 실행하고 응답 값을 `save_as`로 저장 가능하다.
- `trace32.command_sequence`: 동일 transport/fallback 정책으로 여러 Trace32 command 문자열을 순서대로 실행한다. flash, memory, symbol 계열은 현장 PRACTICE script와 프로젝트 설정 차이가 크므로, 별도 DSL 명령으로 고정하기 전 이 명령과 후보 문서로 검증한다.

정책:

- 기본값은 `transport: rcl`이다.
- `transport: udp`를 명시하면 UDP command transport만 사용한다.
- `fallback`은 기본 `true`이며, RCL 실패 시 UDP transport를 시도한다.
- adapter result에는 실제 사용한 `transport`, `fallback_used`, `attempts`를 남겨 리포트에서 RCL 실패와 UDP fallback 여부를 추적 가능하게 한다.

`RclTrace32Transport`는 주입된 RCL client의 command method를 호출하는 wrapper다. RCL Python 패키지별 세부 API 차이는 client factory 또는 `command_method` 설정으로 흡수한다. `UdpTrace32Transport`는 UDP socket으로 command 문자열과 terminator를 전송하고 응답 datagram을 읽는다. 두 transport 모두 `Trace32CommandTransport.execute_command(command, timeout_ms)` 경계를 만족하며, DSL command type과 AdapterResult shape는 유지한다.

Trace32 tool profile은 아래 하위 섹션을 가진다.

- `trace32.rcl.enabled`: RCL transport 사용 여부
- `trace32.rcl.client_factory`: 선택 사항인 `module:attribute` 형식의 RCL client factory
- `trace32.rcl.command_method`: RCL client에서 command를 실행할 method 이름, 기본 `cmd`
- `trace32.rcl.client_args`: RCL client factory에 전달할 keyword argument snapshot
- `trace32.udp.enabled`: UDP fallback transport 사용 여부
- `trace32.udp.host`, `trace32.udp.port`: UDP endpoint
- `trace32.udp.terminator`, `trace32.udp.encoding`, `trace32.udp.response_bytes`: UDP command framing과 응답 크기

IDE나 테스트 harness는 import path 대신 RCL client factory를 직접 주입할 수 있다. 이 경로는 Windows 장비 smoke에서 실제 RCL package 생성 책임을 adapter 내부가 아니라 외부 composition layer에 둔다.

### 13.3 CANoe/CANalyzer Adapter Contract

1차 구현의 CANoe/CANalyzer adapter는 장비 없는 개발/회귀 테스트를 위한 in-memory contract adapter와 Windows Python COM helper 경로를 함께 가진다. 목적은 DSL 명령, adapter result 구조, runtime `save_as` 동작, report event schema, helper RPC payload schema를 고정하면서도 실제 Vector COM 호출을 프로세스 경계 뒤에 격리하는 것이다.

초기 명령:

- `canoe.measurement.start`: measurement running 상태를 시작한다. 실제 COM helper에서는 선택적으로 `configuration`을 열 수 있다.
- `canoe.measurement.stop`: measurement running 상태를 중지한다.
- `canoe.sysvar.set`: `namespace::name` system variable 값을 설정한다.
- `canoe.sysvar.read`: `namespace::name` system variable 값을 읽고 `save_as`로 저장 가능하다.
- `canoe.signal.read`: signal 값을 읽고 `save_as`로 저장 가능하다. 실제 COM helper에서는 `bus`, `channel`, `message`, `signal`을 사용한다.

Vector CANoe 14 COM 도움말 기준 실제 helper는 아래 API shape를 사용한다.

- CANoe ProgID: `CANoe.Application`
- CANalyzer ProgID: `CANalyzer.Application`
- Measurement: `Application.Measurement.Start()` / `Application.Measurement.Stop()`
- System variable: `Application.System.Namespaces(namespace).Variables(name).Value`
- Signal: `Application.Bus(bus).GetSignal(channel, message, signal).Value`

초기 helper transport는 JSON line 기반 stdio RPC다.

- 실행 엔진은 `JsonLineCanoeBridgeTransport`를 통해 helper process stdin에 request JSON 한 줄을 쓴다.
- helper는 동일한 `request_id`를 가진 response JSON 한 줄을 stdout으로 반환한다.
- `timeout_ms`는 command args와 분리된 top-level field로 전달한다. Measurement start/stop에서는 `Measurement.Running`이 목표 상태가 될 때까지 해당 timeout 안에서 polling한다.
- helper response의 `duration_ms`는 `AdapterResult.duration_ms`와 report event output으로 전달한다.
- response id 불일치, 빈 응답, invalid JSON, IO 오류는 failed `AdapterResult`로 변환한다.
- `CanoeAdapter`는 `bridge_transport`가 주입된 경우 CANoe/CANalyzer 명령을 bridge로 위임하고, 주입되지 않은 경우 기존 in-memory contract adapter로 동작한다.

CANoe tool profile은 helper process 실행 command를 composition layer에서 구성할 수 있게 한다.

- `canoe.helper.enabled`: helper-backed CANoe/CANalyzer adapter 사용 여부
- `canoe.helper.command`: Python 실행 파일과 helper module/path argument를 포함하는 command sequence
- `canoe.helper.application`: `canoe` 또는 `canalyzer`, 기본 `canoe`
- `canoe.helper.prog_id`: 선택 사항인 명시 COM ProgID override

profile 기반 adapter registry는 `canoe.helper`가 있으면 `create_canoe_bridge_process_transport`로 helper process transport를 만들고 `CanoeAdapter(bridge_transport=...)`를 등록한다. CLI 기본 실행은 계속 mock adapter를 사용하며, 실제 장비 실행 경로에서만 profile-backed registry를 명시적으로 구성한다.

### 13.4 INCA Adapter Contract

1차 구현의 INCA adapter는 장비 없는 개발/회귀 테스트를 위한 in-memory contract adapter와 Windows 32bit Python COM helper 경로를 함께 가진다. 목적은 DSL 명령, adapter result 구조, runtime `save_as` 동작, report event schema, 32bit helper RPC payload schema를 고정하면서도 실제 INCA Tool-API 호출을 프로세스 경계 뒤에 격리하는 것이다.

초기 명령:

- `inca.measure.read`: measurement variable 값을 읽고 `save_as`로 저장 가능하다. 실제 COM helper에서는 선택적으로 `device`, `acquisition_rate`를 전달할 수 있다.
- `inca.calibration.set`: calibration parameter 값을 설정한다. 실제 COM helper에서는 선택적으로 `device`, `value_kind`(`phys`/`impl`)를 전달할 수 있다.
- `inca.recording.start`: recording 상태를 시작하고 선택적으로 `name`, `output_dir`, `file_format`을 설정한다.
- `inca.recording.stop`: recording 상태를 중지한다. 선택적으로 `file_name`, `file_format`, `discard`를 전달할 수 있다.

INCA COM API가 32bit Python에서만 사용 가능하므로, 실행 엔진은 `IncaBridgeRequest`와 `IncaBridgeResponse` 형태의 직렬화 가능한 메시지를 32bit helper 프로세스에 전달한다. 실제 COM helper는 `Inca.Inca` ProgID로 Tool-API application을 열고 `GetOpenedExperiment`, `GetMeasureValue*`, `GetCalibrationValue*`, `StartRecording`, `StopRecording` 계열 API를 호출한다. 실제 COM 세부사항은 helper 경계 밖으로 노출하지 않고, 런타임은 일반 `AdapterResult`만 받는다.

초기 helper transport는 JSON line 기반 stdio RPC다.

- 실행 엔진은 `JsonLineIncaBridgeTransport`를 통해 helper process stdin에 request JSON 한 줄을 쓴다.
- helper는 동일한 `request_id`를 가진 response JSON 한 줄을 stdout으로 반환한다.
- `timeout_ms`는 command args와 분리된 top-level field로 전달한다.
- response id 불일치, 빈 응답, invalid JSON, IO 오류는 failed `AdapterResult`로 변환한다.
- `IncaAdapter`는 `bridge_transport`가 주입된 경우 INCA 명령을 bridge로 위임하고, 주입되지 않은 경우 기존 in-memory contract adapter로 동작한다.

INCA tool profile은 helper process 실행 command를 composition layer에서 구성할 수 있게 한다.

- `inca.helper.enabled`: helper-backed INCA adapter 사용 여부
- `inca.helper.command`: 32bit Python 실행 파일과 helper script/path argument를 포함하는 command sequence

profile 기반 adapter registry는 `inca.helper`가 있으면 `create_inca_bridge_process_transport`로 helper process transport를 만들고 `IncaAdapter(bridge_transport=...)`를 등록한다. CLI 기본 실행은 계속 mock adapter를 사용하며, 실제 장비 실행 경로에서만 profile-backed registry를 명시적으로 구성한다.

CLI 실행 모드는 두 가지를 구분한다.

- 기본 `run`: `tool_profile`을 resolved package에 보관하지만 adapter registry는 mock을 유지한다.
- `run --use-tool-profile-adapters`: resolved `tool_profile_snapshot`을 사용해 Serial, Trace32, CANoe/CANalyzer, INCA adapter registry를 구성한다.

이 구분은 로컬/CI smoke가 실제 장비를 건드리지 않게 하면서도 Windows 장비 연결 smoke에서는 같은 YAML과 같은 profile snapshot으로 실제 adapter 경로를 실행하기 위한 안전 장치다. 실장비 전용 profile은 추가로 `execution.requires_real_hardware` guard를 사용해 사용자가 명시적으로 허용하기 전까지 기본 run도 차단한다.

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

### 14.1 GUI Workbench MVP 구현 기준

1차 GUI 구현은 `apps/TesterWorkbench`의 WPF shell로 시작한다. 완전한 도킹 프레임워크와 GUI Testcase Editor를 한 번에 구현하지 않고, 고정 split layout으로 Project Explorer, YAML Editor, Outline / Properties, Problems, Console, Execution Trace, Variables, Report 영역을 먼저 제공한다.

GUI의 테스트 가능한 핵심 로직은 `TesterWorkbench.Core`에 둔다.

- `WorkspaceScanner`: workspace 폴더에서 YAML, JSON, HTML 리소스를 탐색한다.
- `TesterEngineBridge`: Python CLI를 subprocess로 호출해 compile/run JSON을 수집하고, run result의 command event와 testcase variable snapshot을 table model로 변환한다.
- `MainWorkbenchViewModel`: workspace, 선택 파일, editor text, diagnostics, run status, report path, execution trace, variables를 관리한다.

초기 engine 연동은 CLI subprocess를 사용한다. GUI는 Python module을 직접 import하지 않으며, repository root의 `src`를 `PYTHONPATH`에 추가해 source tree 실행을 지원한다. Execution Trace와 Variables는 우선 run 종료 후 JSON snapshot을 표시한다. JSON-RPC over stdio streaming은 현재 구조를 유지한 채 `TesterEngineBridge` 뒤에 추가할 수 있는 다음 단계로 둔다.

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
