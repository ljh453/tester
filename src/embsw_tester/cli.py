from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Sequence

from embsw_tester.dsl.compiler import compile_file
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
        result = run_package(package)
        payload = result.to_dict()
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


if __name__ == "__main__":
    raise SystemExit(main())
