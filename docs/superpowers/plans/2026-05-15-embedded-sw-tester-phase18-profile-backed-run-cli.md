# Embedded SW Tester Phase 18 Profile-Backed Run CLI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an opt-in CLI run mode that builds the adapter registry from the resolved tool profile snapshot instead of always using mock adapters.

**Architecture:** Keep the default `embsw-tester run` path mock-safe. Add `--use-tool-profile-adapters` to the `run` command; when present, the CLI calls `create_adapter_registry_from_tool_profile(...)` and passes that registry to `run_package`. Evidence root is derived from `--reports-root` and `--run-id` when available, otherwise from a local `reports` directory.

**Tech Stack:** Python 3.9+, argparse, pytest subprocess CLI tests.

---

## File Structure

- `src/embsw_tester/cli.py`: add the opt-in profile-backed adapter registry mode.
- `tests/test_cli.py`: prove default run remains mock-backed and opt-in run uses profile-backed adapters.
- `README.md`: document the new CLI option.
- `docs/design/embedded-sw-tester-detailed-design.md`: update CLI/run-mode design notes.
- `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase18-profile-backed-run-cli.md`: track this phase.

### Task 1: Failing Tests

**Files:**
- Modify: `tests/test_cli.py`

- [x] **Step 1: Add profile-backed run CLI test**

Create a temp tool profile:

```yaml
trace32: {}
```

Create a temp YAML testcase:

```yaml
tool_profile: tools.yaml
testcases:
  - name: trace32_profile_path
    steps:
      - trace32.command:
          command: "STATE()"
          fallback: false
```

Run the CLI twice:

- Without `--use-tool-profile-adapters`, expect return code `0` because the default mock adapter handles `trace32.command`.
- With `--use-tool-profile-adapters`, expect return code `1` and an event error mentioning Trace32 transport is not configured.

- [x] **Step 2: Run focused test and verify RED**

Run:

```bash
.venv/bin/python -m pytest tests/test_cli.py::test_cli_run_can_use_tool_profile_adapters -q
```

Expected: argparse fails because `--use-tool-profile-adapters` does not exist yet.

### Task 2: CLI Implementation

**Files:**
- Modify: `src/embsw_tester/cli.py`

- [x] **Step 1: Add `--use-tool-profile-adapters` option**

Add:

```python
run_parser.add_argument("--use-tool-profile-adapters", action="store_true")
```

- [x] **Step 2: Build adapter registry from tool profile**

When the option is set:

```python
registry = create_adapter_registry_from_tool_profile(
    package.tool_profile_snapshot,
    evidence_root=_profile_adapter_evidence_root(args),
)
result = run_package(package, run_id=args.run_id, adapter_registry=registry)
```

When it is not set, keep existing `run_package(package, run_id=args.run_id)` behavior.

- [x] **Step 3: Add evidence root helper**

Implement:

```python
def _profile_adapter_evidence_root(args) -> Path:
    if args.reports_root is not None and args.run_id:
        return args.reports_root / args.run_id
    if args.reports_root is not None:
        return args.reports_root
    return Path("reports")
```

### Task 3: Docs And Verification

**Files:**
- Modify: `README.md`
- Modify: `docs/design/embedded-sw-tester-detailed-design.md`
- Modify: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase18-profile-backed-run-cli.md`

- [x] **Step 1: Document the opt-in CLI mode**

Show:

```bash
.venv/bin/embsw-tester run samples/boot-smoke.yaml --use-tool-profile-adapters --run-id real-tools-run --reports-root reports --json
```

Explain that default run remains mock-backed.

- [x] **Step 2: Run focused tests**

Run:

```bash
.venv/bin/python -m pytest tests/test_cli.py -q
```

Expected: all CLI tests pass.

- [x] **Step 3: Run full pytest**

Run:

```bash
.venv/bin/python -m pytest
```

Expected: all tests pass.

- [x] **Step 4: Run CLI smoke**

Run:

```bash
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id profile-backed-cli-smoke --reports-root reports --json
```

Expected: default mock-backed run still returns `status: passed` and `diagnostics: []`.

- [ ] **Step 5: Commit and push**

Commit:

```bash
git add README.md docs/design/embedded-sw-tester-detailed-design.md docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase18-profile-backed-run-cli.md src/embsw_tester/cli.py tests/test_cli.py
git commit -m "feat: add profile backed run cli"
git push -u origin main
```

## Self-Review

- The default CLI path remains safe for local development and CI.
- Real adapter construction requires an explicit CLI flag.
- The test proves the flag changes behavior without touching physical hardware.
