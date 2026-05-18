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


def test_properties_pane_can_show_optional_arguments_on_demand():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    checkbox = next(
        (
            element
            for element in root.iter(f"{PRESENTATION}CheckBox")
            if element.attrib.get(f"{XAML}Name") == "ShowOptionalArgumentsCheckBox"
        ),
        None,
    )

    assert checkbox is not None
    assert checkbox.attrib["Click"] == "ShowOptionalArgumentsCheckBox_Click"


def test_settings_window_exposes_command_defaults_grid():
    root = _load("apps/TesterWorkbench/TesterWorkbench/SettingsWindow.xaml")

    grid = next(
        (
            element
            for element in root.iter(f"{PRESENTATION}DataGrid")
            if element.attrib.get(f"{XAML}Name") == "CommandDefaultsGrid"
        ),
        None,
    )

    assert grid is not None


def test_gui_command_tree_suppresses_default_tree_selection_chrome():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    tree_view = next(
        (
            element
            for element in root.iter(f"{PRESENTATION}TreeView")
            if element.attrib.get("SelectedItemChanged") == "GuiBlockTree_SelectedItemChanged"
        ),
        None,
    )
    assert tree_view is not None

    item_style = tree_view.find(f"{PRESENTATION}TreeView.ItemContainerStyle/{PRESENTATION}Style")
    assert item_style is not None

    template_setter = next(
        (
            setter
            for setter in item_style.findall(f"{PRESENTATION}Setter")
            if setter.attrib.get("Property") == "Template"
        ),
        None,
    )
    assert template_setter is not None

    selected_triggers = [
        trigger
        for trigger in item_style.iter(f"{PRESENTATION}Trigger")
        if trigger.attrib.get("Property") == "IsSelected"
    ]
    assert selected_triggers == []


def test_gui_command_block_header_wraps_long_command_text():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    display_type = next(
        (
            element
            for element in root.iter(f"{PRESENTATION}TextBlock")
            if element.attrib.get("Text") == "{Binding DisplayType}"
        ),
        None,
    )
    assert display_type is not None
    assert display_type.attrib["TextWrapping"] == "Wrap"
    assert int(display_type.attrib["MaxWidth"]) >= 150

    summary = next(
        (
            element
            for element in root.iter(f"{PRESENTATION}TextBlock")
            if element.attrib.get("Text") == "{Binding Summary}"
        ),
        None,
    )
    assert summary is not None
    assert summary.attrib["TextWrapping"] == "Wrap"
    assert int(summary.attrib["MinWidth"]) >= 120

    fixed_command_columns = [
        column
        for column in root.iter(f"{PRESENTATION}ColumnDefinition")
        if column.attrib.get("Width") == "82"
    ]
    assert fixed_command_columns == []
