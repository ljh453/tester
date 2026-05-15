from embsw_tester.adapters import AdapterContext, AdapterRegistry, MockAdapter


def test_mock_adapter_returns_structured_result():
    adapter = MockAdapter("serial")

    result = adapter.execute(
        "serial.write",
        {"port": "psu", "text": "OUT 1 ON"},
        AdapterContext(run_id="run-adapter", testcase="case", phase="steps"),
    )

    assert result.success is True
    assert result.status == "passed"
    assert result.values["mode"] == "mock"
    assert result.values["adapter"] == "serial"
    assert result.values["command_type"] == "serial.write"


def test_adapter_registry_resolves_registered_adapter():
    registry = AdapterRegistry()
    adapter = MockAdapter("serial")

    registry.register("serial", adapter)

    assert registry.get("serial") is adapter
