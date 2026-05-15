from __future__ import annotations

import re
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Protocol

from embsw_tester.adapters.base import AdapterContext, AdapterResult


class SerialPort(Protocol):
    def write_line(self, text: str) -> int:
        ...

    def read_line(self, timeout_ms: int) -> str:
        ...


class FakeSerialPort:
    def __init__(self, rx_lines: Iterable[str]):
        self.rx_lines: List[str] = list(rx_lines)
        self.tx_lines: List[str] = []

    def write_line(self, text: str) -> int:
        self.tx_lines.append(text)
        return len(text)

    def read_line(self, timeout_ms: int) -> str:
        if not self.rx_lines:
            raise TimeoutError(f"Serial read timed out after {timeout_ms} ms.")
        return self.rx_lines.pop(0)


class SerialAdapter:
    name = "serial"

    def __init__(self, ports: Dict[str, SerialPort], evidence_root: Path):
        self._ports = ports
        self._evidence_root = Path(evidence_root)

    def execute(
        self,
        command_type: str,
        args: Dict[str, object],
        context: AdapterContext,
    ) -> AdapterResult:
        if command_type == "serial.write":
            return self._execute_write(args, context)
        if command_type == "serial.read":
            return self._execute_read(args, context)
        return AdapterResult(
            success=False,
            status="failed",
            message=f"Unsupported serial command '{command_type}'.",
        )

    def _execute_write(self, args: Dict[str, object], context: AdapterContext) -> AdapterResult:
        port_name = _required_text(args, "port")
        text = _required_text(args, "text")
        port = self._get_port(port_name)
        bytes_written = port.write_line(text)
        evidence_ref = self._append_evidence(context, f"TX {text}\n")
        return AdapterResult(
            success=True,
            status="passed",
            message=f"Wrote to serial port '{port_name}'.",
            values={
                "port": port_name,
                "tx": text,
                "bytes_written": bytes_written,
            },
            raw_evidence_ref=evidence_ref,
        )

    def _execute_read(self, args: Dict[str, object], context: AdapterContext) -> AdapterResult:
        port_name = _required_text(args, "port")
        timeout_ms = int(args.get("timeout_ms", 1000))
        port = self._get_port(port_name)
        text = port.read_line(timeout_ms)
        expected = args.get("until")
        if expected is not None and str(expected) not in text:
            return AdapterResult(
                success=False,
                status="failed",
                message=f"Serial response did not contain expected text '{expected}'.",
                values={"port": port_name, "text": text, "timeout_ms": timeout_ms},
            )
        evidence_ref = self._append_evidence(context, f"RX {text}\n")
        return AdapterResult(
            success=True,
            status="passed",
            message=f"Read from serial port '{port_name}'.",
            values={
                "port": port_name,
                "text": text,
                "timeout_ms": timeout_ms,
            },
            raw_evidence_ref=evidence_ref,
        )

    def _get_port(self, port_name: str) -> SerialPort:
        try:
            return self._ports[port_name]
        except KeyError as exc:
            raise KeyError(f"Serial port '{port_name}' is not configured.") from exc

    def _append_evidence(self, context: AdapterContext, line: str) -> str:
        relative_path = Path("raw-logs") / "serial" / _safe_segment(context.run_id) / f"{_safe_segment(context.testcase)}.log"
        evidence_path = self._evidence_root / relative_path
        evidence_path.parent.mkdir(parents=True, exist_ok=True)
        with evidence_path.open("a", encoding="utf-8") as stream:
            stream.write(line)
        return relative_path.as_posix()


def _required_text(args: Dict[str, object], name: str) -> str:
    value = args.get(name)
    if value is None:
        raise KeyError(f"Missing required serial argument '{name}'.")
    return str(value)


def _safe_segment(value: str) -> str:
    safe = re.sub(r"[^A-Za-z0-9_.-]+", "_", value).strip("._")
    return safe or "unnamed"
