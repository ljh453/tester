import io
import json

from embsw_tester.adapters.canoe_bridge import CanoeBridgeRequest
from embsw_tester.adapters.canoe_com_helper import CanoeComClient, serve


class FakeMeasurement:
    def __init__(self):
        self.running = False
        self.start_count = 0
        self.stop_count = 0

    def Start(self):
        self.running = True
        self.start_count += 1

    def Stop(self):
        self.running = False
        self.stop_count += 1

    @property
    def Running(self):
        return self.running


class FakeVariable:
    def __init__(self, value=None):
        self.Value = value


class FakeVariables:
    def __init__(self):
        self.items = {}

    def __call__(self, name):
        return self.items.setdefault(name, FakeVariable())


class FakeNamespaces:
    def __init__(self):
        self.items = {}

    def __call__(self, name):
        return self.items.setdefault(name, type("Namespace", (), {"Variables": FakeVariables()})())


class FakeSystem:
    def __init__(self):
        self.Namespaces = FakeNamespaces()


class FakeSignal:
    def __init__(self, value):
        self.Value = value


class FakeBus:
    def __init__(self):
        self.signal_calls = []

    def GetSignal(self, channel, message, signal):
        self.signal_calls.append((channel, message, signal))
        return FakeSignal(88.5)


class FakeApplication:
    def __init__(self):
        self.Measurement = FakeMeasurement()
        self.System = FakeSystem()
        self.buses = {}
        self.opened_configurations = []

    def Open(self, configuration):
        self.opened_configurations.append(configuration)

    def Bus(self, bus_type):
        return self.buses.setdefault(bus_type, FakeBus())


def test_canoe_com_client_starts_measurement_after_opening_configuration():
    application = FakeApplication()
    client = CanoeComClient(dispatch_factory=lambda prog_id: application)

    response = client.execute(
        CanoeBridgeRequest(
            request_id="req-1",
            command_type="canoe.measurement.start",
            args={"configuration": "C:/cfg/demo.cfg"},
        )
    )

    assert response.success is True
    assert application.opened_configurations == ["C:/cfg/demo.cfg"]
    assert application.Measurement.start_count == 1
    assert response.values["measurement_running"] is True
    assert response.values["configuration"] == "C:/cfg/demo.cfg"


def test_canoe_com_client_uses_canalyzer_prog_id_when_requested():
    application = FakeApplication()
    prog_ids = []

    def dispatch_factory(prog_id):
        prog_ids.append(prog_id)
        return application

    client = CanoeComClient(
        dispatch_factory=dispatch_factory,
        application_name="canalyzer",
    )

    response = client.execute(
        CanoeBridgeRequest(
            request_id="req-1",
            command_type="canoe.measurement.stop",
            args={},
        )
    )

    assert response.success is True
    assert prog_ids == ["CANalyzer.Application"]
    assert application.Measurement.stop_count == 1


def test_canoe_com_client_sets_and_reads_system_variable():
    application = FakeApplication()
    client = CanoeComClient(dispatch_factory=lambda prog_id: application)

    set_response = client.execute(
        CanoeBridgeRequest(
            request_id="set",
            command_type="canoe.sysvar.set",
            args={"namespace": "Vehicle", "name": "Ignition", "value": True},
        )
    )
    read_response = client.execute(
        CanoeBridgeRequest(
            request_id="read",
            command_type="canoe.sysvar.read",
            args={"namespace": "Vehicle", "name": "Ignition"},
        )
    )

    assert set_response.success is True
    assert set_response.values["value"] is True
    assert read_response.success is True
    assert read_response.values["value"] is True


def test_canoe_com_client_reads_signal_via_application_bus():
    application = FakeApplication()
    client = CanoeComClient(dispatch_factory=lambda prog_id: application)

    response = client.execute(
        CanoeBridgeRequest(
            request_id="req-1",
            command_type="canoe.signal.read",
            args={
                "bus": "CAN",
                "channel": 1,
                "message": "ABSData",
                "signal": "CarSpeed",
            },
        )
    )

    assert response.success is True
    assert response.values["bus"] == "CAN"
    assert response.values["channel"] == 1
    assert response.values["message"] == "ABSData"
    assert response.values["signal"] == "CarSpeed"
    assert response.values["value"] == 88.5
    assert application.buses["CAN"].signal_calls == [(1, "ABSData", "CarSpeed")]


def test_canoe_com_helper_serves_json_line_requests():
    application = FakeApplication()
    client = CanoeComClient(dispatch_factory=lambda prog_id: application)
    stdin = io.StringIO(
        json.dumps(
            {
                "request_id": "req-1",
                "command_type": "canoe.measurement.start",
                "args": {},
            }
        )
        + "\n"
    )
    stdout = io.StringIO()

    serve(stdin=stdin, stdout=stdout, client=client)

    response = json.loads(stdout.getvalue())
    assert response["request_id"] == "req-1"
    assert response["success"] is True
    assert response["status"] == "passed"
    assert response["values"]["measurement_running"] is True
