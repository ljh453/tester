from embsw_tester.adapters import AdapterContext, AdapterResult
from embsw_tester.adapters.trace32 import FakeTrace32Transport, Trace32Adapter


def test_trace32_adapter_uses_rcl_by_default():
    rcl = FakeTrace32Transport(
        name="rcl",
        result=AdapterResult(
            success=True,
            status="passed",
            message="rcl ok",
            values={"value": "VERSION OK"},
        ),
    )
    udp = FakeTrace32Transport(
        name="udp",
        result=AdapterResult(
            success=True,
            status="passed",
            message="udp ok",
            values={"value": "UDP OK"},
        ),
    )
    adapter = Trace32Adapter(rcl_transport=rcl, udp_transport=udp)

    result = adapter.execute(
        "trace32.command",
        {"command": "VERSION()"},
        AdapterContext(run_id="trace32-run", testcase="case", phase="steps"),
    )

    assert result.success is True
    assert result.values["command"] == "VERSION()"
    assert result.values["transport"] == "rcl"
    assert result.values["fallback_used"] is False
    assert result.values["value"] == "VERSION OK"
    assert rcl.commands == [("VERSION()", 1000)]
    assert udp.commands == []


def test_trace32_adapter_falls_back_to_udp_when_rcl_fails():
    rcl = FakeTrace32Transport(
        name="rcl",
        result=AdapterResult(
            success=False,
            status="failed",
            message="rcl unavailable",
        ),
    )
    udp = FakeTrace32Transport(
        name="udp",
        result=AdapterResult(
            success=True,
            status="passed",
            message="udp ok",
            values={"value": "STATE:HALTED"},
        ),
    )
    adapter = Trace32Adapter(rcl_transport=rcl, udp_transport=udp)

    result = adapter.execute(
        "trace32.command",
        {"command": "STATE()", "timeout_ms": 2500},
        AdapterContext(run_id="trace32-run", testcase="case", phase="steps"),
    )

    assert result.success is True
    assert result.values["transport"] == "udp"
    assert result.values["fallback_used"] is True
    assert result.values["value"] == "STATE:HALTED"
    assert result.values["attempts"] == [
        {"transport": "rcl", "success": False, "status": "failed", "message": "rcl unavailable"},
        {"transport": "udp", "success": True, "status": "passed", "message": "udp ok"},
    ]
    assert rcl.commands == [("STATE()", 2500)]
    assert udp.commands == [("STATE()", 2500)]


def test_trace32_adapter_runs_command_sequence_with_shared_fallback_policy():
    rcl = FakeTrace32Transport(
        name="rcl",
        result=AdapterResult(
            success=True,
            status="passed",
            message="rcl ok",
            values={"value": "OK"},
        ),
    )
    udp = FakeTrace32Transport(
        name="udp",
        result=AdapterResult(
            success=True,
            status="passed",
            message="udp ok",
            values={"value": "UDP OK"},
        ),
    )
    adapter = Trace32Adapter(rcl_transport=rcl, udp_transport=udp)

    result = adapter.execute(
        "trace32.command_sequence",
        {"commands": ["SYStem.Up", "Data.List D:0x1000++0x10"], "timeout_ms": 2000},
        AdapterContext(run_id="trace32-run", testcase="case", phase="steps"),
    )

    assert result.success is True
    assert result.values["commands"] == ["SYStem.Up", "Data.List D:0x1000++0x10"]
    assert result.values["value"] == ["OK", "OK"]
    assert result.values["results"][0]["values"]["command"] == "SYStem.Up"
    assert rcl.commands == [
        ("SYStem.Up", 2000),
        ("Data.List D:0x1000++0x10", 2000),
    ]
    assert udp.commands == []
