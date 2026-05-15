"""Device-level command profile execution."""

__all__ = [
    "DeviceCommandError",
    "DeviceCommandExecution",
    "execute_device_command",
]


def __getattr__(name: str):
    if name in __all__:
        from embsw_tester.devices import command_profiles

        return getattr(command_profiles, name)
    raise AttributeError(name)
