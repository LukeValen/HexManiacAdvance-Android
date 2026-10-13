using HexManiac.Mobile;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;

static void Check(bool condition, string message) {
   if (!condition) throw new Exception(message);
}

Directory.SetCurrentDirectory(AppContext.BaseDirectory);
var references = new Singletons();
Check(references.GameReferenceTables.ContainsKey("BPRE1"), "FireRed Rev 1 references");
Check(references.GameReferenceTables["BPRE1"].Any(t => t.Name == HardcodeTablesModel.PokemonStatsTable), "Pokemon table reference");
Check(references.ScriptLines.Count > 0 && references.ThumbInstructionTemplates.Count > 0, "Code reference files");
Check(PCSString.PCS.Count > 0, "PCS reference initialized");
Console.WriteLine("PASS: packaged references, FireRed Rev 1, scripts and text converter.");

var original = Enumerable.Repeat((byte)0xFF, 513).ToArray();
var model = new PokemonModel((byte[])original.Clone());
await model.InitializationWorkload;
var editor = new HexEditorSession(model, "synthetic.gba");
Check(!editor.IsDirty && !editor.CanUndo && !editor.CanRedo, "Initial state");
Check(editor.GoTo(" 0x100 "), "Prefixed hex address");
Check(editor.Edit("42"), "Edit");
Check(model.RawData.Where((b, i) => b != original[i]).Count() == 1 && model[256] == 0x42, "Exactly one changed byte");
Check(editor.IsDirty && editor.CanUndo, "Dirty edit");
editor.Undo();
Check(model.RawData.SequenceEqual(original) && !editor.IsDirty && editor.CanRedo, "Undo restores exact ROM");
Check(editor.Edit("FF") && editor.CanRedo, "No-op preserves redo");
editor.Redo();
Check(model[256] == 0x42 && editor.IsDirty, "Redo restores edit");
editor.MarkSaved();
Check(!editor.IsDirty, "Saved marker");
editor.Undo();
Check(editor.IsDirty, "Undo after save is dirty");
editor.Redo();
Check(!editor.IsDirty, "Redo returns to save point");
editor.Undo();
Check(editor.Edit("43") && !editor.CanRedo && editor.IsDirty, "Divergent edit drops redo");
Check(editor.GoTo("200") && editor.PageStart == 512, "Last partial page");
Check(editor.Edit("00") && model[512] == 0, "Last byte editable");
editor.MovePage(1);
Check(editor.SelectedAddress == 512, "Cannot page beyond end");
editor.MovePage(-1);
Check(editor.SelectedAddress == 384, "Previous page");
Check(editor.GoTo("0"), "First byte");
editor.MovePage(-1);
Check(editor.SelectedAddress == 0, "Cannot page before start");
foreach (var invalid in new[] { "-1", "201", "0x", "100000000", "FFFFFFFF", "0x0x10", "xyz", "" })
   Check(!editor.GoTo(invalid) && editor.SelectedAddress == 0, "Reject address: " + invalid);
foreach (var invalid in new[] { "-1", "100", "GG", "" })
   Check(!editor.Edit(invalid), "Reject byte: " + invalid);
Check(model.Count == original.Length, "No accidental ROM expansion");
Console.WriteLine("PASS: byte-exact edits, undo/redo, saved state, bounds, navigation and invalid input.");

var tableBytes = new byte[1024];
var tableModel = new PokemonModel(tableBytes);
await tableModel.InitializationWorkload;
ArrayRun.TryParse(tableModel, "[hp. attack.]2", 512, null, out var stats);
Check(stats != null, "Parse stats fixture");
tableModel.ObserveAnchorWritten(new NoDataChangeDeltaModel(), HardcodeTablesModel.PokemonStatsTable, stats);
ArrayRun.TryParse(tableModel, "[price: index:]2", 544, null, out var items);
Check(items != null, "Parse item fixture");
tableModel.ObserveAnchorWritten(new NoDataChangeDeltaModel(), HardcodeTablesModel.ItemsTableName, items);
var tableEditor = new HexEditorSession(tableModel, "tables.gba");
Check(MobileTables.Catalog(tableModel).Contains(HardcodeTablesModel.PokemonStatsTable), "Table discovery");
Check(MobileTables.Find(tableModel, "missing.anchor") == null, "Missing anchor must not resolve to adjacent table");
Check(MobileTables.EditNumber(tableEditor, HardcodeTablesModel.PokemonStatsTable, 1, "hp", "100", out _), "Edit row 1 HP");
Check(tableBytes[514] == 100 && tableBytes.Where((b, i) => i != 514).All(b => b == 0), "Only selected field changed");
tableEditor.Undo();
Check(tableBytes.All(b => b == 0) && !tableEditor.IsDirty, "Table undo exact");
tableEditor.Redo();
Check(tableBytes[514] == 100, "Table redo");
Check(!MobileTables.EditNumber(tableEditor, HardcodeTablesModel.PokemonStatsTable, 0, "hp", "256", out _), "Reject byte overflow");
Check(!MobileTables.EditNumber(tableEditor, HardcodeTablesModel.PokemonStatsTable, -1, "hp", "1", out _), "Reject bad row");
Check(!MobileTables.EditNumber(tableEditor, HardcodeTablesModel.ItemsTableName, 0, "index", "1", out _), "Do not expose structural fields");
Check(MobileTables.EditNumber(tableEditor, HardcodeTablesModel.ItemsTableName, 0, "price", "500", out _), "Edit multi-byte price");
Check(tableBytes[544] == 244 && tableBytes[545] == 1 && tableBytes[546] == 0, "Little endian exact price");
tableEditor.Undo();
Check(tableBytes[544] == 0 && tableBytes[545] == 0, "Atomic multi-byte undo");
Check(!MobileTables.EditNumber(tableEditor, HardcodeTablesModel.ItemsTableName, 0, "price", "-1", out _) && tableEditor.CanRedo, "Invalid input retains redo");
Console.WriteLine("PASS: table discovery, row bounds, exact field edits, decimal validation, little endian and atomic undo/redo.");
