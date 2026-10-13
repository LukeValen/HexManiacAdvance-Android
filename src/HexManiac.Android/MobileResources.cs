using Android.Content;
using HavenSoft.HexManiac.Core.Models;

namespace HexManiac.Mobile;

public static class MobileResources {
   private static readonly object Gate = new();
   private static Singletons? references;

   public static Singletons Load(Context context) {
      lock (Gate) {
         if (references != null) return references;
         var root = Path.Combine(context.FilesDir!.AbsolutePath, "hma-reference-v3");
         var directory = Path.Combine(root, "resources");
         Directory.CreateDirectory(directory);
         var assets = context.Assets!;
         foreach (var name in assets.List("resources") ?? Array.Empty<string>()) {
            using var source = assets.Open("resources/" + name);
            using var target = File.Create(Path.Combine(directory, name));
            source.CopyTo(target);
         }
         foreach (var required in new[] { "tableReference.txt", "pcsReference.txt", "default.toml", "hma.py" })
            if (!File.Exists(Path.Combine(directory, required)))
               throw new InvalidDataException("Referência ausente no APK: " + required);
         // The unmodified desktop Core resolves resources relative to the process directory.
         // Set this once, before constructing any model or initializing PCSString.
         Directory.SetCurrentDirectory(root);
         references = new Singletons();
         if (!references.GameReferenceTables.ContainsKey("BPRE1")) {
            references = null;
            throw new InvalidDataException("Referências do FireRed Rev 1 não foram carregadas.");
         }
         return references;
      }
   }
}
