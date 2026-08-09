using System.Reflection;
using System.Runtime.InteropServices;

// The Win32 version resource. Without these the exe ships as 0.0.0.0 with no company, product or
// description, which is a strong heuristic signal for antivirus scanners - real software carries
// this information and metadata-less binaries usually do not. Costs nothing to include.
//
// AssemblyVersion is the single source of the version string; AppInfo.Version reads it back.

[assembly: AssemblyTitle("RRACF")]
[assembly: AssemblyDescription("Converts a replacer camo mod into an ACF slot mod for Metal Gear Solid Delta: Snake Eater")]
[assembly: AssemblyCompany("peepoNuggie")]
[assembly: AssemblyProduct("RRACF - Replacer to ACF Slot Converter")]
[assembly: AssemblyCopyright("Copyright (c) 2026 peepoNuggie. MIT licence.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]
