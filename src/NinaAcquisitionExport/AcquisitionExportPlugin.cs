using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AcquisitionExport.Core;
using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;

namespace NinaAcquisitionExport;

/// <summary>The plugin itself, and the data behind its page in NINA's plugin options.</summary>
[Export(typeof(IPluginManifest))]
public class AcquisitionExportPlugin : PluginBase, INotifyPropertyChanged {
    private readonly IProfileService profileService;
    private readonly PluginSettings settings;
    private string exportTargetNames = string.Empty;
    private string exportStatus = string.Empty;
    private bool isExporting;
    private bool isLoadingFilters;

    [ImportingConstructor]
    public AcquisitionExportPlugin(IProfileService profileService) {
        this.profileService = profileService;
        settings = new PluginSettings(profileService);
        FilterRows = new ObservableCollection<FilterRow>();
        FilterRows.CollectionChanged += FilterRows_CollectionChanged;
        LoadProfileFiltersCommand = new DelegateCommand(LoadProfileFilters);
        ExportCommand = new DelegateCommand(StartExport, () => !isExporting);
        profileService.ProfileChanged += ProfileService_ProfileChanged;
        LoadFilterRows();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FilterRow> FilterRows { get; }

    public ICommand LoadProfileFiltersCommand { get; }

    public ICommand ExportCommand { get; }

    public string ImageFolder {
        get => settings.ImageFolder;
        set { settings.ImageFolder = value; Raise(); }
    }

    public string ExportFolder {
        get => settings.ExportFolder;
        set { settings.ExportFolder = value; Raise(); }
    }

    public int Bortle {
        get => settings.Bortle;
        set { settings.Bortle = Math.Clamp(value, 1, 9); Raise(); }
    }

    public string ExportTargetNames {
        get => exportTargetNames;
        set { exportTargetNames = value; Raise(); }
    }

    public string ExportStatus {
        get => exportStatus;
        private set { exportStatus = value; Raise(); }
    }

    public override Task Teardown() {
        profileService.ProfileChanged -= ProfileService_ProfileChanged;
        return base.Teardown();
    }

    private async void StartExport() {
        var names = ExportReport.SplitNames(ExportTargetNames);
        isExporting = true;
        CommandManager.InvalidateRequerySuggested();
        ExportStatus = "Exporting…";
        try {
            var exportSettings = settings.ToExportSettings();
            var result = await Task.Run(() => AcquisitionExporter.Export(exportSettings, names));
            ExportStatus = ExportReport.Summary(result, names);
        } catch (Exception e) {
            Logger.Error(e);
            ExportStatus = $"Export failed: {e.Message}";
        } finally {
            isExporting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Adds any filter from the active profile that isn't in the table yet.</summary>
    private void LoadProfileFilters() {
        foreach (var name in settings.ProfileFilterNames) {
            if (!FilterRows.Any(r => string.Equals(r.NinaFilter, name, StringComparison.OrdinalIgnoreCase))) {
                FilterRows.Add(new FilterRow { NinaFilter = name });
            }
        }
    }

    private void LoadFilterRows() {
        isLoadingFilters = true;
        FilterRows.Clear();
        foreach (var mapping in settings.Filters) {
            FilterRows.Add(FilterRow.From(mapping));
        }
        isLoadingFilters = false;
        if (FilterRows.Count == 0) {
            LoadProfileFilters();
        }
    }

    private void FilterRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        foreach (FilterRow row in e.NewItems ?? Array.Empty<FilterRow>()) {
            row.PropertyChanged += FilterRow_PropertyChanged;
        }
        foreach (FilterRow row in e.OldItems ?? Array.Empty<FilterRow>()) {
            row.PropertyChanged -= FilterRow_PropertyChanged;
        }
        SaveFilters();
    }

    private void FilterRow_PropertyChanged(object? sender, PropertyChangedEventArgs e) => SaveFilters();

    private void SaveFilters() {
        if (!isLoadingFilters) {
            settings.Filters = FilterRows.Select(r => r.ToMapping()).ToList();
        }
    }

    private void ProfileService_ProfileChanged(object? sender, EventArgs e) {
        LoadFilterRows();
        foreach (var name in new[] { nameof(ImageFolder), nameof(ExportFolder), nameof(Bortle) }) {
            Raise(name);
        }
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
