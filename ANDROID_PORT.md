# HexManiac Mobile — Android port

## 0.2.0: hexadecimal workspace

The Android app reuses the original `HexManiac.Core` through native Android controls.
This milestone adds an 8-column hex grid, byte selection by touch, 128-byte pages,
hexadecimal goto, and undo/redo through `ChangeHistory<ModelDelta>`.
An unchanged byte does not invalidate redo. Undo/redo returns selection to the edited address.

The interface includes a dark workspace, selected-byte highlight, ROM name/code/size,
and an unsaved-change indicator. Opening another ROM or pressing Back asks before
discarding edits. Editing is disabled while selecting/loading/saving documents.
Rotation retains the current Activity. Android process termination can still lose
unsaved edits; persistent session recovery is a later milestone.

ROM files are opened and exported with Android's document picker. Input is bounded
to 32 MiB (standard GBA ROM address space); larger expanded files are not yet supported.
Raw hexadecimal editing is low-level: bounds and history do not guarantee game validity.
Export success is reported only after the output stream has flushed and closed.

## Build and validation

The `Android APK` workflow installs .NET 10, runs the UI-independent mobile editor
checks against the original Core, installs the Android workload and builds a
self-contained debug APK. Download `HexManiac-Mobile-debug` from the workflow.
The project remains `net10.0-android`, referencing the original `net6.0` Core.

The synthetic-ROM checks cover byte-exact changes, undo/redo, saved state,
divergent edits, unchanged writes, first/last addresses, partial pages and invalid input.
No commercial ROM data is included.

Device acceptance:
1. Open a copy of a ROM; select a byte and navigate to the first and last pages.
2. Change one byte, undo and redo; confirm the unsaved indicator.
3. Export and compare the copy: only the intended byte should differ.
4. Cancel the picker, rotate, open the keyboard and try Back with unsaved edits.
5. Confirm file errors preserve the existing session and undo history.

## Porting sequence and original code

The long-term goal is feature parity with desktop, including map editing. This
milestone is the editing foundation, not completion of the full port.

| Stage | Work | Existing source to reuse |
| --- | --- | --- |
| 1 — current | Hex selection, goto, byte edits, undo/redo, export | Models/ModelDelta.cs; ViewModels/ChangeHistory.cs |
| 2 | Package/load reference files and metadata on Android; anchor navigation and search; session recovery | Models/PokemonModel.cs; Models/Code; ViewModels/ViewPort.cs |
| 3 | Structured tables: Pokémon, moves, items, trainers | ViewModels/Tools/TableTool.cs and array element view models |
| 4 | Game text editor with resizing/repointing and metadata persistence | ViewModels/Tools/PCSTool.cs; Models/Runs/PCSRun.cs |
| 5 | Sprites, palettes and tilemaps with import/export | ViewModels/Tools/SpriteTool.cs; PaletteCollection.cs |
| 6 | Event scripts, compilation and cross-navigation | ViewModels/Tools/CodeTool.cs; CodeBody.cs; Models/Code |
| 7 | Map browser/rendering, zoom/pan, blocks, collisions, events, warps and connections | Models/Map/MapModel.cs; ViewModels/Map/MapEditorViewModel.cs; BlockMapViewModel.cs; BlockEditor.cs |
| 8 | Advanced tools, patch support, parity audit and device validation | Remaining Core tools and desktop commands |

Stage 2 must verify the reference assets are actually available inside the installed
APK before exposing structured editors. Referencing the Core project alone is not
proof that all desktop metadata and filesystem assumptions work on Android.

Retain the desktop organization (data view, navigation and tool panels), adapting
panels for small screens and touch. The native Android UI is the current implementation;
no migration to Avalonia is included. Each stage must validate export and undo before
enabling the next structured editor.

The upstream MIT license is preserved in `LICENSE`. Keep ROMs outside this repository.
