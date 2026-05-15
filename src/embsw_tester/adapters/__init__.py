"""External tool adapter framework."""

from embsw_tester.adapters.base import Adapter, AdapterContext, AdapterResult
from embsw_tester.adapters.mock import MockAdapter
from embsw_tester.adapters.registry import AdapterRegistry, create_default_adapter_registry
from embsw_tester.adapters.serial import FakeSerialPort, PySerialPort, SerialAdapter, SerialPort, SerialPortSettings
from embsw_tester.adapters.serial_factory import (
    create_adapter_registry_from_tool_profile,
    create_serial_adapter_from_profile,
)

__all__ = [
    "Adapter",
    "AdapterContext",
    "AdapterRegistry",
    "AdapterResult",
    "MockAdapter",
    "FakeSerialPort",
    "PySerialPort",
    "SerialAdapter",
    "SerialPort",
    "SerialPortSettings",
    "create_default_adapter_registry",
    "create_adapter_registry_from_tool_profile",
    "create_serial_adapter_from_profile",
]
