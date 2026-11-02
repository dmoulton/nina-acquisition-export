# Acquisition Export for N.I.N.A.

A [N.I.N.A.](https://nighttime-imaging.eu/) 3.2 plugin that turns a target's saved light frames into a CSV for AstroBin's acquisition import, such as `M20_astrobin.csv`, with one row per night, filter, exposure, gain, and binning.

It reads the FITS headers of every matching frame, so the file always covers all nights. Delete a bad sub and the next export drops it. Nothing is uploaded anywhere.

> **Still in testing.** This plugin isn't in NINA's plugin directory yet, so it won't appear in NINA's plugin manager. Check your first few CSVs before importing them, and please report problems in [GitHub Issues](https://github.com/dmoulton/nina-acquisition-export/issues).

## Install

1. Download the zip from the newest version on the [Releases page](https://github.com/dmoulton/nina-acquisition-export/releases).
2. Unzip it and put `NinaAcquisitionExport.dll` in `%LOCALAPPDATA%\NINA\Plugins\3.0.0\AcquisitionExport\`, creating that folder if needed. If the `Plugins` folder has a different version folder, use that one.
3. Restart NINA.

To update, close NINA, replace the DLL with the one from a newer release, and start NINA again. The plugin doesn't update itself.

## Set up

In **Options > Plugins > Acquisition Export**:

- **Image folder:** where to look for light frames, including subfolders. Blank uses NINA's image file path.
- **Export folder:** where the CSV goes. Blank uses `Acquisition Exports` inside the image folder.
- **Bortle:** written on every row.
- **Filters:** your AstroBin filter ID for each NINA filter name. A filter with no ID gets a blank in the CSV and a warning.

Settings are saved per NINA profile.

## Export

- **From the options page:** enter the target and click **Export now**. Use commas for alternate names, such as `M20, Trifid`. The first name sets the file name.
- **From a sequence:** add **Export Acquisition Data** to the target's block. Leave its name blank to use that target. A failed export warns but doesn't stop the sequence.

A frame counts if its `OBJECT` matches a target name and its `IMAGETYP` is `LIGHT`. Files in a folder named `SNAPSHOT` are skipped, because NINA also marks snapshots as `LIGHT`. XISF files are skipped with a warning.

Nights run noon to noon, and the CSV uses the date the night began.

## Develop

The export logic in `src/AcquisitionExport.Core` has no NINA or Windows dependencies. Run its tests anywhere with the .NET SDK:

```bash
dotnet run --project tests/AcquisitionExport.Core.Tests
```

The plugin in `src/NinaAcquisitionExport` needs Windows libraries, so GitHub Actions builds it on every push.
