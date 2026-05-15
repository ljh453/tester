# Embedded SW Tester Phase 3 Report Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist runtime execution results into a reproducible report directory containing machine-readable JSON/YAML and a simple human-readable HTML summary.

**Architecture:** Add an `embsw_tester.reports` package that consumes `ResolvedPackage` and `RunResult`. The writer creates `reports/<run-id>/`, stores `run.json`, `resolved-package.yaml`, per-testcase JSON files, empty evidence folders, and `summary.html`; the existing CLI `run` command gains optional report output flags.

**Tech Stack:** Python 3.9+, PyYAML, pytest, standard-library json/html/pathlib.

---

## File Structure

- `src/embsw_tester/reports/__init__.py`: exports `write_report`.
- `src/embsw_tester/reports/models.py`: report artifact dataclass.
- `src/embsw_tester/reports/writer.py`: filesystem report writer and summary HTML renderer.
- `src/embsw_tester/cli.py`: add `--reports-root` and `--run-id` to `run`.
- `tests/test_reports.py`: report writer tests.
- `tests/test_cli.py`: CLI report output test.
- `README.md`: document report generation.

### Task 1: Report Writer

**Files:**
- Create: `tests/test_reports.py`
- Create: `src/embsw_tester/reports/__init__.py`
- Create: `src/embsw_tester/reports/models.py`
- Create: `src/embsw_tester/reports/writer.py`

- [ ] **Step 1: Write failing report test**

```python
from pathlib import Path
import json
import yaml

from embsw_tester.dsl.compiler import compile_file
from embsw_tester.reports import write_report
from embsw_tester.runtime import run_package


def test_write_report_creates_run_artifacts(tmp_path: Path):
    test_file = tmp_path / "report.yaml"
    test_file.write_text(
        '''
testcases:
  - name: report_case
    steps:
      - set:
          var: ok
          value: true
      - assert.eq:
          left: "${ok}"
          right: true
'''.strip(),
        encoding="utf-8",
    )
    package = compile_file(test_file)
    result = run_package(package, run_id="run-report-1")

    artifacts = write_report(package, result, reports_root=tmp_path / "reports")

    assert artifacts.report_dir == tmp_path / "reports" / "run-report-1"
    assert json.loads(artifacts.run_json.read_text(encoding="utf-8"))["status"] == "passed"
    assert yaml.safe_load(artifacts.resolved_package_yaml.read_text(encoding="utf-8"))["testcases"][0]["name"] == "report_case"
    assert "report_case" in artifacts.summary_html.read_text(encoding="utf-8")
    assert (artifacts.report_dir / "attachments").is_dir()
    assert (artifacts.report_dir / "raw-logs").is_dir()
```

- [ ] **Step 2: Run and verify the test fails**

Run: `.venv/bin/python -m pytest tests/test_reports.py -q`

Expected: FAIL with `ModuleNotFoundError: No module named 'embsw_tester.reports'`.

- [ ] **Step 3: Implement report writer**

Create directories, write JSON/YAML/HTML, and return paths.

- [ ] **Step 4: Verify report tests pass**

Run: `.venv/bin/python -m pytest tests/test_reports.py -q`

Expected: report test passes.

### Task 2: CLI Integration

**Files:**
- Modify: `tests/test_cli.py`
- Modify: `src/embsw_tester/cli.py`
- Modify: `README.md`

- [ ] **Step 1: Write failing CLI report test**

Invoke `python -m embsw_tester.cli run samples/boot-smoke.yaml --json --run-id cli-report --reports-root <tmp>` and assert `report["report_dir"]` exists in the JSON payload.

- [ ] **Step 2: Run and verify failure**

Run: `.venv/bin/python -m pytest tests/test_cli.py -q`

Expected: FAIL until CLI report flags exist.

- [ ] **Step 3: Implement CLI flags**

Add `--run-id` and `--reports-root`. When `--reports-root` is present, call `write_report` and include `report` in the JSON payload.

- [ ] **Step 4: Update README**

Document report generation and output files.

- [ ] **Step 5: Verify full suite and CLI smoke**

Run:

```bash
.venv/bin/python -m pytest
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id smoke-report --reports-root reports --json
```

Expected: all tests pass and report files are written.

## Self-Review

- This plan implements the report slice promised after Phase 2.
- It intentionally does not add custom report templates or rich styling.
- Real raw tool evidence is deferred until actual adapter implementations exist.
