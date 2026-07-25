using System.Text;

namespace DndCompanion.Services;

/// <summary>Chiave di confronto fra voci di catalogo (§4.3 dello spec) e riconoscimento della
/// provenienza (§6). Logica pura.</summary>
public static class CatalogKey
{
    // Piega degli accenti scritta a mano: il progetto compila con InvariantGlobalization=true,
    // quindi non c'è ICU e String.Normalize non decompone nulla — senza sollevare eccezioni.
    // Copre l'insieme latino che serve all'italiano più i casi comuni di altre lingue.
    private static readonly Dictionary<char, char> AccentFolding = new()
    {
        ['à'] = 'a', ['á'] = 'a', ['â'] = 'a', ['ã'] = 'a', ['ä'] = 'a', ['å'] = 'a',
        ['è'] = 'e', ['é'] = 'e', ['ê'] = 'e', ['ë'] = 'e',
        ['ì'] = 'i', ['í'] = 'i', ['î'] = 'i', ['ï'] = 'i',
        ['ò'] = 'o', ['ó'] = 'o', ['ô'] = 'o', ['õ'] = 'o', ['ö'] = 'o',
        ['ù'] = 'u', ['ú'] = 'u', ['û'] = 'u', ['ü'] = 'u',
        ['ç'] = 'c', ['ñ'] = 'n', ['ý'] = 'y', ['ÿ'] = 'y',
    };

    /// <summary>Trim, minuscole e accenti piegati. Null o vuoto → stringa vuota.</summary>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var lowered = name.Trim().ToLowerInvariant();
        var sb = new StringBuilder(lowered.Length);
        foreach (var c in lowered)
            sb.Append(AccentFolding.TryGetValue(c, out var folded) ? folded : c);
        return sb.ToString();
    }

    /// <summary>Chiave di confronto: l'identificatore di provenienza se c'è, altrimenti il nome
    /// normalizzato.</summary>
    public static string For(string? sourceId, string? name)
        => string.IsNullOrWhiteSpace(sourceId) ? NormalizeName(name) : sourceId.Trim();

    /// <summary>Vero se la voce proviene dal pacchetto distribuito con l'app: è ciò che la rende
    /// di sola lettura (§6). Il confronto è sul prefisso "&lt;id pacchetto&gt;/".</summary>
    public static bool IsFromAppPackage(string? sourceId)
        => !string.IsNullOrWhiteSpace(sourceId)
           && sourceId.StartsWith(CatalogPackageParser.AppPackageId + "/", StringComparison.Ordinal);
}
