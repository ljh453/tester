"""External tool adapter framework."""

from embsw_tester.adapters.base import Adapter, AdapterContext, AdapterResult
from embsw_tester.adapters.canoe import CanoeAdapter
from embsw_tester.adapters.inca import IncaAdapter
from embsw_tester.adapters.inca_bridge import (
    IncaBridgeRequest,
    IncaBridgeResponse,
    IncaBridgeTransport,
    JsonLineIncaBridgeTransport,
    create_inca_bridge_process_transport,
)
from embsw_tester.adapters.inca_factory import create_inca_adapter_from_profile
from embsw_tester.adapters.mock import MockAdapter
from embsw_tester.adapters.registry import AdapterRegistry, create_default_adapter_registry
from embsw_tester.adapters.serial import FakeSerialPort, PySerialPort, SerialAdapter, SerialPort, SerialPortSettings
from embsw_tester.adapters.serial_factory import (
    create_adapter_registry_from_tool_profile,
    create_serial_adapter_from_profile,
)
from embsw_tester.adapters.trace32_factory import create_trace32_adapter_from_profile
from embsw_tester.adapters.trace32 import (
    FakeTrace32Transport,
    RclTrace32Transport,
    Trace32Adapter,
    Trace32CommandTransport,
    UdpTrace32Transport,
)

__all__ = [
    "Adapter",
    "AdapterContext",
    "AdapterRegistry",
    "AdapterResult",
    "CanoeAdapter",
    "IncaAdapter",
    "IncaBridgeRequest",
    "IncaBridgeResponse",
    "IncaBridgeTransport",
    "JsonLineIncaBridgeTransport",
    "MockAdapter",
    "FakeSerialPort",
    "PySerialPort",
    "SerialAdapter",
    "SerialPort",
    "SerialPortSettings",
    "Trace32Adapter",
    "Trace32CommandTransport",
    "FakeTrace32Transport",
    "RclTrace32Transport",
    "UdpTrace32Transport",
    "create_default_adapter_registry",
    "create_adapter_registry_from_tool_profile",
    "create_inca_bridge_process_transport",
    "create_inca_adapter_from_profile",
    "create_serial_adapter_from_profile",
    "create_trace32_adapter_from_profile",
]
