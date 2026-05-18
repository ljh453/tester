import json
import os
import subprocess
import sys
from pathlib import Path


def test_cli_compile_outputs_resolved_package_json():
    env = os.environ.copy()
    env["PYTHONPATH"] = str(Path("src").resolve())

    result = subprocess.run(
        [
            sys.executable,
            "-m",
            "embsw_tester.cli",
            "compile",
            "samples/boot-smoke.yaml",
            "--json",
        ],
        cwd=Path.cwd(),
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0
    payload = json.loads(result.stdout)
    assert payload["diagnostics"] == []
    assert payload["testcases"][0]["name"] == "boot_smoke"
    assert payload["testcases"][0]["steps"][0]["type"] == "call"


def test_cli_run_outputs_run_result_json():
    env = os.environ.copy()
    env["PYTHONPATH"] = str(Path("src").resolve())

    result = subprocess.run(
        [
            sys.executable,
            "-m",
            "embsw_tester.cli",
            "run",
            "samples/boot-smoke.yaml",
            "--json",
        ],
        cwd=Path.cwd(),
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0
    payload = json.loads(result.stdout)
    assert payload["status"] == "passed"
    assert payload["testcase_results"][0]["name"] == "boot_smoke"
    assert payload["testcase_results"][0]["variables"] == {"power_ready": True}


def test_cli_run_streams_event_json_lines_without_breaking_final_json(tmp_path: Path):
    env = os.environ.copy()
    env["PYTHONPATH"] = str(Path("src").resolve())
    test_file = tmp_path / "stream.yaml"
    test_file.write_text(
        """
testcases:
  - name: stream_case
    steps:
      - set:
          var: rpm
          value: 700
""".strip(),
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            sys.executable,
            "-m",
            "embsw_tester.cli",
            "run",
            str(test_file),
            "--json",
            "--events-jsonl",
        ],
        cwd=Path.cwd(),
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0
    payload = json.loads(result.stdout)
    assert payload["status"] == "passed"
    event_lines = [
        line.removeprefix("__EMBSW_EVENT__ ")
        for line in result.stderr.splitlines()
        if line.startswith("__EMBSW_EVENT__ ")
    ]
    assert len(event_lines) == 2
    running_event = json.loads(event_lines[0])
    passed_event = json.loads(event_lines[1])
    assert running_event["command_type"] == "set"
    assert running_event["status"] == "running"
    assert running_event["source_line"] == 4
    assert running_event["local_variables"] == {}
    assert passed_event["command_type"] == "set"
    assert passed_event["status"] == "passed"
    assert passed_event["source_line"] == 4
    assert passed_event["local_variables"] == {"rpm": 700}


def test_cli_run_writes_report_when_reports_root_is_set(tmp_path: Path):
    env = os.environ.copy()
    env["PYTHONPATH"] = str(Path("src").resolve())

    result = subprocess.run(
        [
            sys.executable,
            "-m",
            "embsw_tester.cli",
            "run",
            "samples/boot-smoke.yaml",
            "--json",
            "--run-id",
            "cli-report",
            "--reports-root",
            str(tmp_path / "reports"),
        ],
        cwd=Path.cwd(),
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0
    payload = json.loads(result.stdout)
    assert payload["status"] == "passed"
    report_dir = Path(payload["report"]["report_dir"])
    assert report_dir == tmp_path / "reports" / "cli-report"
    assert (report_dir / "run.json").is_file()
    assert (report_dir / "resolved-package.yaml").is_file()
    assert (report_dir / "summary.html").is_file()


def test_cli_run_can_use_tool_profile_adapters(tmp_path: Path):
    env = os.environ.copy()
    env["PYTHONPATH"] = str(Path("src").resolve())
    profile_file = tmp_path / "tools.yaml"
    profile_file.write_text("trace32: {}\n", encoding="utf-8")
    test_file = tmp_path / "trace32.yaml"
    test_file.write_text(
        """
tool_profile: tools.yaml
testcases:
  - name: trace32_profile_path
    steps:
      - trace32.command:
          command: "STATE()"
          fallback: false
""".strip(),
        encoding="utf-8",
    )

    mock_result = subprocess.run(
        [
            sys.executable,
            "-m",
            "embsw_tester.cli",
            "run",
            str(test_file),
            "--json",
        ],
        cwd=Path.cwd(),
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )
    profile_result = subprocess.run(
        [
            sys.executable,
            "-m",
            "embsw_tester.cli",
            "run",
            str(test_file),
            "--json",
            "--use-tool-profile-adapters",
        ],
        cwd=Path.cwd(),
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )

    assert mock_result.returncode == 0
    assert json.loads(mock_result.stdout)["status"] == "passed"
    assert profile_result.returncode == 1
    payload = json.loads(profile_result.stdout)
    assert payload["status"] == "failed"
    event = payload["testcase_results"][0]["events"][0]
    assert event["command_type"] == "trace32.command"
    assert "transport is not configured" in event["error"]


def test_cli_run_blocks_real_hardware_profile_without_confirmation(tmp_path: Path):
    env = os.environ.copy()
    env["PYTHONPATH"] = str(Path("src").resolve())
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
execution:
  requires_real_hardware: true
  allow_env: LAB_HW_READY
""".strip(),
        encoding="utf-8",
    )
    test_file = tmp_path / "real-smoke.yaml"
    test_file.write_text(
        """
tool_profile: lab.tools.yaml
testcases:
  - name: guarded_case
    steps:
      - set:
          var: ready
          value: true
""".strip(),
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            sys.executable,
            "-m",
            "embsw_tester.cli",
            "run",
            str(test_file),
            "--json",
        ],
        cwd=Path.cwd(),
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 1
    payload = json.loads(result.stdout)
    assert payload["status"] == "failed"
    assert payload["testcase_results"] == []
    assert payload["diagnostics"][0]["code"] == "REAL_HARDWARE_CONFIRMATION_REQUIRED"
    assert "--allow-real-hardware" in payload["diagnostics"][0]["message"]
    assert "LAB_HW_READY=1" in payload["diagnostics"][0]["message"]


def test_cli_run_allows_real_hardware_profile_with_confirmation_env(tmp_path: Path):
    env = os.environ.copy()
    env["PYTHONPATH"] = str(Path("src").resolve())
    env["LAB_HW_READY"] = "1"
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        """
execution:
  requires_real_hardware: true
  allow_env: LAB_HW_READY
""".strip(),
        encoding="utf-8",
    )
    test_file = tmp_path / "real-smoke.yaml"
    test_file.write_text(
        """
tool_profile: lab.tools.yaml
testcases:
  - name: guarded_case
    steps:
      - set:
          var: ready
          value: true
""".strip(),
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            sys.executable,
            "-m",
            "embsw_tester.cli",
            "run",
            str(test_file),
            "--json",
        ],
        cwd=Path.cwd(),
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0
    payload = json.loads(result.stdout)
    assert payload["status"] == "passed"
    assert payload["testcase_results"][0]["variables"] == {"ready": True}
