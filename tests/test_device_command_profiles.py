from pathlib import Path

from embsw_tester.adapters.serial import FakeSerialPort
from embsw_tester.adapters.serial_factory import create_adapter_registry_from_tool_profile
from embsw_tester.devices.mach_sent_gateway import encode_gateway_frame
from embsw_tester.dsl.compiler import compile_file
from embsw_tester.runtime import run_package


def test_runtime_executes_sent_usb_command_profile_over_serial(tmp_path: Path):
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
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
""".strip(),
        encoding="utf-8",
    )
    test_file = tmp_path / "sent.yaml"
    test_file.write_text(
        """
tool_profile: lab.tools.yaml
testcases:
  - name: sent_case
    steps:
      - sent_usb.read:
          device: sent_usb
          channel: 1
          timeout_ms: 25
          save_as: sent_value
      - assert.eq:
          left: "${sent_value}"
          right: "VALUE:123"
""".strip(),
        encoding="utf-8",
    )
    package = compile_file(test_file)
    assert package.diagnostics == []

    created_ports = {}

    def port_factory(settings):
        port = FakeSerialPort(rx_lines=["VALUE:123"])
        created_ports[settings.logical_name] = port
        return port

    registry = create_adapter_registry_from_tool_profile(
        package.tool_profile_snapshot,
        evidence_root=tmp_path / "reports" / "sent",
        serial_port_factory=port_factory,
    )

    result = run_package(
        package,
        run_id="sent-run",
        adapter_registry=registry,
    )

    testcase = result.testcase_results[0]
    sent_event = testcase.events[0]
    assert result.status == "passed"
    assert created_ports["sent_usb"].tx_lines == ["READ SENT 1"]
    assert testcase.variables["sent_value"] == "VALUE:123"
    assert sent_event.command_type == "sent_usb.read"
    assert sent_event.outputs["value"] == "VALUE:123"
    assert sent_event.outputs["serial"][0]["command_type"] == "serial.write"
    assert sent_event.outputs["serial"][1]["command_type"] == "serial.read"


def test_runtime_extracts_sent_usb_response_value_from_profile(tmp_path: Path):
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
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
          extract: 'VALUE:(?P<value>\\d+)'
""".strip(),
        encoding="utf-8",
    )
    test_file = tmp_path / "sent-extract.yaml"
    test_file.write_text(
        """
tool_profile: lab.tools.yaml
testcases:
  - name: sent_extract_case
    steps:
      - sent_usb.read:
          device: sent_usb
          channel: 1
          save_as: sent_value
      - assert.eq:
          left: "${sent_value}"
          right: "123"
""".strip(),
        encoding="utf-8",
    )
    package = compile_file(test_file)
    assert package.diagnostics == []
    registry = create_adapter_registry_from_tool_profile(
        package.tool_profile_snapshot,
        evidence_root=tmp_path / "reports" / "sent-extract",
        serial_port_factory=lambda settings: FakeSerialPort(rx_lines=["VALUE:123"]),
    )

    result = run_package(
        package,
        run_id="sent-extract-run",
        adapter_registry=registry,
    )

    testcase = result.testcase_results[0]
    sent_event = testcase.events[0]
    assert result.status == "passed"
    assert testcase.variables["sent_value"] == "123"
    assert sent_event.outputs["value"] == "123"
    assert sent_event.outputs["serial"][1]["outputs"]["values"]["text"] == "VALUE:123"


def test_runtime_applies_device_profile_response_match(tmp_path: Path):
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
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
          match: 'VALUE:{{ channel }}:\\d+'
          extract: 'VALUE:{{ channel }}:(?P<value>\\d+)'
""".strip(),
        encoding="utf-8",
    )
    test_file = tmp_path / "sent-match.yaml"
    test_file.write_text(
        """
tool_profile: lab.tools.yaml
testcases:
  - name: sent_match_case
    steps:
      - sent_usb.read:
          device: sent_usb
          channel: 1
          save_as: sent_value
      - assert.eq:
          left: "${sent_value}"
          right: "123"
""".strip(),
        encoding="utf-8",
    )
    package = compile_file(test_file)
    assert package.diagnostics == []
    registry = create_adapter_registry_from_tool_profile(
        package.tool_profile_snapshot,
        evidence_root=tmp_path / "reports" / "sent-match",
        serial_port_factory=lambda settings: FakeSerialPort(rx_lines=["VALUE:1:123"]),
    )

    result = run_package(
        package,
        run_id="sent-match-run",
        adapter_registry=registry,
    )

    sent_event = result.testcase_results[0].events[0]
    serial_read = sent_event.outputs["serial"][1]
    assert result.status == "passed"
    assert result.testcase_results[0].variables["sent_value"] == "123"
    assert serial_read["resolved_inputs"]["match"] == r"VALUE:1:\d+"
    assert serial_read["outputs"]["values"]["match"] == r"VALUE:1:\d+"


def test_runtime_reads_mach_sent_gateway_fast_frame(tmp_path: Path):
    frame = encode_gateway_frame(100, bytes.fromhex("63214365BA"))
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
serial:
  devices:
    sent_usb:
      device_type: mach_systems_sent_usb
      port: COM4
      baudrate: 115200
      command_profile: mach_sent_gateway
command_profiles:
  mach_sent_gateway:
    commands:
      sent_usb.read:
        protocol: mach_sent_gateway
""".strip(),
        encoding="utf-8",
    )
    test_file = tmp_path / "sent-gateway.yaml"
    test_file.write_text(
        """
tool_profile: lab.tools.yaml
testcases:
  - name: sent_gateway_case
    steps:
      - sent_usb.read:
          device: sent_usb
          channel: 1
          timeout_ms: 25
          save_as: sent_frame
""".strip(),
        encoding="utf-8",
    )
    package = compile_file(test_file)
    assert package.diagnostics == []
    registry = create_adapter_registry_from_tool_profile(
        package.tool_profile_snapshot,
        evidence_root=tmp_path / "reports" / "sent-gateway",
        serial_port_factory=lambda settings: FakeSerialPort(rx_lines=[], rx_bytes=[frame]),
    )

    result = run_package(
        package,
        run_id="sent-gateway-run",
        adapter_registry=registry,
    )

    testcase = result.testcase_results[0]
    sent_event = testcase.events[0]
    assert result.status == "passed"
    assert testcase.variables["sent_frame"]["data_nibbles"] == [1, 2, 3, 4, 5, 6]
    assert testcase.variables["sent_frame"]["crc"] == 10
    assert sent_event.outputs["protocol"] == "mach_sent_gateway"
    assert sent_event.outputs["channel"] == 1
    assert sent_event.outputs["frame"]["raw_hex"].startswith("020664")
    assert sent_event.outputs["serial"][0]["command_type"] == "serial.read_bytes"


def test_runtime_executes_vupower_apply_and_output_commands(tmp_path: Path):
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
serial:
  devices:
    psu:
      device_type: power_supply
      port: COM3
      baudrate: 9600
      command_profile: vupower_k_usb
command_profiles:
  vupower_k_usb:
    commands:
      power_supply.command:
        protocol: vupower_k_usb
""".strip(),
        encoding="utf-8",
    )
    test_file = tmp_path / "psu-vupower.yaml"
    test_file.write_text(
        """
tool_profile: lab.tools.yaml
testcases:
  - name: psu_vupower_case
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
          channel: P1
          state: on
""".strip(),
        encoding="utf-8",
    )
    package = compile_file(test_file)
    assert package.diagnostics == []
    created_ports = {}

    def port_factory(settings):
        port = FakeSerialPort(rx_lines=[])
        created_ports[settings.logical_name] = port
        return port

    registry = create_adapter_registry_from_tool_profile(
        package.tool_profile_snapshot,
        evidence_root=tmp_path / "reports" / "psu-vupower",
        serial_port_factory=port_factory,
    )

    result = run_package(package, run_id="psu-vupower-run", adapter_registry=registry)

    testcase = result.testcase_results[0]
    assert result.status == "passed"
    assert created_ports["psu"].tx_lines == ["APPL P1,12.000,1.234", "OUTP:STAT P1,ON"]
    assert testcase.events[0].outputs["protocol"] == "vupower_k_usb"
    assert testcase.events[0].outputs["command"] == "APPL P1,12.000,1.234"
    assert testcase.events[1].outputs["command"] == "OUTP:STAT P1,ON"


def test_runtime_reads_vupower_measurement_response(tmp_path: Path):
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
serial:
  devices:
    psu:
      device_type: power_supply
      port: COM3
      baudrate: 9600
      command_profile: vupower_k_usb
command_profiles:
  vupower_k_usb:
    commands:
      power_supply.command:
        protocol: vupower_k_usb
""".strip(),
        encoding="utf-8",
    )
    test_file = tmp_path / "psu-measure.yaml"
    test_file.write_text(
        """
tool_profile: lab.tools.yaml
testcases:
  - name: psu_measure_case
    steps:
      - power_supply.command:
          device: psu
          action: measure_voltage
          channel: 1
          average: true
          timeout_ms: 25
          save_as: measured_voltage
      - assert.gt:
          left: "${measured_voltage}"
          right: 12
""".strip(),
        encoding="utf-8",
    )
    package = compile_file(test_file)
    assert package.diagnostics == []
    registry = create_adapter_registry_from_tool_profile(
        package.tool_profile_snapshot,
        evidence_root=tmp_path / "reports" / "psu-measure",
        serial_port_factory=lambda settings: FakeSerialPort(rx_lines=["12.345"]),
    )

    result = run_package(package, run_id="psu-measure-run", adapter_registry=registry)

    testcase = result.testcase_results[0]
    psu_event = testcase.events[0]
    assert result.status == "passed"
    assert testcase.variables["measured_voltage"] == 12.345
    assert psu_event.outputs["command"] == "MEAS:VOLTA? P1"
    assert psu_event.outputs["value"] == 12.345
    assert psu_event.outputs["serial"][0]["command_type"] == "serial.write"
    assert psu_event.outputs["serial"][1]["command_type"] == "serial.read"


def test_compile_reports_pending_power_supply_command_profile(tmp_path: Path):
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
serial:
  devices:
    psu:
      device_type: power_supply
      port: COM3
      baudrate: 9600
      command_profile: pending
""".strip(),
        encoding="utf-8",
    )
    test_file = tmp_path / "psu.yaml"
    test_file.write_text(
        """
tool_profile: lab.tools.yaml
testcases:
  - name: psu_case
    steps:
      - power_supply.command:
          device: psu
          text: "OUT 1 ON"
""".strip(),
        encoding="utf-8",
    )

    package = compile_file(test_file)

    assert [diagnostic.code for diagnostic in package.diagnostics] == ["PENDING_COMMAND_PROFILE"]
    assert "psu" in package.diagnostics[0].message


def test_compile_reports_invalid_response_extractor(tmp_path: Path):
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
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
          extract: "VALUE:("
""".strip(),
        encoding="utf-8",
    )
    test_file = tmp_path / "sent-invalid-extract.yaml"
    test_file.write_text(
        """
tool_profile: lab.tools.yaml
testcases:
  - name: sent_invalid_extract_case
    steps:
      - sent_usb.read:
          device: sent_usb
          channel: 1
          save_as: sent_value
""".strip(),
        encoding="utf-8",
    )

    package = compile_file(test_file)

    assert [diagnostic.code for diagnostic in package.diagnostics] == ["INVALID_RESPONSE_EXTRACTOR"]
    assert "sent_usb.read" in package.diagnostics[0].message


def test_compile_reports_invalid_response_matcher(tmp_path: Path):
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
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
          match: "VALUE:("
""".strip(),
        encoding="utf-8",
    )
    test_file = tmp_path / "sent-invalid-match.yaml"
    test_file.write_text(
        """
tool_profile: lab.tools.yaml
testcases:
  - name: sent_invalid_match_case
    steps:
      - sent_usb.read:
          device: sent_usb
          channel: 1
          save_as: sent_value
""".strip(),
        encoding="utf-8",
    )

    package = compile_file(test_file)

    assert [diagnostic.code for diagnostic in package.diagnostics] == ["INVALID_RESPONSE_MATCHER"]
    assert "sent_usb.read" in package.diagnostics[0].message
