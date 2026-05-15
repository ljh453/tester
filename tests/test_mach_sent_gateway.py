import pytest

from embsw_tester.devices.mach_sent_gateway import (
    MachSentGatewayError,
    encode_gateway_frame,
    parse_gateway_frame,
    parse_sent_fast_frame,
)


def test_encode_gateway_frame_matches_read_serial_number_example():
    assert encode_gateway_frame(0x5A).hex().upper() == "02015A5B03"


def test_parse_gateway_frame_matches_serial_number_response_example():
    frame = parse_gateway_frame(bytes.fromhex("02055AFFFFFFFE5A03"))

    assert frame.message_id == 0x5A
    assert frame.data.hex().upper() == "FFFFFFFE"
    assert frame.checksum == 0x5A


def test_parse_gateway_frame_rejects_bad_checksum():
    with pytest.raises(MachSentGatewayError, match="checksum"):
        parse_gateway_frame(bytes.fromhex("02015A0003"))


def test_parse_sent_fast_frame_extracts_channel_1_nibbles_and_crc():
    frame = parse_gateway_frame(
        encode_gateway_frame(100, bytes.fromhex("63214365BA"))
    )

    parsed = parse_sent_fast_frame(frame)

    assert parsed["channel"] == 1
    assert parsed["message_id"] == 100
    assert parsed["status_nibble"] == 3
    assert parsed["data_nibble_count"] == 6
    assert parsed["data_nibbles"] == [1, 2, 3, 4, 5, 6]
    assert parsed["crc"] == 10
    assert parsed["crc_calculated"] == 11
