using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FocusCatAI.Shared;

namespace FocusCatAI.Presentation.Services;

/// <summary>Developer diagnostic behind <c>--verify-release</c>: metadata, version consistency, installer and release package.</summary>
internal sealed class ReleaseVerifier
{
    private readonly StringBuilder _log = new();
    private int _passed;
    private int _failed;

    public bool Run()
    {
        Line($"WorkBuddy release verification {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        var root = FindRoot();
        Check("Repository root found", root is not null, AppContext.BaseDirectory);
        if (root is null)
        {
            return Finish();
        }

        var ver = AppInfo.CurrentVersion.ToString(3);
        Line("");
        Line("== Metadata ==");
        var info = FileVersionInfo.GetVersionInfo(Environment.ProcessPath!);
        Check("Product name is WorkBuddy", info.ProductName == "WorkBuddy", info.ProductName);
        Check("Company is " + AppInfo.Publisher, info.CompanyName == AppInfo.Publisher, info.CompanyName);
        Check("File version matches " + ver, info.FileVersion?.StartsWith(ver) == true, info.FileVersion);
        Check("Copyright metadata is present", (info.LegalCopyright ?? "").Contains("Qian Tong Low"), info.LegalCopyright);

        Line("== About dialog data ==");
        Check("Publisher", AppInfo.Publisher == "Qian Tong Low");
        Check("Copyright", AppInfo.Copyright == "Copyright \u00a9 2026 Qian Tong Low", AppInfo.Copyright);
        Check("License summary is MIT", AppInfo.LicenseSummary.Contains("MIT"));
        Check("Repository URL", AppInfo.RepositoryUrl == "https://github.com/WuuGee/WorkBuddy_Utility");

        Line("== Version consistency ==");
        var csproj = Read(root, @"src\FocusCatAI.Presentation\FocusCatAI.Presentation.csproj");
        var iss = Read(root, @"installer\WorkBuddy.iss");
        Check("csproj Version is " + ver, csproj.Contains($"<Version>{ver}</Version>"));
        Check("Installer script default version is " + ver, iss.Contains($"#define AppVersion \"{ver}\""));
        Check("Installer publisher matches About", iss.Contains($"#define AppPublisher \"{AppInfo.Publisher}\""));

        Line("== Repository files ==");
        var license = Read(root, "LICENSE");
        Check("LICENSE is MIT with correct holder", license.Contains("MIT License") && license.Contains("2026 Qian Tong Low"));
        var readme = Read(root, "README.md");
        Check("README mentions installer and MIT", readme.Contains("WorkBuddy-Setup") && readme.Contains("MIT"));
        Check("Release notes exist", File.Exists(Path.Combine(root, "docs", "release", $"RELEASE-NOTES-{ver}.md")));
        Check("RC checklist exists", File.Exists(Path.Combine(root, "docs", "release", "RC-CHECKLIST.md")));
        Check("Pet art assets present", Directory.Exists(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "Assets")));

        Line("== Installer artifacts ==");
        var name = $"WorkBuddy-Setup-{ver}.exe";
        var installer = Path.Combine(root, "artifacts", "installer", name);
        Check("Installer exists", File.Exists(installer));
        Check("Installer is a plausible size (> 10 MB)", File.Exists(installer) && new FileInfo(installer).Length > 10_000_000);
        if (File.Exists(installer))
        {
            var vi = FileVersionInfo.GetVersionInfo(installer);
            Check("Installer version metadata", (vi.FileVersion ?? "").StartsWith(ver) || (vi.ProductVersion ?? "").StartsWith(ver), vi.FileVersion);
        }

        Line("== Release package ==");
        var rel = Path.Combine(root, "artifacts", "release");
        foreach (var f in new[] { name, name + ".sha256", "LICENSE", "RELEASE-NOTES.md" })
        {
            Check("Release contains " + f, File.Exists(Path.Combine(rel, f)));
        }

        var relInstaller = Path.Combine(rel, name);
        var shaFile = Path.Combine(rel, name + ".sha256");
        if (File.Exists(relInstaller) && File.Exists(shaFile))
        {
            using var s = File.OpenRead(relInstaller);
            var actual = Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
            Check("SHA-256 file matches the packaged installer", File.ReadAllText(shaFile).Trim().StartsWith(actual));
            Check("Packaged installer identical to built installer", File.Exists(installer) && new FileInfo(installer).Length == new FileInfo(relInstaller).Length);
        }

        return Finish();
    }

    private static string Read(string root, string rel)
    {
        var p = Path.Combine(root, rel);
        return File.Exists(p) ? File.ReadAllText(p) : "";
    }

    private static string? FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "LICENSE")) && Directory.Exists(Path.Combine(d.FullName, "installer")))
            {
                return d.FullName;
            }
        }

        return null;
    }

    private void Check(string name, bool ok, string? detail = null)
    {
        if (ok) { _passed++; } else { _failed++; }
        Line($"{(ok ? "PASS" : "FAIL")}  {name}" + (!ok && !string.IsNullOrEmpty(detail) ? $"  [{detail}]" : ""));
    }

    private void Line(string text) => _log.AppendLine(text);

    private bool Finish()
    {
        Line("");
        Line($"Result: {(_failed == 0 ? "PASS" : "FAIL")}  ({_passed} passed, {_failed} failed)");
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppConstants.AppName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "release-verification.log"), _log.ToString(), new UTF8Encoding(true));
        return _failed == 0;
    }
}
