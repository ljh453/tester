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
