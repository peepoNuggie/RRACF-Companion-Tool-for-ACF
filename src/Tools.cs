using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Rracf
{
    internal static class AppInfo
    {
        public const string Version = "1.0";
    }

    internal static class Io
    {
        public static void DeleteDirectory(string path)
        {
            if (!Directory.Exists(path)) return;
            // Clear read-only flags that copied game files sometimes carry.
            foreach (string f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(f, FileAttributes.Normal); }
                catch (Exception) { }
            }
            Directory.Delete(path, true);
        }

        public static string Quote(string s)
        {
            return "\"" + s.TrimEnd('\\') + "\"";
        }
    }

    /// <summary>Locates and runs retoc.exe and repak.exe.</summary>
    internal class Tools
    {
        public const string EngineVersion = "UE5_3";

        public string RetocPath { get; private set; }
        public string RepakPath { get; private set; }
        /// <summary>Where we looked, so a "not found" message can name real paths.</summary>
        public string BaseFolder { get; set; }

        public Tools(string retocPath, string repakPath)
        {
            RetocPath = retocPath == null ? "" : retocPath;
            RepakPath = repakPath == null ? "" : repakPath;
            BaseFolder = "";
        }

        /// <summary>Finds retoc/repak next to the program, in retoc\ and repak\ subfolders, or one level up.</summary>
        public static Tools Discover(string baseFolder)
        {
            string retoc = FindTool(baseFolder, "retoc.exe");
            string repak = FindTool(baseFolder, "repak.exe");
            var t = new Tools(retoc, repak);
            t.BaseFolder = baseFolder;
            return t;
        }

        private static string FindTool(string baseFolder, string exeName)
        {
            string stem = Path.GetFileNameWithoutExtension(exeName);
            var candidates = new[]
            {
                Path.Combine(baseFolder, exeName),
                Path.Combine(Path.Combine(baseFolder, stem), exeName),
                Path.Combine(Path.Combine(baseFolder, ".."), exeName),
                Path.Combine(Path.Combine(Path.Combine(baseFolder, ".."), stem), exeName)
            };
            foreach (string c in candidates)
            {
                if (File.Exists(c)) return Path.GetFullPath(c);
            }
            return "";
        }

        public bool IsReady
        {
            get { return File.Exists(RetocPath) && File.Exists(RepakPath); }
        }

        public void Validate()
        {
            var missing = new System.Collections.Generic.List<string>();
            if (!File.Exists(RetocPath)) missing.Add("retoc.exe");
            if (!File.Exists(RepakPath)) missing.Add("repak.exe");
            if (missing.Count == 0) return;

            string where = BaseFolder.Length > 0 ? BaseFolder : ".";
            throw new InvalidOperationException(
                "RRACF could not find " + string.Join(" or ", missing.ToArray()) + ".\r\n\r\n" +
                "These have to sit next to RRACF.exe:\r\n" +
                "    " + Path.Combine(where, @"retoc\retoc.exe") + "\r\n" +
                "    " + Path.Combine(where, @"repak\repak.exe") + "\r\n\r\n" +
                "Copy the whole retoc and repak folders across, then try again.");
        }

        public string RunRetoc(string[] args, string workingDir, Action<string> log)
        {
            Validate();
            return Run(RetocPath, args, workingDir, log);
        }

        public string RunRepak(string[] args, string workingDir, Action<string> log)
        {
            Validate();
            return Run(RepakPath, args, workingDir, log);
        }

        private static string Run(string exe, string[] args, string workingDir, Action<string> log)
        {
            var sb = new StringBuilder();
            foreach (string a in args)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(a.IndexOf(' ') >= 0 ? Io.Quote(a) : a);
            }
            string arguments = sb.ToString();
            log("  > " + Path.GetFileName(exe) + " " + arguments);

            var psi = new ProcessStartInfo(exe, arguments);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.WorkingDirectory = workingDir;

            var output = new StringBuilder();
            using (var p = Process.Start(psi))
            {
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) lock (output) { output.AppendLine(e.Data); }
                };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) lock (output) { output.AppendLine(e.Data); }
                };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();

                string text;
                lock (output) { text = output.ToString(); }

                if (p.ExitCode != 0)
                    throw new InvalidOperationException(
                        Path.GetFileName(exe) + " failed (exit code " + p.ExitCode + "):" +
                        Environment.NewLine + text.Trim());
                return text;
            }
        }
    }
}
