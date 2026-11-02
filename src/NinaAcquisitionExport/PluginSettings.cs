using System.IO;
using System.Text.Json;
using AcquisitionExport.Core;
using NINA.Profile;
using NINA.Profile.Interfaces;

namespace NinaAcquisitionExport;

/// <summary>Reads and writes this plugin's settings on the active NINA profile.</summary>
public sealed class PluginSettings {
    // Matches the Guid in Properties/AssemblyInfo.cs.
    public static readonly Guid PluginId = Guid.Parse("33703962-7ee2-43ef-9dfd-91e2ead26fe0");
    private const string ExportFolderName = "Acquisition Exports";

    private readonly IProfileService profileService;
    private readonly PluginOptionsAccessor accessor;

    public PluginSettings(IProfileService profileService) {
        this.profileService = profileService;
        accessor = new PluginOptionsAccessor(profileService, PluginId);
    }

    public string ImageFolder {
        get => accessor.GetValueString(nameof(ImageFolder), string.Empty);
        set => accessor.SetValueString(nameof(ImageFolder), value ?? string.Empty);
    }

    public string ExportFolder {
        get => accessor.GetValueString(nameof(ExportFolder), string.Empty);
        set => accessor.SetValueString(nameof(ExportFolder), value ?? string.Empty);
    }

    public int Bortle {
        get => accessor.GetValueInt32(nameof(Bortle), 6);
        set => accessor.SetValueInt32(nameof(Bortle), value);
    }

    public List<FilterMapping> Filters {
        get {
            var json = accessor.GetValueString(nameof(Filters), string.Empty);
            if (string.IsNullOrWhiteSpace(json)) {
                return new List<FilterMapping>();
            }
            try {
                return JsonSerializer.Deserialize<List<FilterMapping>>(json) ?? new List<FilterMapping>();
            } catch (JsonException) {
                return new List<FilterMapping>();
            }
        }
        set => accessor.SetValueString(nameof(Filters), JsonSerializer.Serialize(value));
    }

    public IEnumerable<string> ProfileFilterNames =>
        profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.Select(f => f.Name);

    /// <summary>Blank folders fall back to NINA's image folder, and an export folder inside it.</summary>
    public ExportSettings ToExportSettings() {
        var imageFolder = string.IsNullOrWhiteSpace(ImageFolder)
            ? profileService.ActiveProfile.ImageFileSettings.FilePath
            : ImageFolder;
        var exportFolder = string.IsNullOrWhiteSpace(ExportFolder)
            ? Path.Combine(imageFolder ?? string.Empty, ExportFolderName)
            : ExportFolder;
        return new ExportSettings {
            ImageFolder = imageFolder ?? string.Empty,
            ExportFolder = exportFolder,
            Bortle = Bortle,
            Filters = Filters,
        };
    }
}
