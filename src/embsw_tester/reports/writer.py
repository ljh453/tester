from __future__ import annotations

import html
import json
import re
from pathlib import Path
from typing import Any, Dict

import yaml

from embsw_tester.dsl.models import ResolvedPackage
from embsw_tester.reports.models import ReportArtifacts
from embsw_tester.runtime.models import RunResult, TestcaseResult


def write_report(
    package: ResolvedPackage,
    result: RunResult,
    reports_root: Path,
) -> ReportArtifacts:
    report_dir = Path(reports_root) / _safe_path_segment(result.run_id)
    testcase_results_dir = report_dir / "testcase-results"
    attachments_dir = report_dir / "attachments"
    raw_logs_dir = report_dir / "raw-logs"

    testcase_results_dir.mkdir(parents=True, exist_ok=True)
    attachments_dir.mkdir(parents=True, exist_ok=True)
    raw_logs_dir.mkdir(parents=True, exist_ok=True)

    run_json = report_dir / "run.json"
    resolved_package_yaml = report_dir / "resolved-package.yaml"
    summary_html = report_dir / "summary.html"

    _write_json(run_json, _run_payload(package, result))
    resolved_package_yaml.write_text(
        yaml.safe_dump(package.to_dict(), allow_unicode=True, sort_keys=False),
        encoding="utf-8",
    )
    for testcase_result in result.testcase_results:
        _write_json(
            testcase_results_dir / f"{_safe_path_segment(testcase_result.name)}.json",
            testcase_result.to_dict(),
        )
    summary_html.write_text(_render_summary_html(package, result), encoding="utf-8")

    return ReportArtifacts(
        report_dir=report_dir,
        run_json=run_json,
        resolved_package_yaml=resolved_package_yaml,
        summary_html=summary_html,
    )


def _run_payload(package: ResolvedPackage, result: RunResult) -> Dict[str, Any]:
    payload = result.to_dict()
    payload.update(
        {
            "schema_version": "0.1",
            "source_file": str(package.source_file),
            "source_file_hash": package.source_file_hash,
            "resolved_package": "resolved-package.yaml",
        }
    )
    return payload


def _write_json(path: Path, payload: Dict[str, Any]) -> None:
    path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def _render_summary_html(package: ResolvedPackage, result: RunResult) -> str:
    testcase_rows = "\n".join(
        _render_testcase_row(testcase_result)
        for testcase_result in result.testcase_results
    )
    return f"""<!doctype html>
<html lang="ko">
<head>
  <meta charset="utf-8">
  <title>Embedded SW Tester Report - {html.escape(result.run_id)}</title>
  <style>
    body {{ font-family: Arial, sans-serif; margin: 32px; color: #1f2937; }}
    h1 {{ margin-bottom: 8px; }}
    .meta {{ color: #4b5563; margin-bottom: 24px; }}
    table {{ border-collapse: collapse; width: 100%; }}
    th, td {{ border: 1px solid #d1d5db; padding: 8px; text-align: left; }}
    th {{ background: #f3f4f6; }}
    .passed {{ color: #047857; font-weight: 700; }}
    .failed {{ color: #b91c1c; font-weight: 700; }}
  </style>
</head>
<body>
  <h1>Embedded SW Tester Report</h1>
  <div class="meta">
    <div>Run ID: {html.escape(result.run_id)}</div>
    <div>Status: <span class="{html.escape(result.status)}">{html.escape(result.status)}</span></div>
    <div>Source: {html.escape(str(package.source_file))}</div>
  </div>
  <h2>Testcases</h2>
  <table>
    <thead>
      <tr><th>Name</th><th>Status</th><th>Events</th><th>Error</th></tr>
    </thead>
    <tbody>
{testcase_rows}
    </tbody>
  </table>
</body>
</html>
"""


def _render_testcase_row(testcase_result: TestcaseResult) -> str:
    return (
        "      <tr>"
        f"<td>{html.escape(testcase_result.name)}</td>"
        f"<td class=\"{html.escape(testcase_result.status)}\">{html.escape(testcase_result.status)}</td>"
        f"<td>{len(testcase_result.events)}</td>"
        f"<td>{html.escape(testcase_result.error or '')}</td>"
        "</tr>"
    )


def _safe_path_segment(value: str) -> str:
    safe = re.sub(r"[^A-Za-z0-9_.-]+", "_", value).strip("._")
    return safe or "unnamed"
