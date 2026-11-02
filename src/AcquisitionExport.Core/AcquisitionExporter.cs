using System.Text;

namespace AcquisitionExport.Core;

public static class AcquisitionExporter {
    /// <summary>
    /// Rebuilds a target's files from every matching frame on disk. Existing files are
    /// left alone if no frames are found.
    /// </summary>
    public static ExportResult Export(
        ExportSettings settings,
        IReadOnlyList<string> targetNames,
        IProgress<string>? progress = null,
        CancellationToken token = default) {
        var result = new ExportResult();
        var names = targetNames.Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
        if (names.Count == 0) {
            result.Warnings.Add("No target name was given.");
            return result;
        }
        if (string.IsNullOrWhiteSpace(settings.ImageFolder) || !Directory.Exists(settings.ImageFolder)) {
            result.Warnings.Add($"The image folder \"{settings.ImageFolder}\" doesn't exist.");
            return result;
        }
        if (string.IsNullOrWhiteSpace(settings.ExportFolder)) {
            result.Warnings.Add("No export folder is set.");
            return result;
        }

        var scan = FrameScanner.Scan(settings.ImageFolder, names, progress, token);
        AddScanWarnings(scan, result);
        if (scan.Frames.Count == 0) {
            result.Warnings.Insert(0, $"No light frames were found for {string.Join(", ", names)}.");
            return result;
        }

        var unmapped = scan.Frames
            .Select(f => f.Filter)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(f => string.IsNullOrWhiteSpace(settings.MappingFor(f)?.AstroBinId))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unmapped.Count > 0) {
            var filters = string.Join(", ", unmapped.Select(f => f.Length > 0 ? f : "(no filter)"));
            result.Warnings.Add($"No AstroBin ID for {filters}. Those CSV rows have a blank filter.");
        }

        Directory.CreateDirectory(settings.ExportFolder);
        var fileStem = SafeFileName(names[0]);
        var csvPath = Path.Combine(settings.ExportFolder, $"{fileStem}_astrobin.csv");
        var galleryPath = Path.Combine(settings.ExportFolder, $"{fileStem}_gallery.yml");
        WriteFile(csvPath, AcquisitionWriters.AstroBinCsv(scan.Frames, settings));
        WriteFile(galleryPath, AcquisitionWriters.GalleryYaml(names[0], scan.Frames, settings));

        result.CsvPath = csvPath;
        result.GalleryPath = galleryPath;
        result.FrameCount = scan.Frames.Count;
        result.NightCount = scan.Frames.Select(f => f.Night).Distinct().Count();
        return result;
    }

    private static void AddScanWarnings(ScanResult scan, ExportResult result) {
        if (scan.UnreadableFiles > 0) {
            result.Warnings.Add($"{scan.UnreadableFiles} FITS files couldn't be read and were skipped.");
        }
        if (scan.FramesMissingData > 0) {
            result.Warnings.Add($"{scan.FramesMissingData} frames had no exposure time or local date and were skipped.");
        }
        if (scan.XisfFiles > 0) {
            result.Warnings.Add($"{scan.XisfFiles} XISF files were skipped because only FITS is supported.");
        }
    }

    private static void WriteFile(string path, string contents) {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporary, path, overwrite: true);
    }

    private static string SafeFileName(string name) {
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars()) { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
