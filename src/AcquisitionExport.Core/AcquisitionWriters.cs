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

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Csv(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
