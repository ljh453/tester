from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path
from typing import Any, Mapping, Optional, Sequence

from embsw_tester.adapters import create_adapter_registry_from_tool_profile
from embsw_tester.dsl.compiler import compile_file
from embsw_tester.reports import write_report
from embsw_tester.runtime import RuntimeControl, run_package


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="embsw-tester")
    subparsers = parser.add_subparsers(dest="command", required=True)

    compile_parser = subparsers.add_parser("compile")
    compile_parser.add_argument("yaml_file", type=Path)
    compile_parser.add_argument("--json", action="store_true", dest="json_output")

    run_parser = subparsers.add_parser("run")
    run_parser.add_argument("yaml_file", type=Path)
    run_parser.add_argument("--json", action="store_true", dest="json_output")
    run_parser.add_argument("--run-id")
    run_parser.add_argument("--reports-root", type=Path)
    run_parser.add_argument("--use-tool-profile-adapters", action="store_true")
    run_parser.add_argument("--allow-real-hardware", action="store_true")
    run_parser.add_argument("--events-jsonl", action="store_true")
    run_parser.add_argument("--control-file", type=Path)
    run_parser.add_argument(
        "--testcase",
        action="append",
        default=[],
        dest="testcase_names",
        help="Run only the named testcase. Can be provided multiple times.",
    )
    run_parser.add_argument(
        "--breakpoint-line",
        type=int,
        action="append",
        default=[],
        dest="breakpoint_lines",
    )

    args = parser.parse_args(argv)
    if args.command == "compile":
        package = compile_file(args.yaml_file)
        payload = package.to_dict()
        if args.json_output:
            print(json.dumps(payload, ensure_ascii=False, indent=2))
        else:
            _print_text_summary(payload)
        return 1 if package.diagnostics else 0

    if args.command == "run":
        package = compile_file(args.yaml_file)
        real_hardware_diagnostic = _real_hardware_guard_diagnostic(package, args)
        if real_hardware_diagnostic is not None:
            payload = {
                "run_id": args.run_id or "",
                "status": "failed",
                "testcase_results": [],
                "diagnostics": [real_hardware_diagnostic],
            }
            if args.json_output:
                print(json.dumps(payload, ensure_ascii=False, indent=2))
            else:
                _print_run_summary(payload)
            return 1

        adapter_registry = None
        if args.use_tool_profile_adapters:
            adapter_registry = create_adapter_registry_from_tool_profile(
                package.tool_profile_snapshot,
                evidence_root=_profile_adapter_evidence_root(args),
            )
        result = run_package(
            package,
            run_id=args.run_id,
            adapter_registry=adapter_registry,
            event_callback=_event_jsonl_callback if args.events_jsonl else None,
            run_control=_runtime_control(args),
            testcase_names=args.testcase_names,
        )
        payload = result.to_dict()
        if args.reports_root is not None:
            artifacts = write_report(package, result, reports_root=args.reports_root)
            payload["report"] = artifacts.to_dict()
        if args.json_output:
            print(json.dumps(payload, ensure_ascii=False, indent=2))
        else:
            _print_run_summary(payload)
        return 0 if result.status == "passed" else 1

    return 2


def _print_text_summary(payload: dict) -> None:
    print(f"source: {payload['source_file']}")
    print(f"testcases: {len(payload['testcases'])}")
    print(f"functions: {len(payload['functions'])}")
    print(f"diagnostics: {len(payload['diagnostics'])}")
    for diagnostic in payload["diagnostics"]:
        print(f"- {diagnostic['severity']} {diagnostic['code']}: {diagnostic['message']}")


def _print_run_summary(payload: dict) -> None:
    print(f"run_id: {payload['run_id']}")
    print(f"status: {payload['status']}")
    print(f"testcases: {len(payload['testcase_results'])}")
    for diagnostic in payload.get("diagnostics", []):
        print(f"- {diagnostic['severity']} {diagnostic['code']}: {diagnostic['message']}")
    for testcase_result in payload["testcase_results"]:
        print(f"- {testcase_result['name']}: {testcase_result['status']}")
    if "report" in payload:
        print(f"report: {payload['report']['report_dir']}")


def _event_jsonl_callback(event: object) -> None:
    payload = event.to_dict()
    print(
        f"__EMBSW_EVENT__ {json.dumps(payload, ensure_ascii=False)}",
        file=sys.stderr,
        flush=True,
    )


def _runtime_control(args: argparse.Namespace) -> Optional[RuntimeControl]:
    if args.control_file is None:
        return None
    return RuntimeControl(
        control_file=args.control_file,
        breakpoint_lines=set(args.breakpoint_lines or []),
    )


def _profile_adapter_evidence_root(args: argparse.Namespace) -> Path:
    if args.reports_root is not None and args.run_id:
        return args.reports_root / args.run_id
    if args.reports_root is not None:
        return args.reports_root
    return Path("reports")


def _real_hardware_guard_diagnostic(
    package: Any,
    args: argparse.Namespace,
) -> Optional[dict[str, Any]]:
    if package.diagnostics:
        return None
    execution = package.tool_profile_snapshot.get("execution", {})
    if not isinstance(execution, Mapping):
        return None
    if not bool(execution.get("requires_real_hardware", False)):
        return None
    allow_env = str(execution.get("allow_env", "EMBSW_TESTER_ALLOW_REAL_HARDWARE"))
    if args.allow_real_hardware or _truthy_environment(allow_env):
        return None
    return {
        "code": "REAL_HARDWARE_CONFIRMATION_REQUIRED",
        "message": (
            "Tool profile requires real hardware. Connect the lab devices and pass "
            f"--allow-real-hardware or set {allow_env}=1 to run this YAML."
        ),
        "severity": "error",
        "path": ["tool_profile", "execution", "requires_real_hardware"],
        "source_file": str(package.source_file),
    }


def _truthy_environment(name: str) -> bool:
    value = os.environ.get(name)
    return value is not None and value.strip().lower() in {"1", "true", "yes", "on"}


if __name__ == "__main__":
    raise SystemExit(main())
