from pathlib import Path
from xml.etree import ElementTree


PRESENTATION = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
XAML = "{http://schemas.microsoft.com/winfx/2006/xaml}"


def _load(path: str) -> ElementTree.Element:
    return ElementTree.parse(Path(path)).getroot()


def test_gui_argument_combo_applies_selection_immediately():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    combo = next(
        element
        for element in root.iter(f"{PRESENTATION}ComboBox")
        if element.attrib.get("LostFocus") == "GuiCommandArgumentComboBox_LostFocus"
    )

    assert combo.attrib["SelectionChanged"] == "GuiCommandArgumentComboBox_SelectionChanged"


def test_combo_box_template_supports_editable_text_display():
    root = _load("apps/TesterWorkbench/TesterWorkbench/Themes/BaseWorkbenchStyles.xaml")

    editable_text_box = next(
        (
            element
            for element in root.iter(f"{PRESENTATION}TextBox")
            if element.attrib.get(f"{XAML}Name") == "PART_EditableTextBox"
        ),
        None,
    )

    assert editable_text_box is not None
    assert editable_text_box.attrib["Visibility"] == "Hidden"
