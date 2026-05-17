import io
import json

from embsw_tester.adapters.inca_bridge import IncaBridgeRequest
from embsw_tester.adapters.inca_com_helper import IncaComClient, serve


class FakeIncaApplication:
    def __init__(self, experiment):
        self.experiment = experiment
        self.suppressed = False

    def SuppressMessageBoxes(self):
        self.suppressed = True
        return True

    def GetOpenedExperiment(self):
        return self.experiment

    def APIVersion(self):
        return "7.2"


class FakeExperiment:
    def __init__(self):
        self.calls = []
        self.devices = {}
        self.measure_value = FakeMeasureValue(123.5)
        self.calibration_value = FakeCalibrationValue()

    def GetDevice(self, name):
        self.calls.append(("GetDevice", name))
        device = object()
        self.devices[name] = device
        return device

    def GetMeasureValue(self, variable):
        self.calls.append(("GetMeasureValue", variable))
        return self.measure_value

    def GetMeasureValueWithAcquisitionRateInDevice(self, variable, acquisition_rate, device):
        self.calls.append(
            (
                "GetMeasureValueWithAcquisitionRateInDevice",
                variable,
                acquisition_rate,
                device,
            )
        )
        return self.measure_value

    def GetCalibrationValue(self, parameter):
        self.calls.append(("GetCalibrationValue", parameter))
        return self.calibration_value

    def GetCalibrationValueInDevice(self, parameter, device):
        self.calls.append(("GetCalibrationValueInDevice", parameter, device))
        return self.calibration_value

    def SetRecordingFileName(self, name):
        self.calls.append(("SetRecordingFileName", name))
        return True

    def SetRecordingPathName(self, output_dir):
        self.calls.append(("SetRecordingPathName", output_dir))
        return True

    def SetRecordingFileFormat(self, file_format):
        self.calls.append(("SetRecordingFileFormat", file_format))
        return True

    def StartRecording(self):
        self.calls.append(("StartRecording",))
        return True

    def StopRecording(self, file_name, file_format):
        self.calls.append(("StopRecording", file_name, file_format))
        return True

    def StopAndDiscardRecording(self):
        self.calls.append(("StopAndDiscardRecording",))
        return True


class FakeMeasureValue:
    def __init__(self, value):
        self.value = value

    def GetPhysType(self):
        return "double"

    def GetDoublePhysValue(self):
        return self.value


class FakeCalibrationValue:
    def __init__(self):
        self.calls = []

    def SetDoublePhysValue(self, value):
        self.calls.append(("SetDoublePhysValue", value))
        return True

    def SetIntegerImplValue(self, value):
        self.calls.append(("SetIntegerImplValue", value))
        return True


def test_inca_com_client_reads_measurement_from_open_experiment():
    experiment = FakeExperiment()
    app = FakeIncaApplication(experiment)
    client = IncaComClient(dispatch_factory=lambda prog_id: app)

    response = client.execute(
        IncaBridgeRequest(
            request_id="req-1",
            command_type="inca.measure.read",
            args={"variable": "EngineSpeed"},
        )
    )

    assert app.suppressed is True
    assert experiment.calls == [("GetMeasureValue", "EngineSpeed")]
    assert response.success is True
    assert response.status == "passed"
    assert response.values == {
        "api_version": "7.2",
        "variable": "EngineSpeed",
        "value": 123.5,
    }


def test_inca_com_client_reads_measurement_with_device_and_acquisition_rate():
    experiment = FakeExperiment()
    app = FakeIncaApplication(experiment)
    client = IncaComClient(dispatch_factory=lambda prog_id: app)

    response = client.execute(
        IncaBridgeRequest(
            request_id="req-1",
            command_type="inca.measure.read",
            args={
                "variable": "EngineSpeed",
                "device": "ETKC",
                "acquisition_rate": "10ms",
            },
        )
    )

    device = experiment.devices["ETKC"]
    assert experiment.calls == [
        ("GetDevice", "ETKC"),
        (
            "GetMeasureValueWithAcquisitionRateInDevice",
            "EngineSpeed",
            "10ms",
            device,
        ),
    ]
    assert response.success is True
    assert response.values["device"] == "ETKC"
    assert response.values["acquisition_rate"] == "10ms"
    assert response.values["value"] == 123.5


def test_inca_com_client_sets_physical_calibration_value():
    experiment = FakeExperiment()
    app = FakeIncaApplication(experiment)
    client = IncaComClient(dispatch_factory=lambda prog_id: app)

    response = client.execute(
        IncaBridgeRequest(
            request_id="req-1",
            command_type="inca.calibration.set",
            args={"parameter": "IdleSpeedTarget", "value": 850.0},
        )
    )

    assert experiment.calls == [("GetCalibrationValue", "IdleSpeedTarget")]
    assert experiment.calibration_value.calls == [("SetDoublePhysValue", 850.0)]
    assert response.success is True
    assert response.values["parameter"] == "IdleSpeedTarget"
    assert response.values["value"] == 850.0
    assert response.values["value_kind"] == "phys"


def test_inca_com_client_sets_implementation_calibration_value_in_device():
    experiment = FakeExperiment()
    app = FakeIncaApplication(experiment)
    client = IncaComClient(dispatch_factory=lambda prog_id: app)

    response = client.execute(
        IncaBridgeRequest(
            request_id="req-1",
            command_type="inca.calibration.set",
            args={
                "parameter": "RawIdleSpeedTarget",
                "value": 850,
                "device": "ETKC",
                "value_kind": "impl",
            },
        )
    )

    device = experiment.devices["ETKC"]
    assert experiment.calls == [
        ("GetDevice", "ETKC"),
        ("GetCalibrationValueInDevice", "RawIdleSpeedTarget", device),
    ]
    assert experiment.calibration_value.calls == [("SetIntegerImplValue", 850)]
    assert response.success is True
    assert response.values["device"] == "ETKC"
    assert response.values["value_kind"] == "impl"


def test_inca_com_client_controls_recording_file_attributes():
    experiment = FakeExperiment()
    app = FakeIncaApplication(experiment)
    client = IncaComClient(dispatch_factory=lambda prog_id: app)

    start = client.execute(
        IncaBridgeRequest(
            request_id="req-start",
            command_type="inca.recording.start",
            args={
                "name": "boot",
                "output_dir": "C:/reports",
                "file_format": "MDF",
            },
        )
    )
    stop = client.execute(
        IncaBridgeRequest(
            request_id="req-stop",
            command_type="inca.recording.stop",
            args={"file_name": "boot-final", "file_format": "MDF4"},
        )
    )
    discard = client.execute(
        IncaBridgeRequest(
            request_id="req-discard",
            command_type="inca.recording.stop",
            args={"discard": True},
        )
    )

    assert experiment.calls == [
        ("SetRecordingFileName", "boot"),
        ("SetRecordingPathName", "C:/reports"),
        ("SetRecordingFileFormat", "MDF"),
        ("StartRecording",),
        ("StopRecording", "boot-final", "MDF4"),
        ("StopAndDiscardRecording",),
    ]
    assert start.success is True
    assert start.values["recording_active"] is True
    assert stop.success is True
    assert stop.values["recording_active"] is False
    assert discard.success is True
    assert discard.values["discarded"] is True


def test_inca_com_helper_serves_json_line_requests():
    experiment = FakeExperiment()
    app = FakeIncaApplication(experiment)
    client = IncaComClient(dispatch_factory=lambda prog_id: app)
    stdin = io.StringIO(
        json.dumps(
            {
                "request_id": "req-1",
                "command_type": "inca.measure.read",
                "args": {"variable": "EngineSpeed"},
                "timeout_ms": 1000,
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
    assert response["values"]["value"] == 123.5
