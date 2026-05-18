from embsw_tester.dsl.catalog import COMMAND_SPECS


def test_inca_commands_allow_timeout_ms_in_catalog():
    for command_type in (
        "inca.measure.read",
        "inca.calibration.set",
        "inca.recording.start",
        "inca.recording.stop",
    ):
        assert "timeout_ms" in COMMAND_SPECS[command_type].optional_args


def test_inca_commands_allow_com_helper_arguments_in_catalog():
    assert {"device", "acquisition_rate"} <= COMMAND_SPECS[
        "inca.measure.read"
    ].optional_args
    assert {"device", "value_kind"} <= COMMAND_SPECS[
        "inca.calibration.set"
    ].optional_args
    assert {"file_format", "format"} <= COMMAND_SPECS[
        "inca.recording.start"
    ].optional_args
    assert {"file_name", "file_format", "format", "discard"} <= COMMAND_SPECS[
        "inca.recording.stop"
    ].optional_args


def test_canoe_commands_allow_com_helper_arguments_in_catalog():
    assert {"configuration", "timeout_ms"} <= COMMAND_SPECS[
        "canoe.measurement.start"
    ].optional_args
    assert "timeout_ms" in COMMAND_SPECS["canoe.measurement.stop"].optional_args
    assert {"bus", "channel", "message", "save_as", "timeout_ms"} <= COMMAND_SPECS[
        "canoe.signal.read"
    ].optional_args
    assert {"save_as", "timeout_ms"} <= COMMAND_SPECS[
        "canoe.sysvar.read"
    ].optional_args
    assert "timeout_ms" in COMMAND_SPECS["canoe.sysvar.set"].optional_args


def test_sent_usb_command_allows_slow_buffer_arguments_in_catalog():
    assert {
        "buffer_index",
        "enabled",
        "buffer_enabled",
        "slow_buffer_index",
    } <= COMMAND_SPECS["sent_usb.command"].optional_args


def test_trace32_command_sequence_is_available_in_catalog():
    spec = COMMAND_SPECS["trace32.command_sequence"]

    assert spec.required_args == frozenset({"commands"})
    assert {"timeout_ms", "transport", "fallback", "save_as"} <= spec.optional_args
    assert spec.adapter == "trace32"
