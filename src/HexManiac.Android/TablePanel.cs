using Android.App;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;
using HavenSoft.HexManiac.Core.Models;

namespace HexManiac.Mobile;

public sealed class TablePanel : LinearLayout {
   private readonly Activity activity;
   private readonly Func<HexEditorSession?> getSession;
   private readonly Action changed;
   private readonly Action<int> showHex;
   private readonly LinearLayout fields;
   private readonly TextView heading;
   private string anchor = HardcodeTablesModel.PokemonStatsTable;
   private int index;
   private HexEditorSession? displayedSession;

   public TablePanel(Activity activity, Func<HexEditorSession?> getSession, Action changed, Action<int> showHex) : base(activity) {
      this.activity = activity;
      this.getSession = getSession;
      this.changed = changed;
      this.showHex = showHex;
      Orientation = Orientation.Vertical;
      var shortcuts = Row(this);
      Button(shortcuts, "Pokémon", () => Choose(HardcodeTablesModel.PokemonStatsTable));
      Button(shortcuts, "Golpes", () => Choose(HardcodeTablesModel.MoveDataTable));
      Button(shortcuts, "Itens", () => Choose(HardcodeTablesModel.ItemsTableName));
      var actions = Row(this);
      Button(actions, "Todas as tabelas", BrowseTables);
      Button(actions, "Buscar registro", BrowseRows);
      heading = Text("");
      AddView(heading);
      var navigation = Row(this);
      Button(navigation, "◀", () => { if (index > 0) index--; Refresh(); });
      Button(navigation, "Ver no hex", () => {
         var s = getSession();
         if (s == null) return;
         var table = MobileTables.Find(s.Model, anchor);
         if (table != null) showHex(table.Start + index * table.ElementLength);
      });
      Button(navigation, "▶", () => {
         var s = getSession();
         var table = s == null ? null : MobileTables.Find(s.Model, anchor);
         if (table != null && index + 1 < table.ElementCount) index++;
         Refresh();
      });
      fields = new LinearLayout(activity) { Orientation = Orientation.Vertical };
      AddView(fields);
   }

   private TextView Text(string text) {
      var view = new TextView(activity) { Text = text, TextSize = 14 };
      view.SetTextColor(Color.Rgb(230, 233, 241));
      view.SetPadding(4, 10, 4, 10);
      return view;
   }

   private LinearLayout Row(LinearLayout parent) {
      var row = new LinearLayout(activity) { Orientation = Orientation.Horizontal };
      parent.AddView(row);
      return row;
   }

   private void Button(LinearLayout parent, string label, Action action) {
      var button = new Button(activity) { Text = label, TextSize = 12 };
      button.SetAllCaps(false);
      button.Click += (_, _) => {
         try { action(); }
         catch (Exception ex) { new AlertDialog.Builder(activity).SetMessage(ex.Message)?.SetPositiveButton("OK", (_, _) => { })?.Show(); }
      };
      parent.AddView(button, new LinearLayout.LayoutParams(0, -2, 1));
   }

   private void Choose(string name) { anchor = name; index = 0; Refresh(); }

   public void Refresh() {
      var s = getSession();
      fields.RemoveAllViews();
      if (s == null) { heading.Text = "Abra uma ROM para consultar suas tabelas."; return; }
      if (s != displayedSession) { displayedSession = s; anchor = HardcodeTablesModel.PokemonStatsTable; index = 0; }
      try {
         var table = MobileTables.Find(s.Model, anchor);
         if (table == null) { heading.Text = anchor + "\nTabela não reconhecida nesta ROM."; return; }
         index = Math.Clamp(index, 0, table.ElementCount - 1);
         heading.Text = $"{anchor}\n#{index} — {MobileTables.RowName(s.Model, anchor, index)}\n{table.ElementCount} registros • números em decimal";
         foreach (var field in MobileTables.Fields(s.Model, anchor, index)) {
            var row = Row(fields);
            row.AddView(Text(field.Name + ": " + field.Value), new LinearLayout.LayoutParams(0, -2, 2));
            if (field.Editable) Button(row, "Editar", () => Edit(field));
         }
      } catch (Exception ex) {
         heading.Text = "Não foi possível interpretar a tabela: " + ex.Message;
      }
   }

   private void Edit(MobileField field) {
      var input = new EditText(activity) { Text = field.Value, InputType = InputTypes.ClassNumber };
      input.SetSingleLine(true);
      var builder = new AlertDialog.Builder(activity);
      builder.SetTitle(field.Name + " (decimal)");
      builder.SetView(input);
      builder.SetNegativeButton("Cancelar", (_, _) => { });
      builder.SetPositiveButton("Aplicar", (_, _) => {
         var s = getSession();
         if (s == null) return;
         if (!MobileTables.EditNumber(s, anchor, index, field.Name, input.Text, out var error)) {
            new AlertDialog.Builder(activity).SetMessage(error)?.SetPositiveButton("OK", (_, _) => { })?.Show();
            return;
         }
         changed();
      });
      builder.Show();
   }

   private void BrowseTables() {
      var s = getSession();
      if (s == null) return;
      var names = MobileTables.Catalog(s.Model);
      Search("Tabelas reconhecidas", names, selected => Choose(names[selected]));
   }

   private void BrowseRows() {
      var s = getSession();
      if (s == null) return;
      var table = MobileTables.Find(s.Model, anchor);
      if (table == null) return;
      var names = Enumerable.Range(0, table.ElementCount).Select(i => $"#{i} — {MobileTables.RowName(s.Model, anchor, i)}").ToArray();
      Search("Nome ou número do registro", names, selected => { index = selected; Refresh(); });
   }

   private void Search(string title, string[] options, Action<int> choose) {
      var layout = new LinearLayout(activity) { Orientation = Orientation.Vertical };
      var input = new EditText(activity) { Hint = "Digite para filtrar" };
      input.SetSingleLine(true);
      layout.AddView(input);
      var count = new TextView(activity);
      layout.AddView(count);
      var list = new ListView(activity);
      layout.AddView(list, new LinearLayout.LayoutParams(-1, (int)(320 * Resources!.DisplayMetrics!.Density)));
      var matches = Array.Empty<int>();
      void Filter() {
         matches = Enumerable.Range(0, options.Length)
            .Where(i => options[i].Contains(input.Text ?? "", StringComparison.OrdinalIgnoreCase)).Take(200).ToArray();
         count.Text = $"{matches.Length} exibidos (até 200). Refine a busca.";
         list.Adapter = new ArrayAdapter<string>(activity, global::Android.Resource.Layout.SimpleListItem1,
            matches.Select(i => options[i]).ToArray());
      }
      input.TextChanged += (_, _) => Filter();
      Filter();
      var dialog = new AlertDialog.Builder(activity).SetTitle(title)?.SetView(layout)?.SetNegativeButton("Fechar", (_, _) => { })?.Create();
      list.ItemClick += (_, e) => { choose(matches[e.Position]); dialog?.Dismiss(); };
      dialog?.Show();
   }
}
