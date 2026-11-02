using System.Globalization;
using System.Text;

namespace AcquisitionExport.Core;

/// <summary>Reads FITS header cards without touching the image data.</summary>
public sealed class FitsHeader {
    private const int BlockSize = 2880;
    private const int CardSize = 80;
    private const int MaxHeaderBlocks = 256;

    private readonly Dictionary<string, string> cards = new(StringComparer.OrdinalIgnoreCase);

    private FitsHeader() { }

    public static FitsHeader Read(string path) {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Read(stream);
    }

    public static FitsHeader Read(Stream stream) {
        var header = new FitsHeader();
        header.ReadUnit(stream);
        if (!header.cards.ContainsKey("SIMPLE")) {
            throw new InvalidDataException("Not a FITS file.");
        }
        // A compressed file keeps an empty primary unit, with the real cards in the next one.
        if (header.GetInt("NAXIS") == 0 && !header.cards.ContainsKey("OBJECT")) {
            header.ReadUnit(stream);
        }
        return header;
    }

    private void ReadUnit(Stream stream) {
        var block = new byte[BlockSize];
        for (var blockIndex = 0; blockIndex < MaxHeaderBlocks; blockIndex++) {
            var read = stream.ReadAtLeast(block, BlockSize, throwOnEndOfStream: false);
            if (read < BlockSize) {
                return;
            }
            for (var offset = 0; offset < BlockSize; offset += CardSize) {
                var card = Encoding.ASCII.GetString(block, offset, CardSize);
                var key = card[..8].Trim();
                if (key == "END") {
                    return;
                }
                if (key.Length > 0 && card[8] == '=' && !cards.ContainsKey(key)) {
                    cards[key] = ParseValue(card[9..]);
                }
            }
        }
    }

    private static string ParseValue(string text) {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('\'')) {
            var value = new StringBuilder();
            for (var i = 1; i < trimmed.Length; i++) {
                if (trimmed[i] != '\'') {
                    value.Append(trimmed[i]);
                } else if (i + 1 < trimmed.Length && trimmed[i + 1] == '\'') {
                    value.Append('\'');
                    i++;
                } else {
                    break;
                }
            }
            return value.ToString().TrimEnd();
        }
        var comment = trimmed.IndexOf('/');
        return (comment >= 0 ? trimmed[..comment] : trimmed).Trim();
    }

    public string? GetString(string key) => cards.TryGetValue(key, out var value) ? value : null;

    public double? GetDouble(string key) =>
        double.TryParse(GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    public int? GetInt(string key) {
        var value = GetDouble(key);
        return value.HasValue ? (int)Math.Round(value.Value) : null;
    }

    public DateTime? GetDateTime(string key) =>
        DateTime.TryParse(GetString(key), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
}
