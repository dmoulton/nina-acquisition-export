using System.IO;
using AcquisitionExport.Core;

namespace NinaAcquisitionExport;

internal static class ExportReport {
    public static string Summary(ExportResult result, IReadOnlyList<string> targetNames) {
        var warnings = result.Warnings.Count > 0 ? " " + string.Join(" ", result.Warnings) : string.Empty;
        if (!result.WroteFiles) {
            return string.Join(" ", result.Warnings);
        }
        var nights = result.NightCount == 1 ? "1 night" : $"{result.NightCount} nights";
        return $"Exported {result.FrameCount} frames from {nights} for {targetNames[0]} to {Path.GetDirectoryName(result.CsvPath)}.{warnings}";
    }

    public static IReadOnlyList<string> SplitNames(string names) =>
        (names ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
