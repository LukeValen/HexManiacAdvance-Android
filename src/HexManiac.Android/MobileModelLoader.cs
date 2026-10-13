using System.Text;
using HavenSoft.HexManiac.Core.Models;

namespace HexManiac.Mobile;

public static class MobileModelLoader {
   public static PokemonModel Create(byte[] bytes, Singletons references) {
      if (bytes.Length < 0xC0) throw new InvalidDataException("Cabeçalho GBA incompleto.");
      var gameCode = Encoding.ASCII.GetString(bytes, 0xAC, 4) + bytes[0xBC];
      // Desktop supplies empty metadata for a ROM without a sidecar. Null skips
      // DecodeConstantsFromReference, breaking table lengths such as data.items.count.
      return references.GameReferenceTables.ContainsKey(gameCode)
         ? new HardcodeTablesModel(references, bytes, new StoredMetadata(Array.Empty<string>()))
         : new PokemonModel(bytes);
   }
}
