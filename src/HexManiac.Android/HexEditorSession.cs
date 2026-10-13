using System.Globalization;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;

namespace HexManiac.Mobile;

// UI-independent editing state; all writes participate in the original Core history.
public sealed class HexEditorSession {
   public const int Columns = 8;
   public const int PageSize = Columns * 16;
   private readonly ChangeHistory<ModelDelta> history;
   public PokemonModel Model { get; }
   public string Name { get; }
   public int SelectedAddress { get; private set; }
   public int PageStart => SelectedAddress / PageSize * PageSize;
   public bool IsDirty => !history.IsSaved;
   public bool CanUndo => history.Undo.CanExecute(null);
   public bool CanRedo => history.Redo.CanExecute(null);

   public HexEditorSession(PokemonModel model, string name) {
      if (model.Count == 0) throw new ArgumentException("A ROM está vazia.", nameof(model));
      Model = model;
      Name = name;
      history = new ChangeHistory<ModelDelta>(change => {
         var address = change.EarliestChange;
         var reverse = change.Revert(Model);
         if (address >= 0 && address < Model.Count) SelectedAddress = address;
         return reverse;
      });
   }

   public static bool TryHex(string? text, out int value) {
      text = text?.Trim();
      if (text?.StartsWith("0x", StringComparison.OrdinalIgnoreCase) == true) text = text[2..];
      return int.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value) && value >= 0;
   }

   public bool Select(int address) {
      if (address < 0 || address >= Model.Count) return false;
      SelectedAddress = address;
      return true;
   }

   public bool GoTo(string? text) => TryHex(text, out var address) && Select(address);

   public void MovePage(int direction) {
      var lastPage = (Model.Count - 1) / PageSize * PageSize;
      Select(Math.Clamp(PageStart + Math.Sign(direction) * PageSize, 0, lastPage));
   }

   public bool Edit(string? text) {
      if (!TryHex(text, out var value) || value > byte.MaxValue) return false;
      // Do not clear redo history for an unchanged value.
      if (Model[SelectedAddress] == value) return true;
      history.CurrentChange.ChangeData(Model, SelectedAddress, (byte)value);
      history.ChangeCompleted();
      return true;
   }

   public void Undo() { if (CanUndo) history.Undo.Execute(null); }
   public void Redo() { if (CanRedo) history.Redo.Execute(null); }
   public void MarkSaved() => history.TagAsSaved();
}
