using System.Globalization;
using System.Text;

namespace AcquisitionExport.Core;

public static class AcquisitionWriters {
    private const string CsvHeader = "date,filter,number,duration,binning,gain,sensorCooling,bortle";

    /// <summary>One row per night, filter, exposure, gain, and binning, in AstroBin's import format.</summary>
    public static string AstroBinCsv(IEnumerable<Frame> frames, ExportSettings settings) {
        var rows = frames
            .GroupBy(f => (f.Night, f.Filter, f.ExposureSeconds, f.Gain, f.Binning))
            .OrderBy(g => g.Key.Night)
            .ThenBy(g => settings.SortIndexFor(g.Key.Filter))
            .ThenBy(g => g.Key.Filter, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Key.ExposureSeconds);

        var csv = new StringBuilder(CsvHeader).Append('\n');
        foreach (var row in rows) {
            var temperatures = row.Where(f => f.SensorTemperature.HasValue).Select(f => f.SensorTemperature!.Value).ToList();
            var cooling = temperatures.Count > 0
                ? Math.Round(temperatures.Average(), MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)
                : "";
            csv.Append(string.Join(',',
                row.Key.Night.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Csv(settings.MappingFor(row.Key.Filter)?.AstroBinId.Trim() ?? ""),
                row.Count().ToString(CultureInfo.InvariantCulture),
                Number(row.Key.ExposureSeconds),
                row.Key.Binning.ToString(CultureInfo.InvariantCulture),
                row.Key.Gain?.ToString(CultureInfo.InvariantCulture) ?? "",
                cooling,
                settings.Bortle.ToString(CultureInfo.InvariantCulture))).Append('\n');
        }
        return csv.ToString();
    }

    /// <summary>The acquisition fields of a gallery entry, in the site's existing YAML format.</summary>
    public static string GalleryYaml(string title, IReadOnlyCollection<Frame> frames, ExportSettings settings) {
        var yaml = new StringBuilder();
        yaml.Append("# Add these fields to the object's entry in _data/galleries.\n");
        yaml.Append("  - title: ").Append(Yaml(title)).Append('\n');
        yaml.Append("    date: ").Append(Yaml(DateLine(frames))).Append('\n');
        yaml.Append("    location: ").Append(Yaml(settings.Location)).Append('\n');
        yaml.Append("    equipment: ").Append(Yaml(settings.Equipment)).Append('\n');
        yaml.Append("    image_info: ").Append(Yaml(ImageInfoLine(frames, settings))).Append('\n');
        return yaml.ToString();
    }

    /// <summary>For example: 13 Jun 2026 (23:45 - 04:10) | 14 Jun 2026 (23:30 - 04:10)</summary>
    public static string DateLine(IEnumerable<Frame> frames) =>
        string.Join(" | ", frames
            .GroupBy(f => f.Night)
            .OrderBy(g => g.Key)
            .Select(night => {
                var first = night.Min(f => f.LocalStart);
                var last = night.Max(f => f.LocalStart);
                var date = first.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
                return $"{date} ({Clock(first)} - {Clock(last)})";
            }));

    /// <summary>For example: 42x120" R | 66x480" Ha</summary>
    public static string ImageInfoLine(IEnumerable<Frame> frames, ExportSettings settings) =>
        string.Join(" | ", frames
            .GroupBy(f => (f.Filter, f.ExposureSeconds))
            .OrderBy(g => settings.SortIndexFor(g.Key.Filter))
            .ThenBy(g => g.Key.Filter, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Key.ExposureSeconds)
            .Select(g => $"{g.Count()}x{Number(g.Key.ExposureSeconds)}\" {settings.GalleryLabelFor(g.Key.Filter)}"));

    private static string Clock(DateTime time) {
        var minutes = (int)Math.Round(time.TimeOfDay.TotalMinutes / 5, MidpointRounding.AwayFromZero) * 5;
        return time.Date.AddMinutes(minutes).ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Csv(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static string Yaml(string value) {
        if (value.Length == 0) {
            return "''";
        }
        var needsQuotes = value.Contains(": ") || value.Contains(" #") || value.EndsWith(':')
            || "!&*-?|>'\"%@`#{}[],".Contains(value[0]) || value != value.Trim();
        return needsQuotes ? $"'{value.Replace("'", "''")}'" : value;
    }
}
