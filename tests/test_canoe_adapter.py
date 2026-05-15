from embsw_tester.adapters import AdapterContext
from embsw_tester.adapters.canoe import CanoeAdapter


def test_canoe_adapter_controls_measurement_and_system_variables():
    adapter = CanoeAdapter()
    context = AdapterContext(run_id="canoe-run", testcase="case", phase="steps")

    start_result = adapter.execute("canoe.measurement.start", {}, context)
    set_result = adapter.execute(
        "canoe.sysvar.set",
        {"namespace": "Vehicle", "name": "Ignition", "value": True},
        context,
    )
    read_result = adapter.execute(
        "canoe.sysvar.read",
        {"namespace": "Vehicle", "name": "Ignition"},
        context,
    )
    stop_result = adapter.execute("canoe.measurement.stop", {}, context)

    assert start_result.success is True
    assert start_result.values["measurement_running"] is True
    assert set_result.success is True
    assert set_result.values["value"] is True
    assert read_result.success is True
    assert read_result.values["value"] is True
    assert stop_result.success is True
    assert stop_result.values["measurement_running"] is False


def test_canoe_adapter_reads_configured_signal():
    adapter = CanoeAdapter(signals={"EngineSpeed": 850})

    result = adapter.execute(
        "canoe.signal.read",
        {"signal": "EngineSpeed"},
        AdapterContext(run_id="canoe-run", testcase="case", phase="steps"),
    )

    assert result.success is True
    assert result.values["signal"] == "EngineSpeed"
    assert result.values["value"] == 850
