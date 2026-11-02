using System.ComponentModel.Composition;
using AcquisitionExport.Core;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;

namespace NinaAcquisitionExport;

/// <summary>
/// A sequence instruction that exports the surrounding target's CSV. A failed export
/// only warns, because paperwork shouldn't stop a night's sequence from finishing.
/// </summary>
[ExportMetadata("Name", "Export Acquisition Data")]
[ExportMetadata("Description", "Writes an AstroBin CSV from this target's light frames across all nights.")]
[ExportMetadata("Icon", "SaveSVG")]
[ExportMetadata("Category", "Acquisition Export")]
[Export(typeof(ISequenceItem))]
[JsonObject(MemberSerialization.OptIn)]
public class ExportAcquisitionData : SequenceItem {
    private readonly IProfileService profileService;
    private string targetNames = string.Empty;

    [ImportingConstructor]
    public ExportAcquisitionData(IProfileService profileService) {
        this.profileService = profileService;
    }

    public ExportAcquisitionData(ExportAcquisitionData copyMe) : this(copyMe.profileService) {
        CopyMetaData(copyMe);
        TargetNames = copyMe.TargetNames;
    }

    /// <summary>Blank uses the target of the sequence block this instruction is in.</summary>
    [JsonProperty]
    public string TargetNames {
        get => targetNames;
        set {
            targetNames = value;
            RaisePropertyChanged();
        }
    }

    public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
        var names = ExportReport.SplitNames(TargetNames);
        if (names.Count == 0 && ContextTargetName() is { Length: > 0 } contextName) {
            names = new[] { contextName };
        }
        if (names.Count == 0) {
            Notification.ShowWarning("Export Acquisition Data has no target. Place it inside a target block or enter a target name.");
            return;
        }

        var status = new Progress<string>(message => progress?.Report(new ApplicationStatus { Source = Category, Status = message }));
        try {
            var settings = new PluginSettings(profileService).ToExportSettings();
            var result = await Task.Run(() => AcquisitionExporter.Export(settings, names, status, token), token);
            var summary = ExportReport.Summary(result, names);
            Logger.Info(summary);
            if (result.WroteFiles && result.Warnings.Count == 0) {
                Notification.ShowSuccess(summary);
            } else {
                Notification.ShowWarning(summary);
            }
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception e) {
            Logger.Error(e);
            Notification.ShowWarning($"Export Acquisition Data failed: {e.Message}");
        } finally {
            progress?.Report(new ApplicationStatus { Source = Category, Status = string.Empty });
        }
    }

    public override object Clone() => new ExportAcquisitionData(this);

    public override string ToString() => $"Category: {Category}, Item: {nameof(ExportAcquisitionData)}, TargetNames: {TargetNames}";

    private string? ContextTargetName() {
        for (var container = Parent; container != null; container = container.Parent) {
            if (container is IDeepSkyObjectContainer deepSky && !string.IsNullOrWhiteSpace(deepSky.Target?.TargetName)) {
                return deepSky.Target.TargetName;
            }
        }
        return null;
    }
}
