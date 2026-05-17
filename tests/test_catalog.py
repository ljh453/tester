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
