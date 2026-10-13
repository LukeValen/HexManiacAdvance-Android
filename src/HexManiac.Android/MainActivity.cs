using System.Text;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using HavenSoft.HexManiac.Core.Models;

namespace HexManiac.Mobile;

[Activity(Label = "HexManiac Mobile", MainLauncher = true, Exported = true,
   ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public class MainActivity : Activity {
   private const int OpenRequest = 100, SaveRequest = 101;
   private HexEditorSession? session;
   private bool busy;
   private TextView status = null!, summary = null!, pageLabel = null!;
   private EditText offset = null!, value = null!;
   private LinearLayout grid = null!;
   private Button open = null!, save = null!, undo = null!, redo = null!, previous = null!, next = null!;
   private readonly List<View> editingControls = new();
   private static readonly Color Background = Color.Rgb(24, 27, 35);
   private static readonly Color Foreground = Color.Rgb(230, 233, 241);
   private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density + 0.5f);

   protected override void OnCreate(Bundle? state) {
      base.OnCreate(state);
      ActionBar?.Hide();
      Window?.SetSoftInputMode(SoftInput.AdjustResize);
      var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
      layout.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
      layout.SetBackgroundColor(Background);
      var title = Label("HexManiac Mobile", 22);
      layout.AddView(title);
      summary = Label("Editor hexadecimal • Pokémon GBA", 14);
      layout.AddView(summary);
      var files = Row(layout);
      open = AddButton(files, "Abrir ROM", () => ConfirmDiscard(OpenRom));
      save = AddButton(files, "Salvar cópia", SaveRom);
      status = Label("Selecione uma ROM GBA para começar.", 14);
      layout.AddView(status);
      var navigation = Row(layout);
      offset = Input("Endereço hex (ex.: 0x100)");
      navigation.AddView(offset, new LinearLayout.LayoutParams(0, -2, 1));
      editingControls.Add(offset);
      editingControls.Add(AddButton(navigation, "Ir", GoTo, false));
      var editRow = Row(layout);
      value = Input("Byte (00–FF)");
      editRow.AddView(value, new LinearLayout.LayoutParams(0, -2, 1));
      editingControls.Add(value);
      editingControls.Add(AddButton(editRow, "Aplicar", EditByte, false));
      var history = Row(layout);
      undo = AddButton(history, "Desfazer", () => { session?.Undo(); Refresh(); });
      redo = AddButton(history, "Refazer", () => { session?.Redo(); Refresh(); });
      var pages = Row(layout);
      previous = AddButton(pages, "◀ Anterior", () => { session?.MovePage(-1); Refresh(); });
      next = AddButton(pages, "Próxima ▶", () => { session?.MovePage(1); Refresh(); });
      pageLabel = Label("Toque em um byte para selecioná-lo.", 13);
      layout.AddView(pageLabel);
      grid = new LinearLayout(this) { Orientation = Orientation.Vertical };
      var horizontal = new HorizontalScrollView(this);
      horizontal.AddView(grid);
      layout.AddView(horizontal);
      var scroll = new SafeAreaScrollView(this) { FillViewport = true, ContentDescription = "HexManiac Mobile" };
      scroll.SetBackgroundColor(Background);
      scroll.AddView(layout);
      SetContentView(scroll);
      layout.FocusableInTouchMode = true;
      layout.RequestFocus();
      Refresh();
   }

   private TextView Label(string text, float size) {
      var label = new TextView(this) { Text = text, TextSize = size };
      label.SetTextColor(Foreground);
      label.SetPadding(0, Dp(4), 0, Dp(4));
      return label;
   }

   private EditText Input(string hint) {
      var input = new EditText(this) { Hint = hint, SingleLine = true,
         InputType = global::Android.Text.InputTypes.ClassText | global::Android.Text.InputTypes.TextFlagNoSuggestions };
      input.SetTextColor(Foreground);
      input.SetHintTextColor(Color.Rgb(160, 169, 189));
      return input;
   }

   private LinearLayout Row(LinearLayout parent) {
      var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
      parent.AddView(row);
      return row;
   }

   private Button AddButton(LinearLayout parent, string text, Action action, bool weighted = true) {
      var button = new Button(this) { Text = text, TextSize = 12 };
      button.SetAllCaps(false);
      button.Click += (_, _) => { if (!busy) action(); };
      parent.AddView(button, new LinearLayout.LayoutParams(weighted ? 0 : -2, -2, weighted ? 1 : 0));
      return button;
   }

   private void ConfirmDiscard(Action action) {
      if (session?.IsDirty != true) { action(); return; }
      new AlertDialog.Builder(this)
         .SetTitle("Alterações não salvas")
         .SetMessage("Salve uma cópia para manter suas alterações. Deseja descartá-las?")
         .SetNegativeButton("Cancelar", (_, _) => { })
         .SetPositiveButton("Descartar", (_, _) => action())
         .Show();
   }

   public override void OnBackPressed() {
      if (busy) { status.Text = "Aguarde a operação terminar."; return; }
      ConfirmDiscard(() => base.OnBackPressed());
   }

   private void OpenRom() {
      var intent = new Intent(Intent.ActionOpenDocument);
      intent.AddCategory(Intent.CategoryOpenable);
      intent.SetType("*/*");
      LaunchPicker(intent, OpenRequest);
   }

   private void SaveRom() {
      if (session == null) return;
      var intent = new Intent(Intent.ActionCreateDocument);
      intent.AddCategory(Intent.CategoryOpenable);
      intent.SetType("application/octet-stream");
      intent.PutExtra(Intent.ExtraTitle, "editada-" + session.Name);
      LaunchPicker(intent, SaveRequest);
   }

   private void LaunchPicker(Intent intent, int request) {
      try {
         busy = true;
         UpdateControls();
         StartActivityForResult(intent, request);
      } catch (Exception ex) {
         busy = false;
         UpdateControls();
         ReportError(ex);
      }
   }

   private void GoTo() {
      if (session == null) return;
      if (!session.GoTo(offset.Text)) {
         status.Text = $"Endereço inválido. Use 0 a {session.Model.Count - 1:X}.";
         return;
      }
      Refresh();
      status.Text = $"Selecionado: 0x{session.SelectedAddress:X6}.";
   }

   private void EditByte() {
      if (session == null) return;
      // Apply the address field as well, so a typed offset is never silently ignored.
      if (!session.GoTo(offset.Text)) { status.Text = "Endereço inválido."; return; }
      if (!session.Edit(value.Text)) { status.Text = "Byte inválido. Use 00 a FF."; return; }
      Refresh();
      status.Text = $"Byte 0x{session.SelectedAddress:X6}: {session.Model[session.SelectedAddress]:X2}.";
   }

   private void UpdateControls() {
      var ready = session != null && !busy;
      open.Enabled = !busy;
      save.Enabled = ready;
      foreach (var control in editingControls) control.Enabled = ready;
      undo.Enabled = ready && session!.CanUndo;
      redo.Enabled = ready && session!.CanRedo;
      previous.Enabled = ready && session!.PageStart > 0;
      next.Enabled = ready && session!.PageStart + HexEditorSession.PageSize < session.Model.Count;
      for (var i = 0; i < grid.ChildCount; i++) {
         if (grid.GetChildAt(i) is ViewGroup row)
            for (var j = 0; j < row.ChildCount; j++) row.GetChildAt(j)!.Enabled = ready;
      }
   }

   private void Refresh() {
      grid.RemoveAllViews();
      if (session != null) {
         var model = session.Model;
         var code = Encoding.ASCII.GetString(model.RawData, 0xAC, 4);
         summary.Text = $"{session.Name}\n{code} • {model.Count:N0} bytes • {(session.IsDirty ? "Alterações não salvas" : "Sem alterações pendentes")}";
         offset.Text = session.SelectedAddress.ToString("X6");
         value.Text = model[session.SelectedAddress].ToString("X2");
         var end = Math.Min(session.PageStart + HexEditorSession.PageSize, model.Count);
         pageLabel.Text = $"0x{session.PageStart:X6}–0x{end - 1:X6} • Toque para selecionar";
         var header = Row(grid);
         header.AddView(Label("Offset", 12), new LinearLayout.LayoutParams(Dp(66), Dp(32)));
         for (var column = 0; column < HexEditorSession.Columns; column++) {
            var label = Label(column.ToString("X2"), 12);
            label.Gravity = GravityFlags.Center;
            header.AddView(label, new LinearLayout.LayoutParams(Dp(36), Dp(32)));
         }
         for (var start = session.PageStart; start < end; start += HexEditorSession.Columns) {
            var row = Row(grid);
            var addressLabel = Label(start.ToString("X6"), 12);
            addressLabel.Typeface = Typeface.Monospace;
            addressLabel.Gravity = GravityFlags.CenterVertical;
            row.AddView(addressLabel, new LinearLayout.LayoutParams(Dp(66), Dp(48)));
            for (var address = start; address < Math.Min(start + HexEditorSession.Columns, end); address++) {
               var selected = address;
               var cell = Label(model[address].ToString("X2"), 14);
               cell.Typeface = Typeface.Monospace;
               cell.Gravity = GravityFlags.Center;
               cell.ContentDescription = $"Endereço {address:X6}, byte {model[address]:X2}";
               cell.SetBackgroundColor(address == session.SelectedAddress ? Color.Rgb(62, 86, 150) : Color.Rgb(35, 40, 52));
               cell.Click += (_, _) => { if (!busy) { session.Select(selected); Refresh(); } };
               row.AddView(cell, new LinearLayout.LayoutParams(Dp(36), Dp(48)));
            }
         }
      }
      UpdateControls();
   }

   protected override async void OnActivityResult(int requestCode, Result resultCode, Intent? data) {
      base.OnActivityResult(requestCode, resultCode, data);
      if (requestCode != OpenRequest && requestCode != SaveRequest) return;
      if (resultCode != Result.Ok || data?.Data == null) {
         busy = false;
         UpdateControls();
         return;
      }
      try {
         if (requestCode == OpenRequest) {
            status.Text = "Carregando ROM...";
            using var source = ContentResolver!.OpenInputStream(data.Data) ?? throw new IOException("Não foi possível abrir a ROM.");
            using var buffer = new MemoryStream();
            // Bound input size before parsing to avoid exhausting the phone's memory.
            var chunk = new byte[65536];
            int read;
            while ((read = await source.ReadAsync(chunk)) > 0) {
               if (buffer.Length + read > 32 * 1024 * 1024) throw new InvalidDataException("Selecione uma ROM GBA de até 32 MiB.");
               await buffer.WriteAsync(chunk.AsMemory(0, read));
            }
            var bytes = buffer.ToArray();
            if (bytes.Length < 0xC0) throw new InvalidDataException("Arquivo pequeno demais para uma ROM GBA.");
            var name = QueryName(data.Data);
            var loaded = await Task.Run(async () => {
               var parsed = new PokemonModel(bytes);
               await parsed.InitializationWorkload;
               return parsed;
            });
            session = new HexEditorSession(loaded, name);
            status.Text = "ROM carregada. Toque em um byte ou digite um endereço.";
         } else if (session != null) {
            status.Text = "Salvando cópia...";
            using (var destination = ContentResolver!.OpenOutputStream(data.Data, "wt") ?? throw new IOException("Não foi possível salvar a cópia.")) {
               await destination.WriteAsync(session.Model.RawData);
               await destination.FlushAsync();
            }
            session.MarkSaved();
            status.Text = $"Cópia salva ({session.Model.Count:N0} bytes).";
         }
      } catch (Exception ex) {
         ReportError(ex);
      } finally {
         busy = false;
         Refresh();
      }
   }

   private void ReportError(Exception ex) {
      status.Text = $"Erro: {ex.Message}";
      global::Android.Util.Log.Error("HexManiacMobile", ex.ToString());
   }

   private string QueryName(global::Android.Net.Uri uri) {
      using var cursor = ContentResolver!.Query(uri, new[] { global::Android.Provider.OpenableColumns.DisplayName }, null, null, null);
      return cursor != null && cursor.MoveToFirst() ? cursor.GetString(0) ?? "ROM.gba" : "ROM.gba";
   }
}
