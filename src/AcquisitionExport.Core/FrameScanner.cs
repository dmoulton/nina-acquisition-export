namespace AcquisitionExport.Core;

/// <summary>Finds a target's light frames under a folder by reading each FITS header.</summary>
public static class FrameScanner {
    private static readonly HashSet<string> FitsExtensions = new(StringComparer.OrdinalIgnoreCase) { ".fits", ".fit", ".fts" };

    public static ScanResult Scan(
        string folder,
        IReadOnlyCollection<string> targetNames,
        IProgress<string>? progress = null,
        CancellationToken token = default) {
        var names = new HashSet<string>(targetNames.Select(Normalize), StringComparer.OrdinalIgnoreCase);
        var result = new ScanResult();
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };

        foreach (var path in Directory.EnumerateFiles(folder, "*", options)) {
            token.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(path);
            if (extension.Equals(".xisf", StringComparison.OrdinalIgnoreCase)) {
                result.XisfFiles++;
                continue;
            }
            if (!FitsExtensions.Contains(extension) || IsSnapshot(path)) {
                continue;
            }
            result.FilesChecked++;
            if (result.FilesChecked % 100 == 0) {
                progress?.Report($"Checked {result.FilesChecked} files");
            }
            try {
                ReadFrame(FitsHeader.Read(path), names, result);
            } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) {
                result.UnreadableFiles++;
            }
        }
        return result;
    }

    private static void ReadFrame(FitsHeader header, HashSet<string> names, ScanResult result) {
        var target = header.GetString("OBJECT");
        if (target == null || !names.Contains(Normalize(target))) {
            return;
        }
        // Darks and flats have their own type here. NINA also writes LIGHT for snapshots,
        // so those are skipped by folder name instead.
        if (!string.Equals(header.GetString("IMAGETYP")?.Trim(), "LIGHT", StringComparison.OrdinalIgnoreCase)) {
            return;
        }
        var exposure = header.GetDouble("EXPOSURE") ?? header.GetDouble("EXPTIME");
        var start = header.GetDateTime("DATE-LOC");
        if (exposure is not > 0 || start == null) {
            result.FramesMissingData++;
            return;
        }
        result.Frames.Add(new Frame(
            target,
            header.GetString("FILTER")?.Trim() ?? "",
            exposure.Value,
            start.Value,
            header.GetInt("GAIN"),
            header.GetInt("XBINNING") ?? 1,
            header.GetDouble("CCD-TEMP") ?? header.GetDouble("SET-TEMP")));
    }

    private static bool IsSnapshot(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part.Equals("SNAPSHOT", StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
