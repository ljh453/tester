from embsw_tester.adapters import AdapterContext
from embsw_tester.adapters.inca import IncaAdapter


def test_inca_adapter_reads_measurement_and_sets_calibration():
    adapter = IncaAdapter(measurements={"EngineSpeed": 900})
    context = AdapterContext(run_id="inca-run", testcase="case", phase="steps")

    measure_result = adapter.execute(
        "inca.measure.read",
        {"variable": "EngineSpeed"},
        context,
    )
    calibration_result = adapter.execute(
        "inca.calibration.set",
        {"parameter": "IdleSpeedTarget", "value": 850},
        context,
    )

    assert measure_result.success is True
    assert measure_result.values["variable"] == "EngineSpeed"
    assert measure_result.values["value"] == 900
    assert calibration_result.success is True
    assert calibration_result.values["parameter"] == "IdleSpeedTarget"
    assert calibration_result.values["value"] == 850
    assert adapter.calibrations["IdleSpeedTarget"] == 850


def test_inca_adapter_controls_recording_state():
    adapter = IncaAdapter()
    context = AdapterContext(run_id="inca-run", testcase="case", phase="steps")

    start_result = adapter.execute(
        "inca.recording.start",
        {"name": "boot", "output_dir": "reports/boot"},
        context,
    )
    stop_result = adapter.execute("inca.recording.stop", {}, context)

    assert start_result.success is True
    assert start_result.values["recording_active"] is True
    assert start_result.values["recording_name"] == "boot"
    assert start_result.values["output_dir"] == "reports/boot"
    assert stop_result.success is True
    assert stop_result.values["recording_active"] is False
