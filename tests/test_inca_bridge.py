from embsw_tester.adapters.inca_bridge import IncaBridgeRequest, IncaBridgeResponse


def test_inca_bridge_request_round_trips_as_dict():
    request = IncaBridgeRequest(
        request_id="req-1",
        command_type="inca.measure.read",
        args={"variable": "EngineSpeed"},
        timeout_ms=2500,
    )

    payload = request.to_dict()
    restored = IncaBridgeRequest.from_dict(payload)

    assert payload == {
        "request_id": "req-1",
        "command_type": "inca.measure.read",
        "args": {"variable": "EngineSpeed"},
        "timeout_ms": 2500,
    }
    assert restored == request


def test_inca_bridge_response_converts_to_adapter_result():
    response = IncaBridgeResponse(
        request_id="req-1",
        success=True,
        status="passed",
        message="read measurement",
        values={"variable": "EngineSpeed", "value": 900},
    )

    payload = response.to_dict()
    restored = IncaBridgeResponse.from_dict(payload)
    adapter_result = restored.to_adapter_result()

    assert restored == response
    assert adapter_result.success is True
    assert adapter_result.status == "passed"
    assert adapter_result.message == "read measurement"
    assert adapter_result.values == {"variable": "EngineSpeed", "value": 900}
