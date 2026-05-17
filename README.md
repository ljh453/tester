# Embedded SW Tester

임베디드 SW 테스트케이스를 YAML로 작성하고, 이를 실행 가능한 resolved package로 컴파일하고 mock runtime으로 실행한 뒤 로컬 리포트를 생성하기 위한 프로토타입입니다.

현재 저장소의 구현 범위는 **Phase 24: Python DSL Compiler + Runtime Core + Report Pipeline + Adapter Framework + Serial/Trace32/CANoe/INCA Adapter Contracts + Tool Profile + Device Command Profiles + Mach/VuPower Serial Protocols + GUI Workbench MVP**입니다. 전체 제품 설계는 C#/.NET Windows IDE, Python 실행 엔진, Trace32/CANoe/INCA/Serial 어댑터를 목표로 하지만, 이 커밋의 실행 가능한 코드는 YAML DSL 컴파일러, 순수 Python runtime, 리포트 생성, adapter framework, 테스트 가능한 Serial/Trace32/CANoe/INCA adapter contract, Trace32 RCL wrapper와 UDP socket transport, Trace32 tool profile factory, INCA 32bit helper RPC schema와 JSON line process transport, INCA tool profile factory, profile-backed CLI run mode, tool profile snapshot, 장비 의미 명령 profile, Mach Systems SENT Gateway binary receive/transmit/control/slow-frame protocol, VuPower K USB-to-Serial power supply protocol, CLI, WPF GUI workbench skeleton, GUI run trace/variables table에 집중되어 있습니다.

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
- 테스트 가능한 `Trace32Adapter`, `RclTrace32Transport`, `UdpTrace32Transport`, `FakeTrace32Transport`
- 테스트 가능한 `CanoeAdapter`
- 테스트 가능한 `IncaAdapter`
- INCA 32bit helper request/response schema와 JSON line process transport
- tool profile snapshot에서 `IncaAdapter` 구성
- `serial.write`, `serial.read`, `serial.write_bytes`, `serial.read_bytes`, `serial.read.save_as` 지원
- `serial.read.match` regex 기반 응답 판정
- `trace32.command` 지원
- Trace32 RCL 기본 실행 및 UDP command fallback
- tool profile snapshot에서 `Trace32Adapter` 구성
- `canoe.measurement.start`, `canoe.measurement.stop` 지원
- `canoe.sysvar.set`, `canoe.sysvar.read`, `canoe.signal.read` 지원
- `inca.measure.read`, `inca.calibration.set` 지원
- `inca.recording.start`, `inca.recording.stop` 지원
- Serial TX/RX raw evidence 파일 기록
- tool profile 기반 serial device 선언과 resolved package snapshot
- tool profile snapshot에서 `SerialAdapter`/`AdapterRegistry` 구성
- CLI `run --use-tool-profile-adapters` profile-backed 실행 모드
- 확정 serial 대상: power supply, Mach Systems SENT-USB interface
- `sent_usb.read` 장비 의미 명령을 profile 정의 기반 serial TX/RX 또는 Mach Systems SENT Gateway binary protocol로 실행
- `sent_usb.command` 장비 의미 명령으로 Mach SENT channel config/start/stop/fast/slow transmit 실행
- `power_supply.command` 장비 의미 명령을 VuPower K USB-to-Serial protocol로 실행
- 장비 profile의 `read.match` regex로 응답 pass/fail 판정
- 장비 profile의 `read.extract` regex로 raw 응답에서 저장 값을 추출
- Mach Systems SENT Gateway frame encode/decode, SENT channel 1/2 fast frame 수신, fast/slow frame 송신, ACK 파싱
- VuPower K `APPL`, `OUTP:STAT`, `MEAS:*?`, `*IDN?`, `*RST`, `SYST:ERR?` 초기 명령 지원
- 미구현 장비가 `pending` profile을 사용할 경우 compile error로 차단
- `run.json`, `resolved-package.yaml`, testcase result JSON, `summary.html` 리포트 생성
- pytest 기반 회귀 테스트
- `.NET` 기반 GUI Workbench MVP skeleton
- GUI core의 workspace scan, engine subprocess bridge, workbench ViewModel 테스트 harness
- GUI run JSON 기반 Execution Trace / Variables table 표시

## 프로젝트 구조

```text
docs/
  design/
    embedded-sw-tester-detailed-design.md
  superpowers/
    plans/
      2026-05-15-embedded-sw-tester-phase1.md
apps/
  TesterWorkbench/
    TesterWorkbench/
      App.xaml
      MainWindow.xaml
    TesterWorkbench.Core/
      Engine/
      ViewModels/
      Workspace/
    TesterWorkbench.Tests/
      Program.cs
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
      canoe.py
      inca.py
      inca_bridge.py
      inca_factory.py
      mock.py
      registry.py
      serial.py
      serial_factory.py
      trace32.py
      trace32_factory.py
    devices/
      command_profiles.py
      mach_sent_gateway.py
      vupower_k.py
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
  test_catalog.py
  test_cli.py
  test_compiler.py
  test_reports.py
  test_inca_adapter.py
  test_inca_bridge.py
  test_inca_bridge_transport.py
  test_inca_factory.py
  test_runtime.py
  test_serial_adapter.py
  test_serial_factory.py
  test_trace32_factory.py
  test_device_command_profiles.py
  test_mach_sent_gateway.py
  test_vupower_k.py
  test_tool_profile.py
```

## 개발 환경 준비

Python 3.9 이상이 필요합니다.

GUI workbench를 빌드하려면 .NET 8 LTS SDK 이상이 필요합니다. 현재 GUI project는 `net8.0` core/test project와 `net8.0-windows` WPF shell로 구성되어 있습니다.

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

GUI core test harness:

```bash
DOTNET_CLI_HOME=$PWD/.dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  dotnet run --project apps/TesterWorkbench/TesterWorkbench.Tests/TesterWorkbench.Tests.csproj
```

GUI WPF shell build:

```bash
DOTNET_CLI_HOME=$PWD/.dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  dotnet build apps/TesterWorkbench/TesterWorkbench/TesterWorkbench.csproj
```

Windows에서 GUI 실행:

```powershell
$env:DOTNET_CLI_HOME = "$PWD\.dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
dotnet run --project apps\TesterWorkbench\TesterWorkbench\TesterWorkbench.csproj
```

GUI는 Python 엔진을 subprocess로 호출합니다. 소스 트리에서 실행하면 workbench가 repository root의 `src`를 `PYTHONPATH`에 추가합니다. Python 실행 파일을 지정해야 하면 `EMBSW_TESTER_PYTHON` 환경변수를 설정합니다.

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

실제 tool profile 기반 adapter를 사용하려면 명시적으로 opt-in 합니다.

```bash
.venv/bin/embsw-tester run samples/boot-smoke.yaml --use-tool-profile-adapters --run-id real-tools-run --reports-root reports --json
```

이 모드에서는 resolved `tool_profile_snapshot`에서 Serial, Trace32, INCA adapter를 구성합니다. `--reports-root`와 `--run-id`가 함께 있으면 adapter raw evidence root도 같은 report run directory를 사용합니다.

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

## Trace32 Adapter

`Trace32Adapter`는 Lauterbach Trace32 연동을 위한 command transport contract입니다. 기본 경로는 RCL이고, RCL transport가 실패하면 UDP command transport로 fallback합니다.

YAML 예시:

```yaml
steps:
  - trace32.command:
      command: "STATE()"
      timeout_ms: 2500
      save_as: trace32_response
```

테스트나 장비 없는 개발에서는 transport를 직접 주입합니다.

```python
from embsw_tester.adapters import AdapterRegistry
from embsw_tester.adapters.trace32 import RclTrace32Transport, Trace32Adapter, UdpTrace32Transport
from embsw_tester.dsl.compiler import compile_file
from embsw_tester.runtime import run_package

rcl_client = make_trace32_rcl_client()
rcl = RclTrace32Transport(client=rcl_client)
udp = UdpTrace32Transport(host="127.0.0.1", port=20000)

registry = AdapterRegistry()
registry.register("trace32", Trace32Adapter(rcl_transport=rcl, udp_transport=udp))

package = compile_file("tests/trace32.yaml")
result = run_package(package, adapter_registry=registry)
```

`transport: udp`를 명시하면 UDP만 사용합니다. 기본값은 RCL 우선이며, `fallback: false`를 지정하면 RCL 실패 시 UDP fallback을 시도하지 않습니다. `RclTrace32Transport`는 주입된 RCL client의 `cmd(command)` 메서드를 호출하는 wrapper입니다. 사용하는 RCL 패키지의 메서드명이 다르면 `command_method`로 바꿀 수 있습니다. `UdpTrace32Transport`는 stdlib UDP socket으로 command와 terminator를 전송하고 한 번의 응답 datagram을 읽습니다.

tool profile에서 Trace32 adapter를 구성할 수도 있습니다.

```yaml
trace32:
  rcl:
    enabled: true
    client_factory: lab_trace32:create_client
    command_method: cmd
    client_args:
      node: TRACE32-A
  udp:
    enabled: true
    host: 127.0.0.1
    port: 20000
    terminator: "\n"
    response_bytes: 4096
```

`client_factory`는 `module:attribute` 형식의 Python callable입니다. factory는 `client_args`를 keyword argument로 받아 RCL client 객체를 반환해야 합니다. 테스트나 IDE 통합 경로에서는 import path 대신 `create_adapter_registry_from_tool_profile(..., trace32_rcl_client_factory=...)`로 factory를 직접 주입할 수 있습니다.

## CANoe/CANalyzer Adapter

`CanoeAdapter`는 Windows 전용 Vector COM 연동 전에 DSL과 runtime 계약을 고정하기 위한 in-memory adapter입니다. 현재 지원 명령은 measurement start/stop, system variable set/read, signal read입니다.

YAML 예시:

```yaml
steps:
  - canoe.measurement.start: {}
  - canoe.sysvar.set:
      namespace: Vehicle
      name: Ignition
      value: true
  - canoe.sysvar.read:
      namespace: Vehicle
      name: Ignition
      save_as: ignition_state
  - canoe.signal.read:
      signal: EngineSpeed
      save_as: rpm
  - canoe.measurement.stop: {}
```

테스트나 장비 없는 개발에서는 adapter를 직접 주입합니다.

```python
from embsw_tester.adapters import AdapterRegistry, CanoeAdapter
from embsw_tester.dsl.compiler import compile_file
from embsw_tester.runtime import run_package

registry = AdapterRegistry()
registry.register("canoe", CanoeAdapter(signals={"EngineSpeed": 850}))

package = compile_file("tests/canoe.yaml")
result = run_package(package, adapter_registry=registry)
```

실제 CANoe/CANalyzer COM API 호출은 이후 같은 `execute(command_type, args, context)` 경계 뒤에 붙입니다.

## INCA Adapter

`IncaAdapter`는 Windows 32bit Python COM helper를 붙일 수 있는 adapter입니다. 장비 없는 개발/테스트에서는 in-memory adapter로 동작하고, 실제 INCA 실행에서는 helper 프로세스가 `Inca.Inca` COM ProgID를 통해 INCA Tool-API를 호출합니다. 현재 지원 명령은 measurement read, calibration set, recording start/stop입니다.

YAML 예시:

```yaml
steps:
  - inca.recording.start:
      name: boot
      output_dir: C:/reports
      file_format: MDF
  - inca.measure.read:
      variable: EngineSpeed
      device: ETKC
      acquisition_rate: 10ms
      save_as: rpm
  - inca.calibration.set:
      parameter: IdleSpeedTarget
      value: 850
      value_kind: phys
  - inca.recording.stop: {}
```

테스트나 장비 없는 개발에서는 adapter를 직접 주입합니다.

```python
from embsw_tester.adapters import AdapterRegistry, IncaAdapter
from embsw_tester.dsl.compiler import compile_file
from embsw_tester.runtime import run_package

registry = AdapterRegistry()
registry.register("inca", IncaAdapter(measurements={"EngineSpeed": 900}))

package = compile_file("tests/inca.yaml")
result = run_package(package, adapter_registry=registry)
```

`IncaBridgeRequest`와 `IncaBridgeResponse`는 64bit 실행 엔진과 32bit Python INCA helper 프로세스 사이의 RPC payload schema입니다. `embsw_tester.adapters.inca_com_helper`는 INCA 7.2 Tool-API wrapper/registration 정보(`Inca.Inca`, `GetOpenedExperiment`, `GetMeasureValue*`, `GetCalibrationValue*`, `StartRecording`, `StopRecording`)를 기준으로 구현된 helper입니다.

Windows 실제 실행 경로에서는 JSON line stdio transport를 `IncaAdapter`에 주입합니다.

```python
from embsw_tester.adapters import (
    AdapterRegistry,
    IncaAdapter,
    create_inca_bridge_process_transport,
)
from embsw_tester.dsl.compiler import compile_file
from embsw_tester.runtime import run_package

transport = create_inca_bridge_process_transport(
    [
        r"C:\Python32\python.exe",
        "-m",
        "embsw_tester.adapters.inca_com_helper",
    ]
)

registry = AdapterRegistry()
registry.register("inca", IncaAdapter(bridge_transport=transport))

package = compile_file("tests/inca.yaml")
result = run_package(package, adapter_registry=registry)
```

helper 프로세스는 stdin에서 `IncaBridgeRequest` JSON 한 줄을 읽고, stdout으로 같은 `request_id`를 가진 `IncaBridgeResponse` JSON 한 줄을 반환해야 합니다. `timeout_ms`는 request의 top-level field로 전달되며, command args에서는 제거됩니다.

tool profile에서 INCA helper process command를 선언할 수도 있습니다.

```yaml
inca:
  helper:
    enabled: true
    command:
      - C:/Python32/python.exe
      - -m
      - embsw_tester.adapters.inca_com_helper
```

profile snapshot 기반 registry factory는 `inca.helper.command`가 있으면 `IncaAdapter`를 helper-backed adapter로 등록합니다.

실제 helper 실행 PC에는 INCA COM registration과 32bit Python용 `pywin32`가 필요합니다. helper는 stdin에서 `IncaBridgeRequest` JSON 한 줄을 읽고 stdout으로 `IncaBridgeResponse` JSON 한 줄을 반환하므로, 실행 엔진의 64bit/32bit 차이는 이 프로세스 경계 밖으로 새지 않습니다.

```python
from pathlib import Path

from embsw_tester.adapters import create_adapter_registry_from_tool_profile
from embsw_tester.dsl.compiler import compile_file
from embsw_tester.runtime import run_package

package = compile_file(Path("samples/boot-smoke.yaml"))
registry = create_adapter_registry_from_tool_profile(
    package.tool_profile_snapshot,
    evidence_root=Path("reports/real-tools-run"),
)
result = run_package(package, run_id="real-tools-run", adapter_registry=registry)
```

## Serial Adapter

`SerialAdapter`는 `SerialPort` 추상화를 통해 `serial.write`, `serial.read`, `serial.write_bytes`, `serial.read_bytes`를 실행합니다. 현재 저장소에는 물리 COM 포트 없이 테스트 가능한 `FakeSerialPort`가 포함되어 있습니다.

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

Binary protocol은 JSON과 report에 bytes를 직접 싣지 않고 대문자 hex 문자열로 남깁니다.

```yaml
steps:
  - serial.read_bytes:
      port: sent_usb
      count: 5
      timeout_ms: 500
      save_as: raw_gateway_frame
```

`serial.write_bytes`는 `payload_hex`를 받아 `TX_HEX` evidence를 남기고, `serial.read_bytes`는 지정한 byte 수를 읽어 `RX_HEX` evidence와 `data_hex` 값을 남깁니다.

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
      command_profile: vupower_k_usb
      notes: "VuPower K Series USB-to-Serial SCPI-style protocol."
    sent_usb:
      device_type: mach_systems_sent_usb
      port: COM4
      baudrate: 115200
      command_profile: mach_sent_gateway
      notes: "Mach Systems SENT-USB interface using the SENT Gateway binary protocol."
command_profiles:
  vupower_k_usb:
    notes: "VuPower K USB manual Ver3.2. Commands are line-based and terminated by LF."
    commands:
      power_supply.command:
        protocol: vupower_k_usb
        channel: P1
  mach_sent_gateway:
    notes: "Mach Systems SENT Gateway protocol. Supports fast frame receive and fast/slow TX control commands."
    commands:
      sent_usb.read:
        protocol: mach_sent_gateway
      sent_usb.command:
        protocol: mach_sent_gateway
        read_ack: true
```

`psu`는 VuPower K Series power supply를 뜻하며, [VuPower K USB Manual Korea Ver3.2](http://www.vupower.com/download/K_USB_Manual_Korea_Ver3.2.pdf)의 USB-to-Serial SCPI 스타일 명령을 사용합니다. `sent_usb`는 Mach Systems의 SENT-USB interface를 뜻하고, [Mach Systems SENT Gateway Communication Protocol Specification](https://www.machsystems.cz/support/SENT%20Gateway-Communication%20Protocol%20Specification_latest.pdf)의 `STX LEN ID DATA CHKSUM ETX` frame을 기준으로 수신 프레임을 파싱합니다. compiler는 이 설정을 실행 직전 `tool_profile_snapshot`으로 고정해서 report의 `resolved-package.yaml`에도 남깁니다.

profile snapshot으로 실제 serial/Trace32 adapter registry를 구성할 수 있습니다. CLI 기본 실행은 장비 없이 동작하도록 mock adapter를 유지하며, 실제 장비 실행 경로에서는 아래 factory를 사용합니다.

```python
from pathlib import Path

from embsw_tester.adapters import create_adapter_registry_from_tool_profile
from embsw_tester.dsl.compiler import compile_file
from embsw_tester.runtime import run_package

package = compile_file(Path("samples/boot-smoke.yaml"))
registry = create_adapter_registry_from_tool_profile(
    package.tool_profile_snapshot,
    evidence_root=Path("reports/real-serial-run"),
    trace32_rcl_client_factory=make_trace32_rcl_client_from_config,
)
result = run_package(package, run_id="real-serial-run", adapter_registry=registry)
```

`create_adapter_registry_from_tool_profile`는 profile의 논리 장비 이름을 `SerialAdapter` port 이름으로 사용합니다. 예를 들어 YAML의 `port: psu`는 profile의 `serial.devices.psu.port: COM3`로 연결됩니다. profile에 `trace32` 섹션이 있으면 `Trace32Adapter`도 함께 등록합니다.

CLI에서는 같은 경로를 `--use-tool-profile-adapters`로 사용할 수 있습니다. 이 옵션을 주지 않으면 `tool_profile`이 있어도 mock adapter registry를 사용합니다.

## Device Command Profiles

장비 의미 명령은 테스트 YAML에서 raw serial 문자열 대신 장비 목적을 드러내는 명령을 쓰고, 실제 serial TX/RX는 tool profile의 `command_profiles`에서 결정하는 방식입니다.

```yaml
steps:
  - power_supply.command:
      device: psu
      action: apply
      channel: 1
      voltage: 12
      current: 1.234
  - power_supply.command:
      device: psu
      action: output
      channel: 1
      state: on
  - power_supply.command:
      device: psu
      action: measure_voltage
      channel: 1
      average: true
      save_as: measured_voltage
  - sent_usb.command:
      device: sent_usb
      action: config
      channel: 1
      direction: tx
      data_nibble_count: 6
      unit_time_us: 3.0
  - sent_usb.command:
      device: sent_usb
      action: start
      channel: 1
  - sent_usb.command:
      device: sent_usb
      action: transmit_fast
      channel: 1
      status: 3
      data_nibbles: [1, 2, 3, 4, 5, 6]
      crc: 10
      crc_calculated: 11
  - sent_usb.command:
      device: sent_usb
      action: transmit_slow
      channel: 1
      slow_message_id: 18
      data: 13398
      slow_frame_type: enhanced_serial
      enhanced_format: true
      crc_received: 37
      crc_calculated: 44
  - sent_usb.read:
      device: sent_usb
      channel: 1
      timeout_ms: 500
      save_as: sent_value
```

`power_supply.command`는 `protocol: vupower_k_usb`일 때 VuPower K manual의 line protocol을 사용합니다. 명령은 LF로 종료되며 현재 action은 `apply`, `set_voltage`, `set_current`, `output`, `read_output`, `measure_voltage`, `measure_current`, `read_voltage`, `read_current`, `mode`, `identify`, `reset`, `system_error`, `raw`를 지원합니다. 예를 들어 `apply`는 `APPL P1,12.000,1.234`, `output`은 `OUTP:STAT P1,ON`, 평균 전압 측정은 `MEAS:VOLTA? P1`로 변환됩니다. 측정/조회 action은 serial read 응답을 float, bool, mode 문자열 등으로 파싱해 `save_as`에 저장합니다.

`sent_usb.command`는 `protocol: mach_sent_gateway`일 때 Mach Systems SENT Gateway binary frame을 `serial.write_bytes`로 전송합니다. 현재 action은 `config`, `start`, `stop`, `transmit_fast`, `transmit_slow`를 지원합니다. channel 1/2 message id는 config `2/12`, start `21/31`, stop `22/32`, fast transmit `41/51`, slow transmit `42/52`입니다. 기본값으로 ACK frame을 읽어 one-byte status `1`을 OK로 처리하고, `0`이면 실패로 보고합니다. SENT channel configuration은 7-byte payload로 직렬화하며, fast transmit payload는 status nibble, data nibble count, data nibbles, CRC nibble layout을 사용합니다. 단일 slow transmit payload는 slow message id, 16-bit data little-endian, 6-bit CRC received, slow/enhanced type bit, enhanced format bit, 6-bit calculated CRC layout을 사용합니다. Slow message multiplex buffer message id `43/53`은 아직 별도 구현 대상입니다.

`sent_usb.read`는 `sent_usb` 장비의 `command_profile`을 찾아 `sent_usb.read` mapping을 실행합니다. `protocol: mach_sent_gateway`이면 Mach Systems SENT Gateway binary frame을 `serial.read_bytes`로 읽고, channel 1은 message id `100`, channel 2는 message id `200`인 SENT fast frame으로 해석합니다. 저장 값은 status nibble, data nibble count, data nibbles, received CRC, calculated CRC를 포함한 dict입니다.

기존 line 기반 profile도 계속 사용할 수 있습니다. profile에 `write`/`read` mapping을 두면 `write` template을 전송하고 `read.until` substring, `read.match` regex, 선택적 `read.extract` regex로 응답을 판정합니다. named group `value`가 있으면 그 값을 저장하고, named group이 없으면 첫 번째 capture group, capture group도 없으면 전체 match를 저장합니다. raw serial evidence와 nested serial output은 그대로 남습니다.

미구현 장비는 `command_profile: pending`으로 둘 수 있습니다. 이 상태에서 해당 device command를 쓰면 compiler가 `PENDING_COMMAND_PROFILE` 진단으로 실행을 차단합니다.

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
- Phase 9 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase9-device-response-extraction.md`
- Phase 10 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase10-serial-response-matching.md`
- Phase 11 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase11-canoe-adapter-contract.md`
- Phase 12 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase12-inca-adapter-contract.md`
- Phase 13 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase13-trace32-rcl-udp-fallback.md`
- Phase 14 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase14-trace32-transports.md`
- Phase 15 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase15-trace32-profile-factory.md`
- Phase 16 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase16-inca-bridge-transport.md`
- Phase 17 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase17-inca-profile-factory.md`
- Phase 18 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase18-profile-backed-run-cli.md`
- Phase 19 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase19-mach-sent-gateway-protocol.md`
- Phase 20 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase20-vupower-k-power-supply.md`
- Phase 21 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase21-mach-sent-commands.md`
- Phase 22 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase22-mach-sent-slow-frame.md`
- Phase 23 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase23-gui-workbench-mvp.md`
- Phase 24 구현 계획: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase24-gui-trace-variables.md`

## 다음 구현 단계

다음 단계는 Windows 장비 smoke 경로와 실제 장비별 추가 명령을 좁혀가는 쪽이 좋습니다.

- GUI Report HTML viewer 연결
- GUI 실행 이벤트 선택 시 resolved input/output detail 표시
- Mach Systems SENT Gateway slow message multiplex buffer 명령 추가
