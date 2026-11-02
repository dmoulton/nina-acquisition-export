using System.Globalization;
using System.Text;
using AcquisitionExport.Core;

// A dependency-free test runner, so these tests run anywhere with only the .NET SDK:
//   dotnet run --project tests/AcquisitionExport.Core.Tests
CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // Output must not depend on the PC's locale.

var failures = 0;
void Test(string name, Action body) {
    try {
        body();
        Console.WriteLine($"pass  {name}");
    } catch (Exception e) {
        failures++;
        Console.WriteLine($"FAIL  {name}\n      {e.Message}");
    }
}
void Equal(object? expected, object? actual) {
    if (!Equals(expected, actual)) {
        throw new Exception($"expected <{expected}> but got <{actual}>");
    }
}

// Writes a header the way NINA 3.2 formats its cards.
static byte[] FitsBytes(params (string Key, string Value)[] cards) {
    var header = new StringBuilder();
    void Card(string key, string value) => header.Append($"{key,-8}= {value}".PadRight(80));
    Card("SIMPLE", "T");
    foreach (var (key, value) in cards) {
        Card(key, value);
    }
    header.Append("END".PadRight(80));
    while (header.Length % 2880 != 0) {
        header.Append(' ');
    }
    return Encoding.ASCII.GetBytes(header.ToString());
}
static string Text(string value) => $"'{value.Replace("'", "''")}' / a comment";
static (string, string)[] Light(string target, string filter, double exposure, string local, int gain = 30, double temp = -10) => new[] {
    ("NAXIS", "2"),
    ("IMAGETYP", "'LIGHT   '"),
    ("EXPOSURE", exposure.ToString(CultureInfo.InvariantCulture) + " / [s] Exposure duration"),
    ("DATE-LOC", Text(local + ".1234567")),
    ("XBINNING", "1"),
    ("GAIN", gain.ToString(CultureInfo.InvariantCulture)),
    ("CCD-TEMP", temp.ToString(CultureInfo.InvariantCulture) + " / [degC] CCD temperature"),
    ("FILTER", Text(filter)),
    ("OBJECT", Text(target)),
};

var settings = new ExportSettings {
    Bortle = 6,
    Filters = new() {
        new() { NinaFilter = "R", AstroBinId = "32342" },
        new() { NinaFilter = "G", AstroBinId = "32343" },
        new() { NinaFilter = "B", AstroBinId = "32344" },
        new() { NinaFilter = "H", AstroBinId = "43627" },
        new() { NinaFilter = "O", AstroBinId = "43628" },
    },
};
Frame F(string filter, double seconds, string local, int gain = 30, double temp = -10) =>
    new("M20", filter, seconds, DateTime.Parse(local, CultureInfo.InvariantCulture), gain, 1, temp);

Test("reads quoted text, numbers, and comments", () => {
    var h = FitsHeader.Read(new MemoryStream(FitsBytes(Light("O'Brien's Nebula", "H", 480, "2026-06-23T23:01:24"))));
    Equal("O'Brien's Nebula", h.GetString("OBJECT"));
    Equal(480d, h.GetDouble("EXPOSURE"));
    Equal(new DateTime(2026, 6, 23, 23, 1, 24).Date, h.GetDateTime("DATE-LOC")!.Value.Date);
});

Test("reads the second unit of a compressed file", () => {
    var compressed = FitsBytes(("NAXIS", "0")).Concat(FitsBytes(Light("M20", "H", 480, "2026-06-23T23:01:24"))).ToArray();
    Equal("M20", FitsHeader.Read(new MemoryStream(compressed)).GetString("OBJECT"));
});

Test("rejects a file that isn't FITS", () => {
    try {
        FitsHeader.Read(new MemoryStream(new byte[4096]));
        throw new Exception("no exception");
    } catch (InvalidDataException) { }
});

Test("keeps a night together across midnight", () => {
    Equal(new DateOnly(2026, 6, 13), F("H", 480, "2026-06-13T23:45:00").Night);
    Equal(new DateOnly(2026, 6, 13), F("H", 480, "2026-06-14T04:10:00").Night);
    Equal(new DateOnly(2026, 6, 12), F("H", 480, "2026-06-13T00:20:00").Night);
});

Test("groups CSV rows by night, filter, and exposure", () => {
    var csv = AcquisitionWriters.AstroBinCsv(new[] {
        F("H", 480, "2026-06-13T23:45:00", temp: -10.2),
        F("H", 480, "2026-06-14T01:00:00", temp: -9.8),
        F("R", 120, "2026-06-13T23:40:00"),
        F("R", 30, "2026-06-13T23:35:00"),
        F("O", 480, "2026-06-14T23:30:00"),
    }, settings);
    Equal(string.Join('\n',
        "date,filter,number,duration,binning,gain,sensorCooling,bortle",
        "2026-06-13,32342,1,30,1,30,-10,6",
        "2026-06-13,32342,1,120,1,30,-10,6",
        "2026-06-13,43627,2,480,1,30,-10,6",
        "2026-06-14,43628,1,480,1,30,-10,6",
        ""), csv);
});

Test("keeps different gains on separate rows", () => {
    var csv = AcquisitionWriters.AstroBinCsv(new[] { F("H", 480, "2026-06-13T23:00:00", gain: 30), F("H", 480, "2026-06-13T23:10:00", gain: 56) }, settings);
    Equal(3, csv.Trim().Split('\n').Length);
});

Test("exports only matching light frames across night folders", () => {
    var root = Path.Combine(Path.GetTempPath(), "acqexp-" + Guid.NewGuid());
    try {
        void Write(string folder, string name, byte[] bytes) {
            var dir = Path.Combine(root, folder);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name), bytes);
        }
        Write("2026-06-13/M20/LIGHT", "a.fits", FitsBytes(Light("M20", "H", 480, "2026-06-13T23:45:00")));
        Write("2026-06-13/M20/LIGHT", "b.fits", FitsBytes(Light("m20", "H", 480, "2026-06-14T01:00:00")));
        Write("2026-06-14/Trifid/LIGHT", "c.fit", FitsBytes(Light("Trifid", "S", 300, "2026-06-14T23:30:00")));
        Write("2026-06-14/M8/LIGHT", "d.fits", FitsBytes(Light("M8", "H", 480, "2026-06-14T23:30:00")));
        var flat = Light("M20", "H", 2, "2026-06-14T23:30:00");
        flat[1] = ("IMAGETYP", "'FLAT    '");
        Write("2026-06-14/M20/FLAT", "e.fits", FitsBytes(flat));
        // NINA 3.2 writes IMAGETYP = LIGHT for snapshots, so only the folder identifies them.
        Write("2026-06-14/M20/SNAPSHOT", "f.fits", FitsBytes(Light("M20", "H", 2, "2026-06-14T23:30:00")));
        Write("2026-06-14/M20/LIGHT", "broken.fits", new byte[100]);
        Write("2026-06-14/M20/LIGHT", "g.xisf", new byte[100]);
        Write("2026-06-14/M20/LIGHT", "notes.txt", new byte[100]);

        settings.ImageFolder = root;
        settings.ExportFolder = Path.Combine(root, "exports");
        var result = AcquisitionExporter.Export(settings, new[] { "M20", "Trifid" });

        Equal(3, result.FrameCount);
        Equal(2, result.NightCount);
        var csv = File.ReadAllText(result.CsvPath!);
        Equal(true, csv.Contains("2026-06-13,43627,2,480,1,30,-10,6\n"));
        Equal(true, csv.Contains("2026-06-14,,1,300,1,30,-10,6\n"));
        Equal(true, result.Warnings.Any(w => w.Contains("No AstroBin ID for S")));
        Equal(true, result.Warnings.Any(w => w.Contains("1 FITS files couldn't be read")));
        Equal(true, result.Warnings.Any(w => w.Contains("1 XISF")));
        Equal(false, File.Exists(result.CsvPath + ".tmp"));
        Equal(new[] { "M20_astrobin.csv" }.Single(), Directory.GetFiles(settings.ExportFolder).Select(Path.GetFileName).Single());

        // Culling a sub lowers the totals on the next export.
        File.Delete(Path.Combine(root, "2026-06-13/M20/LIGHT/b.fits"));
        result = AcquisitionExporter.Export(settings, new[] { "M20", "Trifid" });
        Equal(2, result.FrameCount);
        Equal(true, File.ReadAllText(result.CsvPath!).Contains("2026-06-13,43627,1,480,1,30,-10,6\n"));

        // A target with no frames doesn't touch anything.
        var none = AcquisitionExporter.Export(settings, new[] { "NGC 1" });
        Equal(false, none.WroteFiles);
        Equal(true, none.Warnings[0].Contains("No light frames"));
        Equal(true, File.Exists(result.CsvPath!));
    } finally {
        if (Directory.Exists(root)) {
            Directory.Delete(root, recursive: true);
        }
    }
});

Test("reports a missing image folder instead of failing", () => {
    var result = AcquisitionExporter.Export(new ExportSettings { ImageFolder = "/does/not/exist", ExportFolder = "/tmp" }, new[] { "M20" });
    Equal(false, result.WroteFiles);
    Equal(true, result.Warnings[0].Contains("doesn't exist"));
});

Console.WriteLine(failures == 0 ? "All tests passed." : $"{failures} tests failed.");
return failures == 0 ? 0 : 1;
