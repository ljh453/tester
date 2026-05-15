from embsw_tester.adapters.trace32 import RclTrace32Transport, UdpTrace32Transport


class FakeRclClient:
    def __init__(self, response):
        self.response = response
        self.commands = []

    def cmd(self, command):
        self.commands.append(command)
        return self.response


class FakeUdpSocket:
    def __init__(self, response=None, recv_error=None):
        self.response = response or b""
        self.recv_error = recv_error
        self.timeout = None
        self.connected_to = None
        self.sent_payloads = []
        self.closed = False

    def settimeout(self, timeout):
        self.timeout = timeout

    def connect(self, address):
        self.connected_to = address

    def sendall(self, payload):
        self.sent_payloads.append(payload)

    def recv(self, response_bytes):
        if self.recv_error is not None:
            raise self.recv_error
        return self.response

    def close(self):
        self.closed = True


def test_rcl_trace32_transport_wraps_client_command_method():
    client = FakeRclClient("VERSION OK")
    transport = RclTrace32Transport(client=client)

    result = transport.execute_command("VERSION()", timeout_ms=1000)

    assert result.success is True
    assert result.values["transport"] == "rcl"
    assert result.values["value"] == "VERSION OK"
    assert client.commands == ["VERSION()"]


def test_udp_trace32_transport_sends_command_and_returns_response():
    sockets = []

    def socket_factory(*args, **kwargs):
        socket = FakeUdpSocket(response=b"STATE:HALTED\n")
        sockets.append(socket)
        return socket

    transport = UdpTrace32Transport(
        host="127.0.0.1",
        port=20000,
        socket_factory=socket_factory,
    )

    result = transport.execute_command("STATE()", timeout_ms=2500)

    socket = sockets[0]
    assert result.success is True
    assert result.values["transport"] == "udp"
    assert result.values["value"] == "STATE:HALTED"
    assert socket.connected_to == ("127.0.0.1", 20000)
    assert socket.timeout == 2.5
    assert socket.sent_payloads == [b"STATE()\n"]
    assert socket.closed is True


def test_udp_trace32_transport_reports_socket_errors():
    def socket_factory(*args, **kwargs):
        return FakeUdpSocket(recv_error=TimeoutError("timed out"))

    transport = UdpTrace32Transport(
        host="127.0.0.1",
        port=20000,
        socket_factory=socket_factory,
    )

    result = transport.execute_command("STATE()", timeout_ms=2500)

    assert result.success is False
    assert result.status == "failed"
    assert "UDP" in result.message
    assert "timed out" in result.message


def test_udp_trace32_transport_reports_socket_creation_errors():
    def socket_factory(*args, **kwargs):
        raise OSError("socket unavailable")

    transport = UdpTrace32Transport(
        host="127.0.0.1",
        port=20000,
        socket_factory=socket_factory,
    )

    result = transport.execute_command("STATE()", timeout_ms=2500)

    assert result.success is False
    assert result.status == "failed"
    assert "UDP" in result.message
    assert "socket unavailable" in result.message
