# Embedded SW Tester Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first executable slice of the Python DSL compiler that converts YAML test definitions into a resolved run package with diagnostics.

**Architecture:** Start with a small Python package under `src/embsw_tester`. The compiler loads YAML, resolves library imports, normalizes short command syntax into typed command objects, validates command names and function calls, then returns a serializable resolved package.

**Tech Stack:** Python 3.9+, PyYAML, pytest.

---

## File Structure

- `pyproject.toml`: project metadata, runtime dependency on PyYAML, pytest test settings.
- `src/embsw_tester/__init__.py`: package marker.
- `src/embsw_tester/dsl/__init__.py`: DSL package exports.
- `src/embsw_tester/dsl/models.py`: dataclasses for diagnostics, commands, functions, testcases, resolved packages.
- `src/embsw_tester/dsl/catalog.py`: initial command catalog and required argument rules.
- `src/embsw_tester/dsl/compiler.py`: YAML loading, import resolution, command normalization, semantic validation.
- `tests/test_compiler.py`: pytest tests for the first compiler behaviors.
- `samples/boot-smoke.yaml`: executable YAML sample.
- `samples/libs/common-power-sequence.yaml`: function library sample.

### Task 1: Documentation And Test Skeleton

**Files:**
- Create: `docs/design/embedded-sw-tester-detailed-design.md`
- Create: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase1.md`
- Create: `pyproject.toml`
- Create: `tests/test_compiler.py`

- [ ] **Step 1: Write compiler behavior tests first**

```python
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
```

- [ ] **Step 2: Run pytest and verify the tests fail because implementation is missing**

Run: `.venv/bin/python -m pytest`

Expected: pytest collection fails or tests fail because `embsw_tester.dsl.compiler` does not exist yet.

### Task 2: Minimal DSL Models And Compiler

**Files:**
- Create: `src/embsw_tester/__init__.py`
- Create: `src/embsw_tester/dsl/__init__.py`
- Create: `src/embsw_tester/dsl/models.py`
- Create: `src/embsw_tester/dsl/catalog.py`
- Create: `src/embsw_tester/dsl/compiler.py`

- [ ] **Step 1: Add dataclasses**

Implement `Diagnostic`, `NormalizedCommand`, `FunctionDef`, `TestcaseDef`, and `ResolvedPackage`.

- [ ] **Step 2: Add command catalog**

Implement a minimal set containing `call`, `set`, `assert.eq`, and `serial.write`.

- [ ] **Step 3: Add compiler**

Implement `compile_file(path: Path) -> ResolvedPackage` with YAML loading, relative import resolution, command normalization, and unknown command diagnostics.

- [ ] **Step 4: Run pytest and verify the tests pass**

Run: `.venv/bin/python -m pytest`

Expected: 2 tests pass.

### Task 3: Samples

**Files:**
- Create: `samples/boot-smoke.yaml`
- Create: `samples/libs/common-power-sequence.yaml`

- [ ] **Step 1: Add sample library and executable YAML**

Add the same function and testcase flow used by the passing test.

- [ ] **Step 2: Compile sample through pytest-backed code path**

Run: `.venv/bin/python -m pytest`

Expected: 2 tests pass and sample files remain parseable by the same compiler API.

## Self-Review

- The plan covers the initial compiler slice selected in the design document.
- Runtime, IPC, IDE, report, and real adapters are intentionally deferred to later phases.
- All implementation work starts from pytest tests.
