from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Sequence

from embsw_tester.adapters import create_adapter_registry_from_tool_profile
from embsw_tester.dsl.compiler import compile_file
from embsw_tester.reports import write_report
from embsw_tester.runtime import run_package


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
    for testcase_result in payload["testcase_results"]:
        print(f"- {testcase_result['name']}: {testcase_result['status']}")
    if "report" in payload:
        print(f"report: {payload['report']['report_dir']}")


def _profile_adapter_evidence_root(args: argparse.Namespace) -> Path:
    if args.reports_root is not None and args.run_id:
        return args.reports_root / args.run_id
    if args.reports_root is not None:
        return args.reports_root
    return Path("reports")


if __name__ == "__main__":
    raise SystemExit(main())
