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
