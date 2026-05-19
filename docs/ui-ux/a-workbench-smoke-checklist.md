# A-Workbench UI/UX Smoke Checklist

## Workspace

- Open a workspace from File > Open.
- Confirm the current YAML file is marked in Project/Test Explorer.
- Confirm referenced YAML files are visually different from the current YAML file.
- Confirm the context strip shows current YAML, run target, save state, run state, and line state.

## Editing

- Select a YAML command line and confirm the matching GUI block is the active selection.
- Select a GUI block and confirm the YAML line moves or highlights without creating a second active selection.
- Edit a scalar property and save.
- Confirm the save state changes from Unsaved to Saved.

## Running

- Run `samples/workbench-demo.yaml`.
- Confirm the current command is highlighted in YAML and GUI.
- Confirm delay rows remain visually subtle.
- Confirm Variables, Console, and Execution Trace update during the run.

## Trace Inspector

- While events are streaming, confirm Follow latest moves to the newest event.
- Select an old trace row and confirm details stay pinned.
- Confirm tooltips explain each trace column and detail panel.

## Theme

- Test System, Light, and Dark.
- Confirm selected tabs, dropdowns, toolbar buttons, context chips, and pane headers remain readable.
