using System.ComponentModel;
using System.Runtime.CompilerServices;
using AcquisitionExport.Core;

namespace NinaAcquisitionExport;

/// <summary>An editable row of the filter table on the options page.</summary>
public sealed class FilterRow : INotifyPropertyChanged {
    private string ninaFilter = string.Empty;
    private string astroBinId = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string NinaFilter {
        get => ninaFilter;
        set => Set(ref ninaFilter, value);
    }

    public string AstroBinId {
        get => astroBinId;
        set => Set(ref astroBinId, value);
    }

    public FilterMapping ToMapping() => new() {
        NinaFilter = NinaFilter ?? string.Empty,
        AstroBinId = AstroBinId ?? string.Empty,
    };

    public static FilterRow From(FilterMapping mapping) => new() {
        NinaFilter = mapping.NinaFilter,
        AstroBinId = mapping.AstroBinId,
    };

    private void Set(ref string field, string value, [CallerMemberName] string? name = null) {
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
