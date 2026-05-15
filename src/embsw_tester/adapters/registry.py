from __future__ import annotations

from typing import Dict

from embsw_tester.adapters.base import Adapter
from embsw_tester.adapters.mock import MockAdapter


class AdapterRegistry:
    def __init__(self) -> None:
        self._adapters: Dict[str, Adapter] = {}

    def register(self, name: str, adapter: Adapter) -> None:
        self._adapters[name] = adapter

    def get(self, name: str) -> Adapter:
        try:
            return self._adapters[name]
        except KeyError as exc:
            raise KeyError(f"Adapter '{name}' is not registered.") from exc


def create_default_adapter_registry() -> AdapterRegistry:
    registry = AdapterRegistry()
    registry.register("serial", MockAdapter("serial"))
    registry.register("trace32", MockAdapter("trace32"))
    registry.register("canoe", MockAdapter("canoe"))
    registry.register("inca", MockAdapter("inca"))
    return registry
