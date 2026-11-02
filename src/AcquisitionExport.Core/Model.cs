namespace AcquisitionExport.Core;

public sealed record Frame(
    string Target,
    string Filter,
    double ExposureSeconds,
    DateTime LocalStart,
    int? Gain,
    int Binning,
    double? SensorTemperature) {
    /// <summary>A night runs noon to noon, so a session past midnight stays together.</summary>
    public DateOnly Night => DateOnly.FromDateTime(LocalStart.AddHours(-12));
}

/// <summary>The AstroBin filter ID for one NINA filter name.</summary>
public sealed class FilterMapping {
    public string NinaFilter { get; set; } = "";
    public string AstroBinId { get; set; } = "";
}

public sealed class ExportSettings {
    public string ImageFolder { get; set; } = "";
    public string ExportFolder { get; set; } = "";
    public int Bortle { get; set; } = 6;
    public List<FilterMapping> Filters { get; set; } = new();

    public FilterMapping? MappingFor(string filter) =>
        Filters.FirstOrDefault(f => string.Equals(f.NinaFilter.Trim(), filter.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Filters sort in settings order, then alphabetically if they aren't listed.</summary>
    public int SortIndexFor(string filter) {
        var mapping = MappingFor(filter);
        return mapping == null ? int.MaxValue : Filters.IndexOf(mapping);
    }
}

public sealed class ScanResult {
    public List<Frame> Frames { get; } = new();
    public int FilesChecked { get; set; }
    public int UnreadableFiles { get; set; }
    public int XisfFiles { get; set; }
    public int FramesMissingData { get; set; }
}

public sealed class ExportResult {
    public string? CsvPath { get; set; }
    public int FrameCount { get; set; }
    public int NightCount { get; set; }
    public List<string> Warnings { get; } = new();
    public bool WroteFiles => CsvPath != null;
}
