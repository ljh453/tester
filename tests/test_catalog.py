from embsw_tester.dsl.catalog import COMMAND_SPECS


def test_inca_commands_allow_timeout_ms_in_catalog():
    for command_type in (
        "inca.measure.read",
        "inca.calibration.set",
        "inca.recording.start",
        "inca.recording.stop",
    ):
        assert "timeout_ms" in COMMAND_SPECS[command_type].optional_args
