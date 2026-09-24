using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using HavenSoft.HexManiac.Core.Models;

namespace HexManiac.Mobile;

[Activity(Label = "HexManiac Mobile", MainLauncher = true, Exported = true)]
public class MainActivity : Activity {
   private const int OpenRequest = 100;
   private const int SaveRequest = 101;
   private PokemonModel? model;
   private string romName = "ROM.gba";
   private TextView status = null!;
   private EditText offset = null!;
   private EditText value = null!;

   protected override void OnCreate(Bundle? state) {
      base.OnCreate(state);
      var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
      layout.SetPadding(24, 24, 24, 24);
      status = new TextView(this) { Text = "Selecione uma ROM GBA para começar." };
      layout.AddView(status);
      var open = new Button(this) { Text = "Abrir ROM" };
      open.Click += (_, _) => {
         var intent = new Intent(Intent.ActionOpenDocument);
         intent.AddCategory(Intent.CategoryOpenable);
         intent.SetType("application/octet-stream");
         intent.PutExtra(Intent.ExtraMimeTypes, new[] { "application/octet-stream", "application/x-gba-rom", "*/*" });
         StartActivityForResult(intent, OpenRequest);
      };
      layout.AddView(open);
      offset = new EditText(this) { Hint = "Offset hexadecimal (ex.: 100)" };
      offset.InputType = Android.Text.InputTypes.ClassText;
      layout.AddView(offset);
      value = new EditText(this) { Hint = "Novo byte hexadecimal (00–FF)" };
      value.InputType = Android.Text.InputTypes.ClassText;
      layout.AddView(value);
      var edit = new Button(this) { Text = "Alterar byte" };
      edit.Click += (_, _) => EditByte();
      layout.AddView(edit);
      var save = new Button(this) { Text = "Salvar uma cópia" };
      save.Click += (_, _) => {
         if (model == null) { status.Text = "Abra uma ROM primeiro."; return; }
         var intent = new Intent(Intent.ActionCreateDocument);
         intent.AddCategory(Intent.CategoryOpenable);
         intent.SetType("application/octet-stream");
         intent.PutExtra(Intent.ExtraTitle, "editada-" + romName);
         StartActivityForResult(intent, SaveRequest);
      };
      layout.AddView(save);
      var scroll = new ScrollView(this) { FillViewport = true, ContentDescription = "HexManiac Mobile" };
      scroll.AddView(layout);
      SetContentView(scroll);
   }

   private void EditByte() {
      if (model == null) { status.Text = "Abra uma ROM primeiro."; return; }
      if (!int.TryParse(offset.Text?.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase),
          System.Globalization.NumberStyles.HexNumber, null, out var address) || address < 0 || address >= model.Count ||
          !byte.TryParse(value.Text?.Trim(), System.Globalization.NumberStyles.HexNumber, null, out var b)) {
         status.Text = "Offset ou byte inválido.";
         return;
      }
      var previous = model[address];
      model[address] = b;
      status.Text = $"{romName}: 0x{address:X} alterado de {previous:X2} para {b:X2}. Salve uma cópia.";
   }

   protected override async void OnActivityResult(int requestCode, Result resultCode, Intent? data) {
      base.OnActivityResult(requestCode, resultCode, data);
      if (resultCode != Result.Ok || data?.Data == null) return;
      try {
         if (requestCode == OpenRequest) {
            status.Text = "Carregando ROM...";
            using var source = ContentResolver!.OpenInputStream(data.Data) ?? throw new IOException("Não foi possível abrir a ROM.");
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer);
            var bytes = buffer.ToArray();
            if (bytes.Length < 0xB0) throw new InvalidDataException("Arquivo pequeno demais para uma ROM GBA.");
            var name = QueryName(data.Data);
            var loaded = await Task.Run(async () => {
               var parsed = new PokemonModel(bytes);
               await parsed.InitializationWorkload;
               return parsed;
            });
            model = loaded;
            romName = name;
            status.Text = $"{romName}\nCódigo: {Encoding.ASCII.GetString(bytes, 0xAC, 4)}\nTamanho: {bytes.Length:N0} bytes\nPokemonModel carregado.";
         } else if (requestCode == SaveRequest && model != null) {
            using var destination = ContentResolver!.OpenOutputStream(data.Data, "wt") ?? throw new IOException("Não foi possível salvar a cópia.");
            await destination.WriteAsync(model.RawData);
            await destination.FlushAsync();
            status.Text = $"Cópia salva ({model.Count:N0} bytes).";
         }
      } catch (Exception ex) {
         status.Text = $"Erro: {ex.Message}";
         Android.Util.Log.Error("HexManiacMobile", ex.ToString());
      }
   }

   private string QueryName(Android.Net.Uri uri) {
      using var cursor = ContentResolver!.Query(uri, new[] { Android.Provider.OpenableColumns.DisplayName }, null, null, null);
      if (cursor != null && cursor.MoveToFirst()) return cursor.GetString(0) ?? romName;
      return romName;
   }
}
