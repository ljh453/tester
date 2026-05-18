from pathlib import Path
from xml.etree import ElementTree


MAIN_WINDOW_CODE = Path("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs")
MAIN_WINDOW_XAML = Path("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")
MAIN_VIEW_MODEL_CODE = Path(
    "apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs"
)
PRESENTATION = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"


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
    body = _method_body(source, "GuiCommandBlock_MouseRightButtonDown")

    assert "_isGuiBulkSelectingWithRightButton" not in body
    assert "e.Handled = true;" in body


def test_block_hover_range_selection_uses_left_button_only():
    source = MAIN_WINDOW_CODE.read_text(encoding="utf-8")
    body = _method_body(source, "GuiCommandBlock_MouseEnter")

    assert "_isGuiBulkSelectingWithRightButton" not in body
    assert "e.RightButton" not in body


def test_block_click_uses_bubbling_mouse_events_for_nested_blocks():
    root = ElementTree.parse(MAIN_WINDOW_XAML).getroot()
    block_border = next(
        element
        for element in root.iter(f"{PRESENTATION}Border")
        if element.attrib.get("MouseEnter") == "GuiCommandBlock_MouseEnter"
    )

    assert block_border.attrib["MouseLeftButtonDown"] == "GuiCommandBlock_MouseLeftButtonDown"
    assert block_border.attrib["MouseRightButtonDown"] == "GuiCommandBlock_MouseRightButtonDown"
    assert "PreviewMouseLeftButtonDown" not in block_border.attrib
    assert "PreviewMouseRightButtonDown" not in block_border.attrib


def test_yaml_line_selection_replaces_gui_block_selection_marker():
    source = MAIN_VIEW_MODEL_CODE.read_text(encoding="utf-8")
    body = _method_body(source, "SelectGuiCommandAtLine")

    assert "SelectGuiCommandForBulkAction(commandBlock, replaceSelection: true);" in body


def test_gui_block_click_moves_yaml_caret_to_same_block():
    source = MAIN_WINDOW_CODE.read_text(encoding="utf-8")
    body = _method_body(source, "GuiCommandBlock_MouseLeftButtonDown")

    assert "FocusYamlLine(commandBlock.SourceLineStart);" in body
