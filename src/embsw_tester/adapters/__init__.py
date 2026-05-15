"""External tool adapter framework."""

from embsw_tester.adapters.base import Adapter, AdapterContext, AdapterResult
from embsw_tester.adapters.mock import MockAdapter
from embsw_tester.adapters.registry import AdapterRegistry, create_default_adapter_registry
from embsw_tester.adapters.serial import FakeSerialPort, SerialAdapter, SerialPort

__all__ = [
    "Adapter",
    "AdapterContext",
    "AdapterRegistry",
    "AdapterResult",
    "MockAdapter",
    "FakeSerialPort",
    "SerialAdapter",
    "SerialPort",
    "create_default_adapter_registry",
]
