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


def test_gui_argument_combo_opens_autocomplete_while_typing():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    combo = next(
        element
        for element in root.iter(f"{PRESENTATION}ComboBox")
        if element.attrib.get("LostFocus") == "GuiCommandArgumentComboBox_LostFocus"
    )

    assert combo.attrib["GotKeyboardFocus"] == "GuiCommandArgumentComboBox_GotKeyboardFocus"
    assert combo.attrib["PreviewTextInput"] == "GuiCommandArgumentComboBox_PreviewTextInput"
    assert combo.attrib["KeyUp"] == "GuiCommandArgumentComboBox_KeyUp"
    assert combo.attrib["StaysOpenOnEdit"] == "True"
    assert combo.attrib["IsTextSearchEnabled"] == "False"


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


def test_resume_toolbar_icon_is_distinct_from_run_icon():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    run_button = next(
        element
        for element in root.iter(f"{PRESENTATION}Button")
        if element.attrib.get("Click") == "Run_Click"
    )
    resume_button = next(
        element
        for element in root.iter(f"{PRESENTATION}Button")
        if element.attrib.get("Click") == "Resume_Click"
    )

    assert run_button.attrib["Content"] == "▶"
    assert resume_button.attrib["Content"] == "▌▶"
    assert resume_button.attrib["Content"] != run_button.attrib["Content"]


def test_file_open_is_available_from_menu_not_toolbar_ellipsis():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    file_menu = next(
        element
        for element in root.iter(f"{PRESENTATION}MenuItem")
        if element.attrib.get("Header") == "_File"
    )
    open_menu = next(
        (
            element
            for element in file_menu.iter(f"{PRESENTATION}MenuItem")
            if element.attrib.get("Click") == "OpenWorkspace_Click"
        ),
        None,
    )
    toolbar_ellipsis_open = [
        element
        for element in root.iter(f"{PRESENTATION}Button")
        if element.attrib.get("Click") == "OpenWorkspace_Click"
        and element.attrib.get("Content") == "..."
    ]

    assert open_menu is not None
    assert open_menu.attrib["Header"] == "_Open"
    assert open_menu.attrib["InputGestureText"] == "Ctrl+O"
    assert toolbar_ellipsis_open == []


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


def test_delay_gui_blocks_use_subtle_card_style():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    delay_trigger = next(
        (
            trigger
            for trigger in root.iter(f"{PRESENTATION}DataTrigger")
            if trigger.attrib.get("Binding") == "{Binding IsSubtleGuiBlock}"
        ),
        None,
    )

    assert delay_trigger is not None
    setters = {
        setter.attrib["Property"]: setter.attrib["Value"]
        for setter in delay_trigger.findall(f"{PRESENTATION}Setter")
    }
    assert setters["Background"] == "Transparent"
    assert setters["BorderThickness"] == "0,0,0,1"
    assert setters["Opacity"] == "0.72"


def test_gui_command_block_is_tagged_for_drag_selection_hit_testing():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    command_block_border = next(
        (
            element
            for element in root.iter(f"{PRESENTATION}Border")
            if element.attrib.get("MouseLeftButtonDown") == "GuiCommandBlock_MouseLeftButtonDown"
        ),
        None,
    )

    assert command_block_border is not None
    assert command_block_border.attrib["Tag"] == "GuiCommandBlock"


def test_gui_exposes_extended_testcase_run_selection_list():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    testcase_list = next(
        (
            element
            for element in root.iter(f"{PRESENTATION}ListBox")
            if element.attrib.get(f"{XAML}Name") == "GuiRunTestcaseListBox"
        ),
        None,
    )

    assert testcase_list is not None
    assert testcase_list.attrib["SelectionMode"] == "Extended"
    assert testcase_list.attrib["SelectionChanged"] == "GuiRunTestcaseListBox_SelectionChanged"


def test_execution_trace_columns_explain_their_meaning_with_tooltips():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    expected_headers = {
        "Testcase",
        "Phase",
        "Command",
        "Status",
        "Line",
        "Path",
        "Detail",
        "Evidence",
        "Duration",
        "Error",
    }
    header_tooltips = {
        element.attrib.get("Text"): element.attrib.get("ToolTip")
        for element in root.iter(f"{PRESENTATION}TextBlock")
        if element.attrib.get("Text") in expected_headers
    }

    assert set(header_tooltips) == expected_headers
    assert all(header_tooltips[header] for header in expected_headers)


def test_execution_trace_details_can_be_pinned_instead_of_following_fast_events():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    follow_checkbox = next(
        (
            element
            for element in root.iter(f"{PRESENTATION}CheckBox")
            if element.attrib.get(f"{XAML}Name") == "TraceFollowLatestCheckBox"
        ),
        None,
    )

    assert follow_checkbox is not None
    assert follow_checkbox.attrib["Click"] == "TraceFollowLatestCheckBox_Click"
    assert follow_checkbox.attrib["IsChecked"] == "True"
    assert "latest" in follow_checkbox.attrib["ToolTip"].lower()


def test_execution_trace_detail_regions_explain_resolved_runtime_data():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    detail_tooltips = {
        element.attrib.get(f"{XAML}Name"): element.attrib.get("ToolTip")
        for element in root.iter(f"{PRESENTATION}TextBox")
        if element.attrib.get(f"{XAML}Name")
        in {
            "TraceResolvedInputsBox",
            "TraceOutputsBox",
            "TraceEvidenceBox",
            "TraceErrorBox",
        }
    }

    assert detail_tooltips["TraceResolvedInputsBox"]
    assert detail_tooltips["TraceOutputsBox"]
    assert detail_tooltips["TraceEvidenceBox"]
    assert detail_tooltips["TraceErrorBox"]


def test_workbench_has_a_shell_context_strip():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    strip = next(
        (
            element
            for element in root.iter(f"{PRESENTATION}Border")
            if element.attrib.get(f"{XAML}Name") == "WorkbenchContextStrip"
        ),
        None,
    )
    text_names = {
        element.attrib.get(f"{XAML}Name")
        for element in root.iter(f"{PRESENTATION}TextBlock")
    }

    assert strip is not None
    assert {
        "ContextSelectedYamlText",
        "ContextRunTargetText",
        "ContextSaveStateText",
        "ContextRunStateText",
        "ContextCurrentLineText",
    } <= text_names


def test_toolbar_commands_are_grouped_by_intent():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    group_names = {
        element.attrib.get(f"{XAML}Name")
        for element in root.iter(f"{PRESENTATION}StackPanel")
    }

    assert {
        "FileToolbarGroup",
        "BuildToolbarGroup",
        "RunToolbarGroup",
        "ViewToolbarGroup",
    } <= group_names
