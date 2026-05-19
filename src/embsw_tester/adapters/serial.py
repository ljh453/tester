from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Protocol

from embsw_tester.adapters.base import AdapterContext, AdapterResult


class SerialPort(Protocol):
    def write_line(self, text: str) -> int:
        ...

    def read_line(self, timeout_ms: int) -> str:
        ...

    def write_bytes(self, payload: bytes) -> int:
        ...

    def read_bytes(self, count: int, timeout_ms: int) -> bytes:
        ...


@dataclass(frozen=True)
class SerialPortSettings:
    logical_name: str
    system_port: str
    baudrate: int = 9600
    timeout_ms: int = 1000
    parity: str = "none"
    stop_bits: float = 1.0
    byte_size: int = 8
    line_ending: str = "\n"
    encoding: str = "utf-8"
    write_flush: bool = True
    dtr: Optional[bool] = None
    rts: Optional[bool] = None
    device_type: str = "generic_serial"
    command_profile: str = "raw_line"


class PySerialPort:
    def __init__(self, settings: SerialPortSettings):
        try:
            import serial  # type: ignore[import-not-found]
        except ImportError as exc:
            raise RuntimeError(
                "pyserial is required for real serial ports. Install it with 'pip install pyserial'."
            ) from exc

        self._serial = serial.Serial(
            port=settings.system_port,
            baudrate=settings.baudrate,
            timeout=settings.timeout_ms / 1000,
            write_timeout=settings.timeout_ms / 1000,
            parity=_pyserial_parity(settings.parity),
            stopbits=float(settings.stop_bits),
            bytesize=int(settings.byte_size),
        )
        self._line_ending = _normalize_line_ending(settings.line_ending)
        self._encoding = settings.encoding
        self._write_flush = settings.write_flush
        self.last_tx_payload: Optional[bytes] = None
        if settings.dtr is not None:
            self._serial.dtr = settings.dtr
        if settings.rts is not None:
            self._serial.rts = settings.rts

    def write_line(self, text: str) -> int:
        payload = f"{text}{self._line_ending}".encode(self._encoding)
        return self._write_payload(payload)

    def read_line(self, timeout_ms: int) -> str:
        self._serial.timeout = timeout_ms / 1000
        data = self._serial.readline()
        if not data:
            raise TimeoutError(f"Serial read timed out after {timeout_ms} ms.")
        return data.decode(self._encoding, errors="replace").rstrip("\r\n")

    def write_bytes(self, payload: bytes) -> int:
        return self._write_payload(bytes(payload))

    def read_bytes(self, count: int, timeout_ms: int) -> bytes:
        self._serial.timeout = timeout_ms / 1000
        data = bytes(self._serial.read(count))
        if len(data) != count:
            raise TimeoutError(
                f"Serial byte read timed out after {timeout_ms} ms: expected {count} bytes, got {len(data)}."
            )
        return data

    def _write_payload(self, payload: bytes) -> int:
        self.last_tx_payload = payload
        bytes_written = int(self._serial.write(payload))
        if self._write_flush and hasattr(self._serial, "flush"):
            self._serial.flush()
        return bytes_written


class FakeSerialPort:
    def __init__(
        self,
        rx_lines: Iterable[str],
        rx_bytes: Iterable[bytes] = (),
        line_ending: str = "\n",
        encoding: str = "utf-8",
    ):
        self.rx_lines: List[str] = list(rx_lines)
        self.rx_bytes: List[bytes] = list(rx_bytes)
        self.tx_lines: List[str] = []
        self.tx_bytes: List[bytes] = []
        self.tx_payloads: List[bytes] = []
        self.line_ending = _normalize_line_ending(line_ending)
        self.encoding = encoding
        self.last_tx_payload: Optional[bytes] = None

    def write_line(self, text: str) -> int:
        self.tx_lines.append(text)
        payload = f"{text}{self.line_ending}".encode(self.encoding)
        self.tx_payloads.append(payload)
        self.last_tx_payload = payload
        return len(payload)

    def read_line(self, timeout_ms: int) -> str:
        if not self.rx_lines:
            raise TimeoutError(f"Serial read timed out after {timeout_ms} ms.")
        return self.rx_lines.pop(0)

    def write_bytes(self, payload: bytes) -> int:
        data = bytes(payload)
        self.tx_bytes.append(data)
        self.last_tx_payload = data
        return len(data)

    def read_bytes(self, count: int, timeout_ms: int) -> bytes:
        if not self.rx_bytes:
            raise TimeoutError(f"Serial byte read timed out after {timeout_ms} ms.")

        chunk = self.rx_bytes.pop(0)
        if len(chunk) < count:
            raise TimeoutError(
                f"Serial byte read timed out after {timeout_ms} ms: expected {count} bytes, got {len(chunk)}."
            )
        if len(chunk) > count:
            self.rx_bytes.insert(0, chunk[count:])
        return chunk[:count]


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
        if command_type == "serial.write_bytes":
            return self._execute_write_bytes(args, context)
        if command_type == "serial.read_bytes":
            return self._execute_read_bytes(args, context)
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
        tx_hex = _last_tx_payload_hex(port)
        values = {
            "port": port_name,
            "tx": text,
            "bytes_written": bytes_written,
        }
        evidence = f"TX {text}\n"
        if tx_hex is not None:
            values["tx_hex"] = tx_hex
            evidence += f"TX_HEX {tx_hex}\n"
        evidence_ref = self._append_evidence(context, evidence)
        return AdapterResult(
            success=True,
            status="passed",
            message=f"Wrote to serial port '{port_name}'.",
            values=values,
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
        match_pattern = args.get("match")
        if match_pattern is not None:
            try:
                matched = re.search(str(match_pattern), text)
            except re.error as exc:
                return AdapterResult(
                    success=False,
                    status="failed",
                    message=f"Serial response regex match is invalid: {exc}.",
                    values={
                        "port": port_name,
                        "text": text,
                        "timeout_ms": timeout_ms,
                        "match": str(match_pattern),
                    },
                )
            if matched is None:
                return AdapterResult(
                    success=False,
                    status="failed",
                    message=f"Serial response did not match regex '{match_pattern}'.",
                    values={
                        "port": port_name,
                        "text": text,
                        "timeout_ms": timeout_ms,
                        "match": str(match_pattern),
                    },
                )
        evidence_ref = self._append_evidence(context, f"RX {text}\n")
        values = {
            "port": port_name,
            "text": text,
            "timeout_ms": timeout_ms,
        }
        if match_pattern is not None:
            values["match"] = str(match_pattern)
        return AdapterResult(
            success=True,
            status="passed",
            message=f"Read from serial port '{port_name}'.",
            values=values,
            raw_evidence_ref=evidence_ref,
        )

    def _execute_write_bytes(self, args: Dict[str, object], context: AdapterContext) -> AdapterResult:
        port_name = _required_text(args, "port")
        payload_hex = _required_text(args, "payload_hex").replace(" ", "")
        payload = bytes.fromhex(payload_hex)
        port = self._get_port(port_name)
        bytes_written = port.write_bytes(payload)
        normalized_hex = payload.hex().upper()
        evidence_ref = self._append_evidence(context, f"TX_HEX {normalized_hex}\n")
        return AdapterResult(
            success=True,
            status="passed",
            message=f"Wrote bytes to serial port '{port_name}'.",
            values={
                "port": port_name,
                "payload_hex": normalized_hex,
                "bytes_written": bytes_written,
            },
            raw_evidence_ref=evidence_ref,
        )

    def _execute_read_bytes(self, args: Dict[str, object], context: AdapterContext) -> AdapterResult:
        port_name = _required_text(args, "port")
        count = int(args.get("count", 0))
        if count <= 0:
            raise KeyError("Serial read_bytes requires a positive 'count'.")
        timeout_ms = int(args.get("timeout_ms", 1000))
        port = self._get_port(port_name)
        data = port.read_bytes(count, timeout_ms)
        data_hex = data.hex().upper()
        evidence_ref = self._append_evidence(context, f"RX_HEX {data_hex}\n")
        return AdapterResult(
            success=True,
            status="passed",
            message=f"Read bytes from serial port '{port_name}'.",
            values={
                "port": port_name,
                "data_hex": data_hex,
                "count": len(data),
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


def _pyserial_parity(value: str) -> str:
    normalized = str(value).strip().lower()
    aliases = {
        "none": "N",
        "n": "N",
        "even": "E",
        "e": "E",
        "odd": "O",
        "o": "O",
        "mark": "M",
        "m": "M",
        "space": "S",
        "s": "S",
    }
    if normalized not in aliases:
        raise ValueError(
            "Serial parity must be one of none, even, odd, mark, or space."
        )
    return aliases[normalized]


def _normalize_line_ending(value: str) -> str:
    raw = str(value)
    aliases = {
        "": "",
        "\n": "\n",
        "\r\n": "\r\n",
        "\r": "\r",
        "\\n": "\n",
        "\\r\\n": "\r\n",
        "\\r": "\r",
        "lf": "\n",
        "crlf": "\r\n",
        "cr": "\r",
        "none": "",
        "no": "",
    }
    if raw in aliases:
        return aliases[raw]
    normalized = raw.strip().lower()
    if normalized in aliases:
        return aliases[normalized]
    return raw


def _last_tx_payload_hex(port: SerialPort) -> Optional[str]:
    payload = getattr(port, "last_tx_payload", None)
    if isinstance(payload, (bytes, bytearray)):
        return bytes(payload).hex().upper()
    return None


def _safe_segment(value: str) -> str:
    safe = re.sub(r"[^A-Za-z0-9_.-]+", "_", value).strip("._")
    return safe or "unnamed"
