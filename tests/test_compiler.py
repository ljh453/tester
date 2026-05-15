from pathlib import Path

from embsw_tester.dsl.compiler import compile_file


def test_compile_file_resolves_imported_function_and_normalizes_commands(tmp_path: Path):
    lib_dir = tmp_path / "libs"
    lib_dir.mkdir()
    (lib_dir / "common-power-sequence.yaml").write_text(
        """
functions:
  power_on:
    params: [channel]
    returns: [voltage_ok]
    steps:
      - serial.write:
          port: psu
          text: "OUT {{ channel }} ON"
      - set:
          var: voltage_ok
          value: true
""".strip(),
        encoding="utf-8",
    )
    test_file = tmp_path / "boot-smoke.yaml"
    test_file.write_text(
        """
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
""".strip(),
        encoding="utf-8",
    )

    package = compile_file(test_file)

    assert package.diagnostics == []
    assert "power_on" in package.functions
    assert package.testcases[0].steps[0].type == "call"
    assert package.testcases[0].steps[1].type == "assert.eq"
    assert package.testcases[0].steps[1].args == {"left": "${power_ready}", "right": True}


def test_compile_file_reports_unknown_command(tmp_path: Path):
    test_file = tmp_path / "bad.yaml"
    test_file.write_text(
        """
testcases:
  - name: bad_case
    steps:
      - unknown.command:
          value: 1
""".strip(),
        encoding="utf-8",
    )

    package = compile_file(test_file)

    assert len(package.diagnostics) == 1
    assert package.diagnostics[0].code == "UNKNOWN_COMMAND"
    assert "unknown.command" in package.diagnostics[0].message


def test_sample_boot_smoke_compiles_without_diagnostics():
    sample_file = Path("samples/boot-smoke.yaml")

    package = compile_file(sample_file)

    assert package.diagnostics == []
    assert package.testcases[0].name == "boot_smoke"
