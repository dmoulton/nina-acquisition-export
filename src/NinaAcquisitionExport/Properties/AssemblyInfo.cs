using System.Reflection;
using System.Runtime.InteropServices;

// Identifies the plugin to NINA. Must stay the same between versions; PluginSettings.PluginId matches it.
[assembly: Guid("33703962-7ee2-43ef-9dfd-91e2ead26fe0")]

// Increase for each release.
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]

// The options page's DataTemplate key in Options.xaml is built from this title.
[assembly: AssemblyTitle("Acquisition Export")]
[assembly: AssemblyDescription("Exports AstroBin acquisition CSVs from a target's light frames.")]
[assembly: AssemblyCompany("David Moulton")]
[assembly: AssemblyProduct("Acquisition Export")]
[assembly: AssemblyCopyright("Copyright © 2026 David Moulton")]

[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("Repository", "https://github.com/dmoulton/nina-acquisition-export")]
[assembly: AssemblyMetadata("Tags", "AstroBin,Export,Acquisition")]
[assembly: AssemblyMetadata("LongDescription", @"Reads the FITS headers of a target's saved light frames across all nights and writes an AstroBin acquisition CSV. Nothing is uploaded.

Set your folders, Bortle value, and AstroBin filter IDs on this page. Export a target with the button here, or add the Export Acquisition Data instruction to a sequence.")]

[assembly: ComVisible(false)]
