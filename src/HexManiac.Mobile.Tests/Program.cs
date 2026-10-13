using HexManiac.Mobile;
using HavenSoft.HexManiac.Core.Models;

static void Check(bool condition, string message) {
   if (!condition) throw new Exception(message);
}

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
