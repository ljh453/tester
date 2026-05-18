from pathlib import Path


MAIN_WINDOW_CODE = Path("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs")


def _method_body(source: str, method_name: str) -> str:
    signature_index = source.index(f" {method_name}(")
    open_brace = source.index("{", signature_index)
    depth = 0
    for index in range(open_brace, len(source)):
        char = source[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return source[open_brace + 1 : index]
    raise AssertionError(f"Could not find method body for {method_name}")


def test_block_click_does_not_start_drag_range_selection():
    source = MAIN_WINDOW_CODE.read_text(encoding="utf-8")
    body = _method_body(source, "GuiCommandBlock_MouseLeftButtonDown")

    assert "BeginGuiBulkSelection" not in body


def test_block_right_click_does_not_start_drag_range_selection():
    source = MAIN_WINDOW_CODE.read_text(encoding="utf-8")
    body = _method_body(source, "GuiCommandBlock_PreviewMouseRightButtonDown")

    assert "_isGuiBulkSelectingWithRightButton" not in body


def test_block_hover_range_selection_uses_left_button_only():
    source = MAIN_WINDOW_CODE.read_text(encoding="utf-8")
    body = _method_body(source, "GuiCommandBlock_MouseEnter")

    assert "_isGuiBulkSelectingWithRightButton" not in body
    assert "e.RightButton" not in body
