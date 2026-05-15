import pytest

from embsw_tester.devices.vupower_k import (
    VuPowerKError,
    format_vupower_command,
    parse_vupower_response,
)


def test_format_vupower_apply_command_uses_scpi_limit_syntax():
    command = format_vupower_command(
        "apply",
        {"channel": 1, "voltage": 12, "current": "1.234"},
    )

    assert command == "APPL P1,12.000,1.234"


def test_format_vupower_output_command_uses_output_status_syntax():
    assert format_vupower_command("output", {"channel": "P2", "state": True}) == (
        "OUTP:STAT P2,ON"
    )


def test_format_vupower_average_voltage_measurement_query():
    assert format_vupower_command("measure_voltage", {"channel": 1, "average": True}) == (
        "MEAS:VOLTA? P1"
    )


def test_parse_vupower_output_status_response_as_bool():
    assert parse_vupower_response("read_output", "1") is True
    assert parse_vupower_response("read_output", "0") is False


def test_parse_vupower_measurement_response_as_float():
    assert parse_vupower_response("measure_current", "1.23E-3") == pytest.approx(0.00123)


def test_format_vupower_command_rejects_unknown_action():
    with pytest.raises(VuPowerKError, match="Unsupported"):
        format_vupower_command("unknown", {})
