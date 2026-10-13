using System.Globalization;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;

namespace HexManiac.Mobile;

public sealed record MobileField(string Name, int Address, string Value, bool Editable, int Length);

public static class MobileTables {
   private static readonly Dictionary<string, HashSet<string>> EditableFields = new() {
      [HardcodeTablesModel.PokemonStatsTable] = new("hp attack def speed spatk spdef catchRate baseExp steps2hatch basehappiness runrate".Split(' ')),
      [HardcodeTablesModel.MoveDataTable] = new("power accuracy pp effectAccuracy".Split(' ')),
      [HardcodeTablesModel.ItemsTableName] = new("price param".Split(' ')),
   };

   public static ITableRun? Find(PokemonModel model, string anchor) {
      var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchor);
      if (address < 0 || address >= model.Count) return null;
      return model.GetNextRun(address) is ITableRun table && table.Start == address &&
         table.ElementCount > 0 && table.ElementLength > 0 &&
         (long)table.Start + (long)table.ElementCount * table.ElementLength <= model.Count ? table : null;
   }

   public static string[] Catalog(PokemonModel model) => model.GetAutoCompleteAnchorNameOptions("", int.MaxValue)
      .Where(name => Find(model, name) != null).OrderBy(name => name).ToArray();

   public static string RowName(PokemonModel model, string anchor, int index) {
      var table = Find(model, anchor);
      if (table == null || index < 0 || index >= table.ElementCount) return "";
      if (table.ElementNames is { } names && index < names.Count && !string.IsNullOrWhiteSpace(names[index]))
         return names[index];
      var text = table.ElementContent.FirstOrDefault(s => s.Type == ElementContentType.PCS && s.Name == "name");
      if (text != null) {
         var offset = table.ElementContent.TakeWhile(s => s != text).Sum(s => s.Length);
         return model.TextConverter.Convert(model, table.Start + index * table.ElementLength + offset, text.Length).Trim('"');
      }
      return $"Registro {index}";
   }

   public static IReadOnlyList<MobileField> Fields(PokemonModel model, string anchor, int index) {
      var table = Find(model, anchor);
      if (table == null || index < 0 || index >= table.ElementCount) return Array.Empty<MobileField>();
      var fields = new List<MobileField>();
      var address = table.Start + index * table.ElementLength;
      foreach (var segment in table.ElementContent) {
         var canEdit = EditableFields.TryGetValue(anchor, out var allowed) && allowed.Contains(segment.Name) &&
            segment.GetType() == typeof(ArrayRunElementSegment) && segment.Type == ElementContentType.Integer &&
            segment.Length is >= 1 and <= 3;
         if (!segment.IsUnused()) {
            string value;
            try { value = segment.ToText(model, address, 0); }
            catch (NotImplementedException) { value = "(campo composto)"; }
            fields.Add(new MobileField(segment.Name, address, value, canEdit, segment.Length));
         }
         address += segment.Length;
      }
      return fields;
   }

   public static bool EditNumber(HexEditorSession session, string anchor, int index, string fieldName, string? input, out string error) {
      error = "Campo indisponível para edição.";
      var field = Fields(session.Model, anchor, index).FirstOrDefault(f => f.Name == fieldName);
      if (field == null || !field.Editable) return false;
      var maximum = (1 << (field.Length * 8)) - 1;
      if (!int.TryParse(input?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number > maximum) {
         error = $"Use um número decimal entre 0 e {maximum}.";
         return false;
      }
      var bytes = Enumerable.Range(0, field.Length).Select(i => (byte)(number >> (8 * i))).ToArray();
      session.WriteBytes(field.Address, bytes);
      error = "";
      return true;
   }
}
