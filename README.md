# Embedded SW Tester

임베디드 SW 테스트케이스를 YAML로 작성하고, 이를 실행 가능한 resolved package로 컴파일하고 mock runtime으로 실행한 뒤 로컬 리포트를 생성하기 위한 프로토타입입니다.

현재 저장소의 구현 범위는 **Phase 8: Python DSL Compiler + Runtime Core + Report Pipeline + Adapter Framework + Serial Adapter + Tool Profile + Serial Profile Factory + Device Command Profiles**입니다. 전체 제품 설계는 C#/.NET Windows IDE, Python 실행 엔진, Trace32/CANoe/INCA/Serial 어댑터를 목표로 하지만, 이 커밋의 실행 가능한 코드는 YAML DSL 컴파일러, 순수 Python runtime, 리포트 생성, adapter framework, 테스트 가능한 Serial adapter, tool profile snapshot, profile 기반 SerialAdapter factory, 장비 의미 명령 profile, CLI에 집중되어 있습니다.

## 현재 지원 범위

- YAML 테스트 파일 로드
- 상대 경로 기반 function library import
- 축약 명령 문법 정규화
- function symbol table 생성
- unknown command 진단
- function call 반환 매핑 검증
- resolved package JSON 출력
- `set`, `call`, `assert.eq`, `assert.gt`, `assert.fail`, `log.*`, `delay` 실행
- testcase/function frame과 local variable scope
- command event와 testcase result JSON 출력
- 공통 adapter interface와 adapter registry
- adapter-category 명령의 registry 기반 dispatch
- Serial/Trace32/CANoe/INCA용 기본 mock adapter 등록
- 테스트 가능한 `SerialAdapter`, `SerialPort`, `FakeSerialPort`
- `serial.write`, `serial.read`, `serial.read.save_as` 지원
- Serial TX/RX raw evidence 파일 기록
- tool profile 기반 serial device 선언과 resolved package snapshot
- tool profile snapshot에서 `SerialAdapter`/`AdapterRegistry` 구성
- 확정 serial 대상: power supply, Mach Systems SENT-USB interface
- `sent_usb.read` 장비 의미 명령을 profile 정의 기반 serial TX/RX로 실행
- `power_supply.command`는 입력 포맷 확정 전 `pending` profile 사용 시 compile error로 차단
- `run.json`, `resolved-package.yaml`, testcase result JSON, `summary.html` 리포트 생성
- pytest 기반 회귀 테스트

## 프로젝트 구조

```text
docs/
  design/
    embedded-sw-tester-detailed-design.md
  superpowers/
    plans/
      2026-05-15-embedded-sw-tester-phase1.md
samples/
  boot-smoke.yaml
  libs/
    common-power-sequence.yaml
  tool-profiles/
    lab-serial.tools.yaml
src/
  embsw_tester/
    cli.py
    adapters/
      base.py
      mock.py
      registry.py
      serial.py
      serial_factory.py
    devices/
      command_profiles.py
    dsl/
      catalog.py
      compiler.py
      models.py
    reports/
      models.py
      writer.py
    runtime/
      expressions.py
      models.py
      runner.py
    tools/
      profile.py
tests/
  test_adapters.py
  test_cli.py
  test_compiler.py
  test_reports.py
  test_runtime.py
  test_serial_adapter.py
  test_serial_factory.py
  test_device_command_profiles.py
  test_tool_profile.py
```

## 개발 환경 준비

Python 3.9 이상이 필요합니다.

macOS/Linux:

```bash
python3 -m venv .venv
.venv/bin/python -m pip install --upgrade pip
.venv/bin/python -m pip install -e ".[dev]"
```

Windows PowerShell:

```powershell
py -3.9 -m venv .venv
.\.venv\Scripts\python -m pip install --upgrade pip
.\.venv\Scripts\python -m pip install -e ".[dev]"
```

실제 serial port를 열어야 하는 환경에서는 optional dependency를 추가로 설치합니다.

```bash
.venv/bin/python -m pip install -e ".[serial]"
```

## 테스트 실행

```bash
.venv/bin/python -m pytest
```

Windows PowerShell:

```powershell
.\.venv\Scripts\python -m pytest
```

## 샘플 YAML 컴파일

설치 후 console script를 사용합니다.

```bash
.venv/bin/embsw-tester compile samples/boot-smoke.yaml --json
```

Windows PowerShell:

```powershell
.\.venv\Scripts\embsw-tester compile samples\boot-smoke.yaml --json
```

editable install 없이 바로 실행하려면 `PYTHONPATH`를 지정할 수 있습니다.

```bash
PYTHONPATH=src .venv/bin/python -m embsw_tester.cli compile samples/boot-smoke.yaml --json
```

정상 컴파일되면 `diagnostics`가 빈 배열이고, import된 `power_on` function과 `boot_smoke` testcase가 포함된 resolved package JSON이 출력됩니다.
샘플은 `tool_profile`도 참조하므로 compile output의 `tool_profile_snapshot`에 `psu`와 `sent_usb` 장비 선언이 함께 포함됩니다.

## 샘플 YAML 실행

mock runtime으로 샘플 테스트케이스를 실행합니다.

```bash
.venv/bin/embsw-tester run samples/boot-smoke.yaml --json
```

Windows PowerShell:

```powershell
.\.venv\Scripts\embsw-tester run samples\boot-smoke.yaml --json
```

정상 실행되면 `status`가 `passed`이고, `testcase_results`에 실행된 command event와 최종 local variable snapshot이 포함됩니다. 현재 외부 툴 adapter 명령은 adapter registry를 통해 실행되며, CLI 기본값은 실제 장비를 제어하지 않는 mock adapter입니다.

## Adapter Framework

adapter-category 명령은 command catalog의 adapter metadata를 통해 adapter registry로 라우팅됩니다. 현재 기본 registry는 아래 adapter 이름에 mock adapter를 등록합니다.

- `serial`
- `trace32`
- `canoe`
- `inca`

runtime API에서 테스트용 adapter를 직접 주입할 수 있습니다.

```python
from embsw_tester.adapters import AdapterRegistry, MockAdapter
from embsw_tester.dsl.compiler import compile_file
from embsw_tester.runtime import run_package

registry = AdapterRegistry()
registry.register("serial", MockAdapter("serial"))

package = compile_file("samples/boot-smoke.yaml")
result = run_package(package, adapter_registry=registry)
```

실제 Serial, Trace32, CANoe, INCA 연동은 이 adapter contract 뒤에 붙이는 방식으로 확장합니다.

## Serial Adapter

`SerialAdapter`는 `SerialPort` 추상화를 통해 `serial.write`와 `serial.read`를 실행합니다. 현재 저장소에는 물리 COM 포트 없이 테스트 가능한 `FakeSerialPort`가 포함되어 있습니다.

```python
from embsw_tester.adapters import AdapterRegistry
from embsw_tester.adapters.serial import FakeSerialPort, SerialAdapter

registry = AdapterRegistry()
registry.register(
    "serial",
    SerialAdapter(
        {"psu": FakeSerialPort(rx_lines=["OK"])},
        evidence_root="reports/my-run",
    ),
)
```

YAML 예시:

```yaml
steps:
  - serial.write:
      port: psu
      text: "OUT 1 ON"
  - serial.read:
      port: psu
      timeout_ms: 500
      until: "OK"
      save_as: psu_response
  - assert.eq:
      left: "${psu_response}"
      right: "OK"
```

`serial.read.save_as`는 adapter 응답의 `text` 값을 현재 testcase local variable에 저장합니다. Serial adapter는 `raw-logs/serial/<run-id>/<testcase>.log` 형태의 TX/RX evidence를 기록합니다.

실제 COM 포트 사용은 `PySerialPort`가 담당하며, `pyserial` 설치 후 사용합니다. 테스트나 장비 없는 개발에서는 `FakeSerialPort`를 주입합니다.

## Tool Profile

실행 YAML은 top-level `tool_profile`로 공통 장비 설정 파일을 참조할 수 있습니다.

```yaml
tool_profile: tool-profiles/lab-serial.tools.yaml
```

현재 샘플 profile은 serial 조작 대상을 두 개로 선언합니다.

```yaml
serial:
  devices:
    psu:
      device_type: power_supply
      port: COM3
      baudrate: 9600
      command_profile: pending
      notes: "Power supply input format is not confirmed yet."
    sent_usb:
      device_type: mach_systems_sent_usb
      port: COM4
      baudrate: 115200
      command_profile: sent_usb_line
      notes: "Mach Systems SENT-USB serial interface."
command_profiles:
  sent_usb_line:
    notes: "Placeholder mapping. Replace write/read syntax after confirming the Mach Systems SENT-USB command format."
    commands:
      sent_usb.read:
        write: "READ SENT {{ channel }}"
        read:
          until: "VALUE"
```

`psu`는 power supply를 뜻하며, 입력 포맷이 아직 확정되지 않았기 때문에 `command_profile: pending`으로 둡니다. `sent_usb`는 Mach Systems의 SENT-USB interface를 뜻합니다. compiler는 이 설정을 실행 직전 `tool_profile_snapshot`으로 고정해서 report의 `resolved-package.yaml`에도 남깁니다.

profile snapshot으로 실제 serial adapter registry를 구성할 수 있습니다. CLI 기본 실행은 장비 없이 동작하도록 mock adapter를 유지하며, 실제 장비 실행 경로에서는 아래 factory를 사용합니다.

```python
from pathlib import Path

from embsw_tester.adapters import create_adapter_registry_from_tool_profile
from embsw_tester.dsl.compiler import compile_file
from embsw_tester.runtime import run_package

package = compile_file(Path("samples/boot-smoke.yaml"))
registry = create_adapter_registry_from_tool_profile(
    package.tool_profile_snapshot,
    evidence_root=Path("reports/real-serial-run"),
)
result = run_package(package, run_id="real-serial-run", adapter_registry=registry)
```

`create_adapter_registry_from_tool_profile`는 profile의 논리 장비 이름을 `SerialAdapter` port 이름으로 사용합니다. 예를 들어 YAML의 `port: psu`는 profile의 `serial.devices.psu.port: COM3`로 연결됩니다.

## Device Command Profiles

장비 의미 명령은 테스트 YAML에서 raw serial 문자열 대신 장비 목적을 드러내는 명령을 쓰고, 실제 serial TX/RX는 tool profile의 `command_profiles`에서 결정하는 방식입니다.

```yaml
steps:
  - sent_usb.read:
      device: sent_usb
      channel: 1
      timeout_ms: 500
      save_as: sent_value
```

위 명령은 `sent_usb` 장비의 `command_profile`을 찾아 `sent_usb.read` mapping을 실행합니다. 샘플 profile에서는 placeholder로 `READ SENT {{ channel }}`을 전송하고, 응답에 `VALUE`가 포함될 때 read 성공으로 처리합니다. Mach Systems SENT-USB의 실제 입력 포맷이 확정되면 `samples/tool-profiles/lab-serial.tools.yaml`의 mapping만 교체하면 됩니다.

Power supply는 아직 입력 포맷이 미확정이므로 샘플 profile에서 `command_profile: pending`입니다. 이 상태에서 `power_supply.command`를 쓰면 compiler가 `PENDING_COMMAND_PROFILE` 진단으로 실행을 차단합니다.

## 리포트 생성

`run` 명령에 `--reports-root`를 지정하면 실행 결과를 파일로 저장합니다.

```bash
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id boot-smoke-local --reports-root reports --json
```

Windows PowerShell:

```powershell
.\.venv\Scripts\embsw-tester run samples\boot-smoke.yaml --run-id boot-smoke-local --reports-root reports --json
```

출력 위치:

```text
reports/boot-smoke-local/
  summary.html
  run.json
  resolved-package.yaml
  testcase-results/
    boot_smoke.json
  attachments/
  raw-logs/
```

`run.json`은 기계용 실행 원본이고, `summary.html`은 사람이 빠르게 확인하기 위한 요약입니다. `attachments/`와 `raw-logs/`는 이후 실제 외부 툴 adapter가 생성할 증적을 저장하기 위해 미리 만들어집니다.

## YAML 예시

```yaml
imports:
  - libs/common-power-sequence.yaml

testcases:
  - name: boot_smoke
    steps:
      - call:
          function: power_on
          args:
            channel: 1
          out:
            voltage_ok: power_ready
      - assert.eq:
          left: "${power_ready}"
          right: true
```

명령은 `cmd:` 키를 쓰지 않고 명령 이름 자체를 YAML key로 사용합니다. 컴파일러는 이를 내부적으로 `type`과 `args`를 가진 명령 객체로 정규화합니다.

## 설계 문서

- 상세 설계서: `docs/design/embedded-sw-tester-detailed-design.md`
- Phase 1 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase1.md`
- Phase 2 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase2-runtime.md`
- Phase 3 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase3-report-pipeline.md`
- Phase 4 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase4-adapter-framework.md`
- Phase 5 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase5-serial-adapter.md`
- Phase 6 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase6-tool-profile-serial-devices.md`
- Phase 7 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase7-serial-profile-factory.md`
- Phase 8 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase8-device-command-profiles.md`

## 다음 구현 단계

다음 단계는 실제 장비 연결을 준비하는 serial 실행 경로 정교화입니다.

- power supply 입력 포맷 확정 후 command profile 구현
- Mach Systems SENT-USB 실제 line protocol로 sample mapping 교체
- Windows 장비 연결 smoke test
- timeout/응답 매칭 정책 확장
