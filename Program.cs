// Kimi — fast code line counter, directory profiler, and code growth tracker.
// Install:
//   cp bin/Release/net*/linux-x64/publish/Kimi ~/.local/bin/kimi
//   chmod +x ~/.local/bin/kimi
//
// Storage:
//   ~/.config/kimi/config.json
//   ~/.local/share/kimi/snapshots.jsonl
//   ~/.local/share/kimi/state.json

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KimiCli;

public sealed class KimiConfig
{
    [JsonPropertyName("tracked_paths")]
    public List<string> TrackedPaths { get; set; } = new();

    [JsonPropertyName("scan_interval_minutes")]
    public int ScanIntervalMinutes { get; set; } = 15;

    [JsonPropertyName("max_file_size_bytes")]
    public long MaxFileSizeBytes { get; set; } = 25_000_000;

    [JsonPropertyName("ignore_dirs")]
    public List<string> IgnoreDirs { get; set; } = new()
    {
        ".git", ".hg", ".svn", "bin", "obj", "node_modules", ".venv", "venv", "__pycache__",
        ".mypy_cache", ".pytest_cache", "target", "build", "dist", ".cache", ".idea", ".vs", ".vscode"
    };

    [JsonPropertyName("ignore_extensions")]
    public List<string> IgnoreExtensions { get; set; } = new()
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".ico", ".pdf", ".zip", ".gz", ".tar", ".7z",
        ".rar", ".exe", ".dll", ".so", ".dylib", ".o", ".a", ".class", ".jar", ".pdb", ".mp3", ".mp4", ".mov"
    };
}

public sealed class FileLineReport
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "Unknown";

    [JsonPropertyName("total_lines")]
    public long TotalLines { get; set; }

    [JsonPropertyName("code_lines")]
    public long CodeLines { get; set; }

    [JsonPropertyName("blank_lines")]
    public long BlankLines { get; set; }

    [JsonPropertyName("comment_lines")]
    public long CommentLines { get; set; }

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    [JsonPropertyName("modified_at")]
    public string ModifiedAt { get; set; } = "";
}

public sealed class DirectoryLineReport
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "directory";

    [JsonPropertyName("total_lines")]
    public long TotalLines { get; set; }

    [JsonPropertyName("code_lines")]
    public long CodeLines { get; set; }

    [JsonPropertyName("blank_lines")]
    public long BlankLines { get; set; }

    [JsonPropertyName("comment_lines")]
    public long CommentLines { get; set; }

    [JsonPropertyName("files")]
    public int Files { get; set; }

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    [JsonPropertyName("languages")]
    public Dictionary<string, long> Languages { get; set; } = new();

    [JsonPropertyName("top_files")]
    public List<FileLineReport> TopFiles { get; set; } = new();
}

public sealed class KimiSnapshot
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("ts")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("total_lines")]
    public long TotalLines { get; set; }

    [JsonPropertyName("code_lines")]
    public long CodeLines { get; set; }

    [JsonPropertyName("blank_lines")]
    public long BlankLines { get; set; }

    [JsonPropertyName("comment_lines")]
    public long CommentLines { get; set; }

    [JsonPropertyName("files")]
    public int Files { get; set; }

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    [JsonPropertyName("languages")]
    public Dictionary<string, long> Languages { get; set; } = new();

    [JsonPropertyName("top_files")]
    public List<SnapshotFile> TopFiles { get; set; } = new();

    [JsonPropertyName("source")]
    public string Source { get; set; } = "cli";
}

public sealed class SnapshotFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "Unknown";

    [JsonPropertyName("lines")]
    public long Lines { get; set; }
}

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Dictionary<string, string> LanguageByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "C#",
        [".csproj"] = "XML",
        [".sln"] = "Solution",
        [".c"] = "C",
        [".h"] = "C/C++ Header",
        [".cpp"] = "C++",
        [".cc"] = "C++",
        [".cxx"] = "C++",
        [".hpp"] = "C++ Header",
        [".hh"] = "C++ Header",
        [".py"] = "Python",
        [".js"] = "JavaScript",
        [".jsx"] = "JavaScript",
        [".ts"] = "TypeScript",
        [".tsx"] = "TypeScript",
        [".html"] = "HTML",
        [".css"] = "CSS",
        [".scss"] = "SCSS",
        [".json"] = "JSON",
        [".jsonl"] = "JSONL",
        [".xml"] = "XML",
        [".axaml"] = "AXAML",
        [".xaml"] = "XAML",
        [".md"] = "Markdown",
        [".txt"] = "Text",
        [".sh"] = "Shell",
        [".bash"] = "Shell",
        [".fish"] = "Fish",
        [".zsh"] = "Zsh",
        [".rs"] = "Rust",
        [".go"] = "Go",
        [".java"] = "Java",
        [".kt"] = "Kotlin",
        [".swift"] = "Swift",
        [".php"] = "PHP",
        [".rb"] = "Ruby",
        [".lua"] = "Lua",
        [".r"] = "R",
        [".sql"] = "SQL",
        [".yaml"] = "YAML",
        [".yml"] = "YAML",
        [".toml"] = "TOML",
        [".ini"] = "INI",
        [".dockerfile"] = "Dockerfile",
        [".makefile"] = "Makefile"
    };

    public static int Main(string[] args)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            Directory.CreateDirectory(DataDir);
            EnsureDefaultConfig();

            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp();
                return 0;
            }

            string first = args[0];
            string normalized = first.Trim().ToLowerInvariant();
            string[] rest = args.Skip(1).ToArray();

            return normalized switch
            {
                "track" => CmdTrack(rest),
                "untrack" => CmdUntrack(rest),
                "tracked" => CmdTracked(),
                "scan" => CmdScan(rest),
                "top" => CmdTop(rest),
                "month" => CmdMonth(rest),
                "year" => CmdYear(rest),
                "history" => CmdHistory(rest),
                "daemon" => CmdDaemon(),
                "install-service" => CmdInstallService(),
                "uninstall-service" => CmdUninstallService(),
                "status" => CmdStatus(),
                "where" => CmdWhere(),
                "config" => CmdConfig(rest),
                _ => CmdPathMode(args)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"kimi error: {ex.Message}");
            return 1;
        }
    }

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static string ConfigDir => Path.Combine(Home, ".config", "kimi");
    private static string DataDir => Path.Combine(Home, ".local", "share", "kimi");
    private static string ConfigPath => Path.Combine(ConfigDir, "config.json");
    private static string SnapshotsPath => Path.Combine(DataDir, "snapshots.jsonl");
    private static string StatePath => Path.Combine(DataDir, "state.json");
    private static string PidPath => Path.Combine(DataDir, "kimid.pid");

    private static bool IsHelp(string arg) => arg is "help" or "--help" or "-h";

    private static void PrintHelp()
    {
        Console.WriteLine("Kimi — code lines, code mass, and growth tracker");
        Console.WriteLine();
        Console.WriteLine("Immediate reports:");
        Console.WriteLine("  kimi <file> lines");
        Console.WriteLine("  kimi <directory> lines");
        Console.WriteLine("  kimi <path> lines report");
        Console.WriteLine("  kimi scan <path>");
        Console.WriteLine();
        Console.WriteLine("History reports:");
        Console.WriteLine("  kimi <path> lines day");
        Console.WriteLine("  kimi <path> lines month");
        Console.WriteLine("  kimi <path> lines month 2026-04");
        Console.WriteLine("  kimi <path> lines year");
        Console.WriteLine("  kimi <path> lines year 2026");
        Console.WriteLine("  kimi month");
        Console.WriteLine("  kimi year");
        Console.WriteLine("  kimi history <path>");
        Console.WriteLine();
        Console.WriteLine("Tracking service:");
        Console.WriteLine("  kimi track <path>");
        Console.WriteLine("  kimi untrack <path>");
        Console.WriteLine("  kimi tracked");
        Console.WriteLine("  kimi daemon");
        Console.WriteLine("  kimi install-service");
        Console.WriteLine("  systemctl --user enable --now kimid");
        Console.WriteLine();
        Console.WriteLine("Other:");
        Console.WriteLine("  kimi top");
        Console.WriteLine("  kimi status");
        Console.WriteLine("  kimi where");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  kimi Program.cs lines");
        Console.WriteLine("  kimi ~/Documents/scripts/Pulse lines");
        Console.WriteLine("  kimi ~/Documents/scripts/Pulse lines month");
        Console.WriteLine("  kimi track ~/Documents/scripts/Pulse");
    }

    private static int CmdPathMode(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: kimi <path> lines [report|day|month|year]");
            return 2;
        }

        string path = ExpandPath(args[0]);
        string action = args[1].ToLowerInvariant();
        string[] rest = args.Skip(2).ToArray();

        if (action != "lines" && action != "line" && action != "loc")
        {
            Console.Error.WriteLine("usage: kimi <path> lines [report|day|month|year]");
            return 2;
        }

        if (rest.Length == 0 || rest[0].Equals("report", StringComparison.OrdinalIgnoreCase))
        {
            return PrintImmediateReport(path, saveSnapshot: rest.Any(x => x.Equals("--save", StringComparison.OrdinalIgnoreCase)), source: "cli");
        }

        string period = rest[0].ToLowerInvariant();
        string? value = rest.Length > 1 ? rest[1] : null;
        return period switch
        {
            "day" or "today" => CmdPathPeriod(path, "day", value),
            "month" => CmdPathPeriod(path, "month", value),
            "year" => CmdPathPeriod(path, "year", value),
            _ => PrintImmediateReport(path, saveSnapshot: false, source: "cli")
        };
    }

    private static int PrintImmediateReport(string path, bool saveSnapshot, string source)
    {
        var config = LoadConfig();
        if (File.Exists(path))
        {
            var report = CountFile(path, config);
            PrintFileReport(report);
            if (saveSnapshot) AppendSnapshot(ToSnapshot(report, source));
            return 0;
        }
        if (Directory.Exists(path))
        {
            var report = ScanDirectory(path, config);
            PrintDirectoryReport(report);
            if (saveSnapshot) AppendSnapshot(ToSnapshot(report, source));
            return 0;
        }
        Console.Error.WriteLine($"path not found: {path}");
        return 1;
    }

    private static int CmdScan(string[] rest)
    {
        if (rest.Length == 0)
        {
            Console.Error.WriteLine("usage: kimi scan <path>");
            return 2;
        }
        string path = ExpandPath(rest[0]);
        var config = LoadConfig();
        if (File.Exists(path))
        {
            var file = CountFile(path, config);
            var snapshot = ToSnapshot(file, "cli");
            AppendSnapshot(snapshot);
            PrintFileReport(file);
            Console.WriteLine();
            Console.WriteLine("Snapshot saved.");
            return 0;
        }
        if (Directory.Exists(path))
        {
            var dir = ScanDirectory(path, config);
            var snapshot = ToSnapshot(dir, "cli");
            AppendSnapshot(snapshot);
            PrintDirectoryReport(dir);
            Console.WriteLine();
            Console.WriteLine("Snapshot saved.");
            return 0;
        }
        Console.Error.WriteLine($"path not found: {path}");
        return 1;
    }

    private static int CmdTrack(string[] rest)
    {
        if (rest.Length == 0)
        {
            Console.Error.WriteLine("usage: kimi track <path>");
            return 2;
        }
        var config = LoadConfig();
        foreach (var raw in rest)
        {
            string path = ExpandPath(raw);
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Console.Error.WriteLine($"skipping missing path: {path}");
                continue;
            }
            if (!config.TrackedPaths.Contains(path, StringComparer.Ordinal))
            {
                config.TrackedPaths.Add(path);
                Console.WriteLine($"tracking {ShortenPath(path)}");
            }
        }
        SaveConfig(config);
        return 0;
    }

    private static int CmdUntrack(string[] rest)
    {
        if (rest.Length == 0)
        {
            Console.Error.WriteLine("usage: kimi untrack <path>");
            return 2;
        }
        var config = LoadConfig();
        var remove = rest.Select(ExpandPath).ToHashSet(StringComparer.Ordinal);
        int before = config.TrackedPaths.Count;
        config.TrackedPaths = config.TrackedPaths.Where(p => !remove.Contains(p)).ToList();
        SaveConfig(config);
        Console.WriteLine($"removed {before - config.TrackedPaths.Count} tracked path(s)");
        return 0;
    }

    private static int CmdTracked()
    {
        var config = LoadConfig();
        Console.WriteLine("Tracked paths:");
        if (config.TrackedPaths.Count == 0)
        {
            Console.WriteLine("  none");
            return 0;
        }
        foreach (var path in config.TrackedPaths)
        {
            Console.WriteLine($"  {ShortenPath(path)}");
        }
        return 0;
    }

    private static int CmdTop(string[] rest)
    {
        int limit = 10;
        if (rest.Length > 0 && int.TryParse(rest[0], out var parsed)) limit = Math.Max(1, parsed);
        var latest = LatestSnapshotsByPath().Values.OrderByDescending(s => s.TotalLines).Take(limit).ToList();
        if (latest.Count == 0)
        {
            Console.WriteLine("No snapshots yet. Run: kimi scan <path>");
            return 0;
        }
        Console.WriteLine("Kimi Top Code Mass");
        Console.WriteLine();
        int rank = 1;
        foreach (var s in latest)
        {
            Console.WriteLine($"{rank,2}. {s.Label,-42} {s.TotalLines,10:n0} lines  {s.Files,6:n0} files");
            rank++;
        }
        return 0;
    }

    private static int CmdMonth(string[] rest)
    {
        string month = rest.Length > 0 ? rest[0] : DateTimeOffset.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var snaps = ReadSnapshots().Where(s => s.Timestamp.StartsWith(month, StringComparison.Ordinal)).ToList();
        if (snaps.Count == 0)
        {
            Console.WriteLine($"No snapshots for {month}. Run: kimi scan <path>");
            return 0;
        }
        Console.WriteLine($"{MonthLabel(month)} — Kimi Growth");
        Console.WriteLine();
        PrintGrowthSummary(snaps, "month");
        return 0;
    }

    private static int CmdYear(string[] rest)
    {
        string year = rest.Length > 0 ? rest[0] : DateTimeOffset.Now.ToString("yyyy", CultureInfo.InvariantCulture);
        var snaps = ReadSnapshots().Where(s => s.Timestamp.StartsWith(year, StringComparison.Ordinal)).ToList();
        if (snaps.Count == 0)
        {
            Console.WriteLine($"No snapshots for {year}. Run: kimi scan <path>");
            return 0;
        }
        Console.WriteLine($"{year} — Kimi Growth");
        Console.WriteLine();
        PrintGrowthSummary(snaps, "year");
        return 0;
    }

    private static int CmdHistory(string[] rest)
    {
        if (rest.Length == 0)
        {
            Console.Error.WriteLine("usage: kimi history <path>");
            return 2;
        }
        return CmdPathPeriod(ExpandPath(rest[0]), "month", null);
    }

    private static int CmdPathPeriod(string path, string period, string? value)
    {
        string full = ExpandPath(path);
        var snaps = ReadSnapshots()
            .Where(s => SamePath(s.Path, full))
            .OrderBy(s => ParseIso(s.Timestamp))
            .ToList();

        if (snaps.Count == 0)
        {
            Console.WriteLine($"No snapshots for {ShortenPath(full)} yet.");
            Console.WriteLine($"Create one with: kimi scan {QuoteIfNeeded(ShortenPath(full))}");
            return 0;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        if (period == "day")
        {
            string day = value ?? now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var filtered = snaps.Where(s => s.Timestamp.StartsWith(day, StringComparison.Ordinal)).ToList();
            PrintSeries($"{ShortenPath(full)} — {day}", filtered, Bucket.Hour);
            return 0;
        }
        if (period == "month")
        {
            string month = value ?? now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var filtered = snaps.Where(s => s.Timestamp.StartsWith(month, StringComparison.Ordinal)).ToList();
            PrintSeries($"{ShortenPath(full)} — {MonthLabel(month)}", filtered, Bucket.Day);
            return 0;
        }
        if (period == "year")
        {
            string year = value ?? now.ToString("yyyy", CultureInfo.InvariantCulture);
            var filtered = snaps.Where(s => s.Timestamp.StartsWith(year, StringComparison.Ordinal)).ToList();
            PrintSeries($"{ShortenPath(full)} — {year}", filtered, Bucket.Month);
            return 0;
        }
        return 0;
    }

    private enum Bucket { Hour, Day, Month }

    private static void PrintSeries(string title, List<KimiSnapshot> snapshots, Bucket bucket)
    {
        Console.WriteLine(title);
        Console.WriteLine();
        if (snapshots.Count == 0)
        {
            Console.WriteLine("No snapshots in this period.");
            return;
        }

        var grouped = snapshots
            .GroupBy(s => BucketKey(ParseIso(s.Timestamp), bucket))
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(x => ParseIso(x.Timestamp)).Last())
            .ToList();

        foreach (var s in grouped)
        {
            Console.WriteLine($"{BucketLabel(ParseIso(s.Timestamp), bucket),-12} {s.TotalLines,10:n0} lines   {s.CodeLines,10:n0} code");
        }

        long change = grouped.Last().TotalLines - grouped.First().TotalLines;
        Console.WriteLine();
        Console.WriteLine($"Change: {Signed(change)} lines");
    }

    private static string BucketKey(DateTimeOffset ts, Bucket bucket)
    {
        return bucket switch
        {
            Bucket.Hour => ts.ToString("yyyy-MM-dd-HH", CultureInfo.InvariantCulture),
            Bucket.Day => ts.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Bucket.Month => ts.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            _ => ts.ToString("o", CultureInfo.InvariantCulture)
        };
    }

    private static string BucketLabel(DateTimeOffset ts, Bucket bucket)
    {
        return bucket switch
        {
            Bucket.Hour => ts.ToString("HH:00", CultureInfo.InvariantCulture),
            Bucket.Day => ts.ToString("MMM dd", CultureInfo.InvariantCulture),
            Bucket.Month => ts.ToString("MMM", CultureInfo.InvariantCulture),
            _ => ts.ToString("g", CultureInfo.InvariantCulture)
        };
    }

    private static void PrintGrowthSummary(List<KimiSnapshot> snapshots, string period)
    {
        var byPath = snapshots.GroupBy(s => s.Path).Select(g =>
        {
            var ordered = g.OrderBy(s => ParseIso(s.Timestamp)).ToList();
            var first = ordered.First();
            var last = ordered.Last();
            return new
            {
                last.Path,
                last.Label,
                First = first.TotalLines,
                Last = last.TotalLines,
                Change = last.TotalLines - first.TotalLines,
                last.Files
            };
        }).OrderByDescending(x => x.Change).ToList();

        long totalPositive = byPath.Where(x => x.Change > 0).Sum(x => x.Change);
        Console.WriteLine($"You wrote a total of {totalPositive:n0} lines of code this {period}.");
        Console.WriteLine();
        Console.WriteLine("Congrats <3 keep building.");
        Console.WriteLine();
        Console.WriteLine("Top growth:");
        foreach (var item in byPath.Take(10))
        {
            Console.WriteLine($"  {item.Label,-42} {Signed(item.Change),10} lines   now {item.Last,10:n0}");
        }
    }

    private static int CmdDaemon()
    {
        Directory.CreateDirectory(DataDir);
        File.WriteAllText(PidPath, Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            StopRequested = true;
        };

        Console.WriteLine("kimid started");
        while (!StopRequested)
        {
            var config = LoadConfig();
            foreach (var path in config.TrackedPaths.ToList())
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var file = CountFile(path, config);
                        AppendSnapshot(ToSnapshot(file, "daemon"));
                        Console.WriteLine($"snapshot {ShortenPath(path)} {file.TotalLines:n0} lines");
                    }
                    else if (Directory.Exists(path))
                    {
                        var dir = ScanDirectory(path, config);
                        AppendSnapshot(ToSnapshot(dir, "daemon"));
                        Console.WriteLine($"snapshot {ShortenPath(path)} {dir.TotalLines:n0} lines");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"scan failed {path}: {ex.Message}");
                }
            }

            int sleepMinutes = Math.Max(1, config.ScanIntervalMinutes);
            for (int i = 0; i < sleepMinutes * 60 && !StopRequested; i++)
            {
                Thread.Sleep(1000);
            }
        }

        try { File.Delete(PidPath); } catch { }
        Console.WriteLine("kimid stopped");
        return 0;
    }

    private static bool StopRequested;

    private static int CmdInstallService()
    {
        string userSystemd = Path.Combine(Home, ".config", "systemd", "user");
        Directory.CreateDirectory(userSystemd);
        string servicePath = Path.Combine(userSystemd, "kimid.service");
        string exe = Environment.ProcessPath ?? "kimi";
        string service = $"""
[Unit]
Description=Kimi code line tracker
After=default.target

[Service]
Type=simple
ExecStart={exe} daemon
Restart=on-failure
RestartSec=5

[Install]
WantedBy=default.target
""";
        File.WriteAllText(servicePath, service);
        RunQuiet("systemctl", "--user", "daemon-reload");
        Console.WriteLine($"installed {servicePath}");
        Console.WriteLine("enable with: systemctl --user enable --now kimid");
        return 0;
    }

    private static int CmdUninstallService()
    {
        RunQuiet("systemctl", "--user", "disable", "--now", "kimid");
        string servicePath = Path.Combine(Home, ".config", "systemd", "user", "kimid.service");
        try { File.Delete(servicePath); } catch { }
        RunQuiet("systemctl", "--user", "daemon-reload");
        Console.WriteLine("removed kimid user service");
        return 0;
    }

    private static int CmdStatus()
    {
        var config = LoadConfig();
        Console.WriteLine("Kimi status");
        Console.WriteLine($"config:    {ConfigPath}");
        Console.WriteLine($"snapshots: {SnapshotsPath}");
        Console.WriteLine($"state:     {StatePath}");
        if (File.Exists(PidPath)) Console.WriteLine($"daemon:    possibly running pid {File.ReadAllText(PidPath).Trim()}");
        else Console.WriteLine("daemon:    no pid file found");
        Console.WriteLine($"interval:  {config.ScanIntervalMinutes} minute(s)");
        Console.WriteLine();
        Console.WriteLine("tracked paths:");
        if (config.TrackedPaths.Count == 0) Console.WriteLine("  none");
        foreach (var path in config.TrackedPaths) Console.WriteLine($"  {ShortenPath(path)}");
        return 0;
    }

    private static int CmdWhere()
    {
        Console.WriteLine($"config:    {ConfigPath}");
        Console.WriteLine($"snapshots: {SnapshotsPath}");
        Console.WriteLine($"state:     {StatePath}");
        return 0;
    }

    private static int CmdConfig(string[] rest)
    {
        var config = LoadConfig();
        if (rest.Length == 0)
        {
            Console.WriteLine(JsonSerializer.Serialize(config, PrettyJsonOptions));
            return 0;
        }

        if (rest.Length >= 2 && rest[0].Equals("interval", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(rest[1], out var minutes) || minutes < 1)
            {
                Console.Error.WriteLine("usage: kimi config interval <minutes>");
                return 2;
            }
            config.ScanIntervalMinutes = minutes;
            SaveConfig(config);
            Console.WriteLine($"scan_interval_minutes = {minutes}");
            return 0;
        }

        Console.Error.WriteLine("usage: kimi config interval <minutes>");
        return 2;
    }

    private static FileLineReport CountFile(string path, KimiConfig config)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException(path);
        if (info.Length > config.MaxFileSizeBytes) throw new InvalidOperationException($"file too large: {path}");

        long total = 0;
        long blank = 0;
        long comment = 0;
        long code = 0;
        string language = DetectLanguage(path);

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.SequentialScan);
        if (LooksBinary(fs)) throw new InvalidOperationException($"binary file skipped: {path}");
        fs.Position = 0;
        using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024, leaveOpen: false);

        string? line;
        bool inBlockComment = false;
        while ((line = reader.ReadLine()) is not null)
        {
            total++;
            string t = line.Trim();
            if (t.Length == 0)
            {
                blank++;
                continue;
            }

            bool isComment = IsCommentLine(t, language, ref inBlockComment);
            if (isComment) comment++;
            else code++;
        }

        return new FileLineReport
        {
            Path = Path.GetFullPath(path),
            Label = ShortenPath(Path.GetFullPath(path)),
            Language = language,
            TotalLines = total,
            CodeLines = code,
            BlankLines = blank,
            CommentLines = comment,
            Bytes = info.Length,
            ModifiedAt = ToIso(info.LastWriteTime)
        };
    }

    private static DirectoryLineReport ScanDirectory(string path, KimiConfig config)
    {
        string full = Path.GetFullPath(path);
        var files = EnumerateCodeFiles(full, config).ToList();
        var bag = new ConcurrentBag<FileLineReport>();

        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
        {
            try
            {
                bag.Add(CountFile(file, config));
            }
            catch
            {
                // Skip unreadable, binary, or too-large files. Kimi should keep moving.
            }
        });

        var reports = bag.ToList();
        var dir = new DirectoryLineReport
        {
            Path = full,
            Label = ShortenPath(full),
            Kind = "directory",
            Files = reports.Count,
            TotalLines = reports.Sum(r => r.TotalLines),
            CodeLines = reports.Sum(r => r.CodeLines),
            BlankLines = reports.Sum(r => r.BlankLines),
            CommentLines = reports.Sum(r => r.CommentLines),
            Bytes = reports.Sum(r => r.Bytes),
            Languages = reports.GroupBy(r => r.Language).OrderByDescending(g => g.Sum(x => x.TotalLines)).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalLines)),
            TopFiles = reports.OrderByDescending(r => r.TotalLines).Take(15).ToList()
        };
        return dir;
    }

    private static IEnumerable<string> EnumerateCodeFiles(string root, KimiConfig config)
    {
        var ignoreDirs = config.IgnoreDirs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ignoreExt = config.IgnoreExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            IEnumerable<string> subdirs;
            try { subdirs = Directory.EnumerateDirectories(dir); }
            catch { continue; }

            foreach (var sub in subdirs)
            {
                string name = Path.GetFileName(sub);
                if (ignoreDirs.Contains(name)) continue;
                stack.Push(sub);
            }

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir); }
            catch { continue; }

            foreach (var file in files)
            {
                string ext = ExtensionFor(file);
                if (ignoreExt.Contains(ext)) continue;
                if (LanguageByExtension.ContainsKey(ext) || IsInterestingNoExtension(file)) yield return file;
            }
        }
    }

    private static bool LooksBinary(FileStream fs)
    {
        Span<byte> buffer = stackalloc byte[4096];
        int read = fs.Read(buffer);
        for (int i = 0; i < read; i++)
        {
            if (buffer[i] == 0) return true;
        }
        return false;
    }

    private static bool IsCommentLine(string trimmed, string language, ref bool inBlock)
    {
        if (inBlock)
        {
            if (trimmed.Contains("*/")) inBlock = false;
            return true;
        }

        if (language is "Python" or "Shell" or "Fish" or "Zsh" or "Ruby" or "R" or "YAML" or "TOML" or "INI")
        {
            return trimmed.StartsWith("#", StringComparison.Ordinal);
        }

        if (language is "SQL")
        {
            return trimmed.StartsWith("--", StringComparison.Ordinal);
        }

        if (language is "HTML" or "XML" or "AXAML" or "XAML" or "Markdown")
        {
            return trimmed.StartsWith("<!--", StringComparison.Ordinal);
        }

        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return true;
        if (trimmed.StartsWith("/*", StringComparison.Ordinal))
        {
            if (!trimmed.Contains("*/")) inBlock = true;
            return true;
        }
        return false;
    }

    private static string DetectLanguage(string path)
    {
        string name = Path.GetFileName(path).ToLowerInvariant();
        if (name == "makefile") return "Makefile";
        if (name == "dockerfile") return "Dockerfile";
        string ext = ExtensionFor(path);
        return LanguageByExtension.TryGetValue(ext, out var lang) ? lang : "Text";
    }

    private static string ExtensionFor(string path)
    {
        string name = Path.GetFileName(path);
        if (name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)) return ".dockerfile";
        if (name.Equals("Makefile", StringComparison.OrdinalIgnoreCase)) return ".makefile";
        if (name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) return ".csproj";
        return Path.GetExtension(path).ToLowerInvariant();
    }

    private static bool IsInterestingNoExtension(string path)
    {
        string name = Path.GetFileName(path).ToLowerInvariant();
        return name is "makefile" or "dockerfile" or "readme" or "license";
    }

    private static void PrintFileReport(FileLineReport report)
    {
        Console.WriteLine(report.Label);
        Console.WriteLine();
        Console.WriteLine($"Language:       {report.Language}");
        Console.WriteLine($"Total lines:    {report.TotalLines,10:n0}");
        Console.WriteLine($"Code lines:     {report.CodeLines,10:n0}");
        Console.WriteLine($"Blank lines:    {report.BlankLines,10:n0}");
        Console.WriteLine($"Comment lines:  {report.CommentLines,10:n0}");
        Console.WriteLine($"Bytes:          {report.Bytes,10:n0}");
    }

    private static void PrintDirectoryReport(DirectoryLineReport report)
    {
        Console.WriteLine($"{report.Label} — Directory Lines");
        Console.WriteLine();
        Console.WriteLine($"Total lines:    {report.TotalLines,10:n0}");
        Console.WriteLine($"Code lines:     {report.CodeLines,10:n0}");
        Console.WriteLine($"Blank lines:    {report.BlankLines,10:n0}");
        Console.WriteLine($"Comment lines:  {report.CommentLines,10:n0}");
        Console.WriteLine($"Files:          {report.Files,10:n0}");
        Console.WriteLine($"Bytes:          {report.Bytes,10:n0}");
        Console.WriteLine();
        Console.WriteLine("Languages:");
        foreach (var kv in report.Languages.OrderByDescending(x => x.Value).Take(12))
        {
            Console.WriteLine($"  {kv.Key,-18} {kv.Value,10:n0} lines");
        }
        Console.WriteLine();
        Console.WriteLine("Top files:");
        foreach (var f in report.TopFiles.Take(10))
        {
            Console.WriteLine($"  {f.Label,-58} {f.TotalLines,10:n0}");
        }
    }

    private static KimiSnapshot ToSnapshot(FileLineReport report, string source)
    {
        return new KimiSnapshot
        {
            Id = NewId(),
            Timestamp = ToIso(DateTimeOffset.Now),
            Path = report.Path,
            Label = report.Label,
            Kind = "file",
            TotalLines = report.TotalLines,
            CodeLines = report.CodeLines,
            BlankLines = report.BlankLines,
            CommentLines = report.CommentLines,
            Files = 1,
            Bytes = report.Bytes,
            Languages = new Dictionary<string, long> { [report.Language] = report.TotalLines },
            TopFiles = new List<SnapshotFile>
            {
                new() { Path = report.Path, Label = report.Label, Language = report.Language, Lines = report.TotalLines }
            },
            Source = source
        };
    }

    private static KimiSnapshot ToSnapshot(DirectoryLineReport report, string source)
    {
        return new KimiSnapshot
        {
            Id = NewId(),
            Timestamp = ToIso(DateTimeOffset.Now),
            Path = report.Path,
            Label = report.Label,
            Kind = "directory",
            TotalLines = report.TotalLines,
            CodeLines = report.CodeLines,
            BlankLines = report.BlankLines,
            CommentLines = report.CommentLines,
            Files = report.Files,
            Bytes = report.Bytes,
            Languages = report.Languages,
            TopFiles = report.TopFiles.Take(15).Select(f => new SnapshotFile
            {
                Path = f.Path,
                Label = f.Label,
                Language = f.Language,
                Lines = f.TotalLines
            }).ToList(),
            Source = source
        };
    }

    private static void AppendSnapshot(KimiSnapshot snapshot)
    {
        Directory.CreateDirectory(DataDir);
        using var stream = new FileStream(SnapshotsPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.WriteLine(JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    private static IEnumerable<KimiSnapshot> ReadSnapshots()
    {
        if (!File.Exists(SnapshotsPath)) yield break;
        foreach (var line in File.ReadLines(SnapshotsPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            KimiSnapshot? snap = null;
            try { snap = JsonSerializer.Deserialize<KimiSnapshot>(line, JsonOptions); }
            catch { }
            if (snap is not null) yield return snap;
        }
    }

    private static Dictionary<string, KimiSnapshot> LatestSnapshotsByPath()
    {
        return ReadSnapshots()
            .GroupBy(s => s.Path)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => ParseIso(x.Timestamp)).Last());
    }

    private static KimiConfig LoadConfig()
    {
        EnsureDefaultConfig();
        try
        {
            return JsonSerializer.Deserialize<KimiConfig>(File.ReadAllText(ConfigPath), JsonOptions) ?? new KimiConfig();
        }
        catch
        {
            return new KimiConfig();
        }
    }

    private static void SaveConfig(KimiConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, PrettyJsonOptions));
    }

    private static void EnsureDefaultConfig()
    {
        if (!File.Exists(ConfigPath)) SaveConfig(new KimiConfig());
    }

    private static string ExpandPath(string raw)
    {
        string p = raw;
        if (p == "~") p = Home;
        else if (p.StartsWith("~/", StringComparison.Ordinal)) p = Path.Combine(Home, p[2..]);
        return Path.GetFullPath(p);
    }

    private static string ShortenPath(string path)
    {
        string full = Path.GetFullPath(path);
        if (full == Home) return "~";
        if (full.StartsWith(Home + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return "~" + full[Home.Length..];
        return full;
    }

    private static string QuoteIfNeeded(string value)
    {
        return value.Contains(' ') ? $"\"{value}\"" : value;
    }

    private static bool SamePath(string a, string b)
    {
        return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.Ordinal);
    }

    private static string NewId()
    {
        return $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}-{Guid.NewGuid():N}"[..28];
    }

    private static string ToIso(DateTimeOffset ts)
    {
        return ts.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseIso(string iso)
    {
        return DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
    }

    private static string ToIso(DateTime dt)
    {
        return new DateTimeOffset(dt).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    private static string MonthLabel(string yyyyMm)
    {
        var dt = DateTime.ParseExact(yyyyMm + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return dt.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
    }

    private static string Signed(long value)
    {
        return value >= 0 ? $"+{value:n0}" : value.ToString("n0", CultureInfo.InvariantCulture);
    }

    private static void RunQuiet(string fileName, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);
            using var p = Process.Start(psi);
            p?.WaitForExit();
        }
        catch { }
    }
}
