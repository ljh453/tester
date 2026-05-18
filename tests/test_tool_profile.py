from pathlib import Path

from embsw_tester.dsl.compiler import compile_file
from embsw_tester.tools.profile import load_tool_profile


def test_load_tool_profile_normalizes_serial_devices(tmp_path: Path):
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
      notes: "input format not confirmed"
    sent_usb:
      device_type: mach_systems_sent_usb
      port: COM4
      baudrate: 115200
      command_profile: sent_usb_line
""".strip(),
        encoding="utf-8",
    )

    profile = load_tool_profile(profile_file)

    assert profile["serial"]["devices"]["psu"]["device_type"] == "power_supply"
    assert profile["serial"]["devices"]["psu"]["command_profile"] == "pending"
    assert profile["serial"]["devices"]["sent_usb"]["device_type"] == "mach_systems_sent_usb"
    assert profile["serial"]["devices"]["sent_usb"]["baudrate"] == 115200


def test_load_tool_profile_normalizes_serial_framing_settings(tmp_path: Path):
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
serial:
  devices:
    sent_usb:
      device_type: mach_systems_sent_usb
      port: COM4
      baudrate: 115200
      parity: even
      stop_bits: 2
      byte_size: 7
      command_profile: sent_usb_line
""".strip(),
        encoding="utf-8",
    )

    profile = load_tool_profile(profile_file)

    sent_usb = profile["serial"]["devices"]["sent_usb"]
    assert sent_usb["parity"] == "even"
    assert sent_usb["stop_bits"] == 2.0
    assert sent_usb["byte_size"] == 7


def test_load_tool_profile_normalizes_trace32_settings(tmp_path: Path):
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
trace32:
  rcl:
    enabled: true
    client_factory: lab_trace32:create_client
    command_method: command
    client_args:
      node: TRACE32-A
  udp:
    enabled: true
    host: 127.0.0.1
    port: 20000
    terminator: "\\n"
    response_bytes: 2048
""".strip(),
        encoding="utf-8",
    )

    profile = load_tool_profile(profile_file)

    assert profile["trace32"]["rcl"]["enabled"] is True
    assert profile["trace32"]["rcl"]["client_factory"] == "lab_trace32:create_client"
    assert profile["trace32"]["rcl"]["command_method"] == "command"
    assert profile["trace32"]["rcl"]["client_args"] == {"node": "TRACE32-A"}
    assert profile["trace32"]["udp"]["enabled"] is True
    assert profile["trace32"]["udp"]["host"] == "127.0.0.1"
    assert profile["trace32"]["udp"]["port"] == 20000
    assert profile["trace32"]["udp"]["terminator"] == "\n"
    assert profile["trace32"]["udp"]["response_bytes"] == 2048


def test_load_tool_profile_normalizes_inca_helper_settings(tmp_path: Path):
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
inca:
  helper:
    enabled: true
    command:
      - C:/Python32/python.exe
      - C:/tools/inca_helper.py
""".strip(),
        encoding="utf-8",
    )

    profile = load_tool_profile(profile_file)

    assert profile["inca"]["helper"]["enabled"] is True
    assert profile["inca"]["helper"]["command"] == [
        "C:/Python32/python.exe",
        "C:/tools/inca_helper.py",
    ]


def test_load_tool_profile_normalizes_canoe_helper_settings(tmp_path: Path):
    profile_file = tmp_path / "vehicle-a.tools.yaml"
    profile_file.write_text(
        """
canoe:
  helper:
    enabled: yes
    application: canalyzer
    prog_id: CANalyzer.Application
    command:
      - C:/Python311/python.exe
      - -m
      - embsw_tester.adapters.canoe_com_helper
""",
        encoding="utf-8",
    )

    profile = load_tool_profile(profile_file)

    assert profile["canoe"]["helper"]["enabled"] is True
    assert profile["canoe"]["helper"]["application"] == "canalyzer"
    assert profile["canoe"]["helper"]["prog_id"] == "CANalyzer.Application"
    assert profile["canoe"]["helper"]["command"] == [
        "C:/Python311/python.exe",
        "-m",
        "embsw_tester.adapters.canoe_com_helper",
    ]


def test_load_tool_profile_normalizes_real_hardware_execution_guard(tmp_path: Path):
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
execution:
  requires_real_hardware: yes
  allow_env: LAB_HW_READY
  notes: "Only run after confirming the bench is connected."
""".strip(),
        encoding="utf-8",
    )

    profile = load_tool_profile(profile_file)

    assert profile["execution"]["requires_real_hardware"] is True
    assert profile["execution"]["allow_env"] == "LAB_HW_READY"
    assert profile["execution"]["notes"] == "Only run after confirming the bench is connected."


def test_compile_file_includes_tool_profile_snapshot(tmp_path: Path):
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
    sent_usb:
      device_type: mach_systems_sent_usb
      port: COM4
      baudrate: 115200
      command_profile: sent_usb_line
""".strip(),
        encoding="utf-8",
    )
    test_file = tmp_path / "case.yaml"
    test_file.write_text(
        """
tool_profile: lab.tools.yaml
testcases:
  - name: profile_case
    steps:
      - log.text:
          text: "profile loaded"
""".strip(),
        encoding="utf-8",
    )

    package = compile_file(test_file)

    assert package.diagnostics == []
    assert package.tool_profile_snapshot["serial"]["devices"]["psu"]["device_type"] == "power_supply"
    assert (
        package.tool_profile_snapshot["serial"]["devices"]["sent_usb"]["device_type"]
        == "mach_systems_sent_usb"
    )
    assert package.to_dict()["tool_profile_snapshot"]["serial"]["devices"]["psu"]["port"] == "COM3"


def test_compile_real_tools_smoke_sample_uses_lab_profile():
    package = compile_file(Path("samples/real-tools-smoke.yaml"))

    assert package.diagnostics == []
    assert package.tool_profile_snapshot["execution"]["requires_real_hardware"] is True
    assert "psu" in package.tool_profile_snapshot["serial"]["devices"]
    assert "sent_usb" in package.tool_profile_snapshot["serial"]["devices"]
    assert "trace32" in package.tool_profile_snapshot
    assert package.tool_profile_snapshot["inca"]["helper"]["enabled"] is True
