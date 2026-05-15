import json
from pathlib import Path

import yaml

from embsw_tester.dsl.compiler import compile_file
from embsw_tester.reports import write_report
from embsw_tester.runtime import run_package


def test_write_report_creates_run_artifacts(tmp_path: Path):
    test_file = tmp_path / "report.yaml"
    test_file.write_text(
        """
testcases:
  - name: report_case
    steps:
      - set:
          var: ok
          value: true
      - assert.eq:
          left: "${ok}"
          right: true
""".strip(),
        encoding="utf-8",
    )
    package = compile_file(test_file)
    result = run_package(package, run_id="run-report-1")

    artifacts = write_report(package, result, reports_root=tmp_path / "reports")

    assert artifacts.report_dir == tmp_path / "reports" / "run-report-1"
    assert json.loads(artifacts.run_json.read_text(encoding="utf-8"))["status"] == "passed"
    resolved_package = yaml.safe_load(
        artifacts.resolved_package_yaml.read_text(encoding="utf-8")
    )
    assert resolved_package["testcases"][0]["name"] == "report_case"
    assert "report_case" in artifacts.summary_html.read_text(encoding="utf-8")
    assert (artifacts.report_dir / "testcase-results" / "report_case.json").is_file()
    assert (artifacts.report_dir / "attachments").is_dir()
    assert (artifacts.report_dir / "raw-logs").is_dir()
