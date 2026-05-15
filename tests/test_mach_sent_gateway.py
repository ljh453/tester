import pytest

from embsw_tester.devices.mach_sent_gateway import (
    MachSentGatewayError,
    build_sent_channel_config,
    build_sent_fast_frame_payload,
    build_sent_gateway_command,
    build_sent_slow_frame_payload,
    encode_gateway_frame,
    parse_gateway_ack,
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


def test_build_sent_channel_config_for_tx_defaults():
    config = build_sent_channel_config(
        {
            "direction": "tx",
            "data_nibble_count": 6,
            "unit_time_us": 3.0,
        }
    )

    assert config.hex().upper() == "C8002C01000000"


def test_build_sent_fast_frame_payload_matches_data_frame_layout():
    payload = build_sent_fast_frame_payload(
        {
            "status": 3,
            "data_nibbles": [1, 2, 3, 4, 5, 6],
            "crc": 10,
            "crc_calculated": 11,
        }
    )

    assert payload.hex().upper() == "63214365BA"


def test_build_sent_slow_frame_payload_matches_data_frame_layout():
    payload = build_sent_slow_frame_payload(
        {
            "slow_message_id": 0x12,
            "data": 0x3456,
            "crc_received": 0x25,
            "slow_frame_type": "enhanced_serial",
            "enhanced_format": True,
            "crc_calculated": 0x2C,
        }
    )

    assert payload.hex().upper() == "125634E52C"


def test_build_sent_gateway_start_and_transmit_commands():
    assert build_sent_gateway_command("start", {"channel": 2}).hex().upper() == "02011F2003"
    assert build_sent_gateway_command(
        "transmit_fast",
        {
            "channel": 1,
            "status": 3,
            "data_nibbles": [1, 2, 3, 4, 5, 6],
            "crc": 10,
            "crc_calculated": 11,
        },
    ).hex().upper() == "02062963214365BA1503"
    assert build_sent_gateway_command(
        "transmit_slow",
        {
            "channel": 2,
            "slow_message_id": 0x12,
            "data": 0x3456,
            "crc_received": 0x25,
            "slow_frame_type": "enhanced_serial",
            "enhanced_format": True,
            "crc_calculated": 0x2C,
        },
    ).hex().upper() == "020634125634E52CE703"


def test_parse_gateway_ack_requires_ok_status_and_expected_message_id():
    frame = parse_gateway_frame(encode_gateway_frame(21, b"\x01"))

    assert parse_gateway_ack(frame, 21) is True

    with pytest.raises(MachSentGatewayError, match="ERR"):
        parse_gateway_ack(parse_gateway_frame(encode_gateway_frame(21, b"\x00")), 21)
