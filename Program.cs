namespace fixeol;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PowerCode;

public class Program
{
  /// <summary>
  /// Main entry point.
  /// </summary>
  /// <param name="arguments"></param>
  /// <returns></returns>
  public static int Main( string[] arguments )
  {

    var app = new Program();
    var cli = new CliArgumentBinder(arguments, "fixeol") {
      AppEnvarPrefix = "fixeol_"
    };

    var result = cli.ParseAndBind(app, allowEnvarValues: true);
    if (result.ShouldExit) {
      return result.ExitCode;
    }

    return app.Run();
  }

  // ===== Common Console Options =====

  [CliArgument(
    namedParameters: ["help", "?", "h"],
    namedCommand: "help",
    description: "Show this help message.",
    valueIsOptional: true,
    order: CliArgumentAttribute.DefaultGlobalOrder
  )]
  public bool Help { get; set; } = false;

  [CliArgument(
    namedParameter: "v",
    description: "Show the version.",
    order: CliArgumentAttribute.DefaultGlobalOrder + 10
  )]
  public bool Version { get; set; } = false;

  [CliArgument(
    namedParameter: "version",
    description: "Show app info and version.",
    order: CliArgumentAttribute.DefaultGlobalOrder + 10
  )]
  public bool VersionFull { get; set; } = false;

  [CliArgument(
    namedParameter: "pause",
    allowEnvar: true,
    description: "Pause when finished.",
    order: CliArgumentAttribute.DefaultGlobalOrder + 40,
    defaultIfMissing: false
  )]
  public bool Pause { get; set; } = false;

  [CliArgument(
    namedParameter: "verbose",
    description: "Output additional details.",
    allowEnvar: true,
    order: CliArgumentAttribute.DefaultGlobalOrder + 50,
    defaultIfMissing: false
  )]
  public bool Verbose { get; set; } = false;

  // ===== App-Specific Options =====

  [CliArgument(
    namedParameter: "dry-run",
    description: "List files whose contents would change without modifying any files.",
    defaultIfMissing: false,
    order: 304
  )]
  public bool DryRun { get; set; } = false;

  /// <summary>
  /// Shows additional details during processing, such as the file being processed and progress
  /// percentage.
  /// </summary>
  [CliArgument(
    namedParameter: "progress",
    allowEnvar: true,
    description: "Output per-file progress.",
    defaultIfMissing: false,
    order: 302
  )]
  public bool ShowProgress { get; set; } = false;

  /// <summary>
  /// Gets or sets whether to scan sub-directories.
  /// </summary>
  [CliArgument(
    namedParameters: ["recursive", "r", "s"],
    allowEnvar: true,
    description: "Apply the file-pattern(s) to the current and all sub-directories.",
    defaultIfMissing: false,
    order: 300
  )]
  public bool Recurse { get; set; } = false;

  /// <summary>
  /// Gets or sets whether to backup the (changed) files.
  /// </summary>
  [CliArgument(
    namedParameter: "backup",
    allowEnvar: true,
    description: "Create backup files of the changed files.",
    defaultIfMissing: false,
    order: 301
  )]
  public bool Backup { get; set; } = false;

  /// <summary>
  /// Gets or sets whether to correct line endings and also specifies what ending to use. Set to
  /// 'crlf' for Windows format (\r\n). Set to 'lf' for linux format (\n).
  /// </summary>
  [CliArgument(
    namedParameter: "eol",
    allowEnvar: true,
    description: "Specify the EOL for the files.",
    allowedValues: ["os", "crlf", "cr", "lf", "\\r\\n", "\\r", "\\n"],
    defaultIfMissing: "os",
    order: 100
  )]
  public string? EOL { get; set; } = null;

  /// <summary>
  /// Gets or sets the output encoding. If not specified, the operating-system default is used.
  /// </summary>
  [CliArgument(
    namedParameter: "encoding",
    allowEnvar: true,
    description: "Specify the file encoding. The hyphen is optional (ie: 'utf-8' or 'utf8').",
    allowedValues: ["os", "default", "ascii", "ansi", "utf32", "utf-32", "utf32bom", "utf-32-bom", "utf7", "utf-7", "utf8", "utf-8", "utf8bom", "utf-8-bom", "unicode", "utf16", "utf-16", "windows1252", "windows-1252", "win1252", "win-1252"],
    defaultIfMissing: "os",
    order: 200
  )]
  public string? FileEncoding { get; set; } = null;

  /// <summary>
  /// Gets or sets file names or patterns to exclude from processing.
  /// </summary>
  [CliArgument(
    namedParameter: "exclude",
    allowEnvar: true,
    description: "Exclude files whose name or path matches this pattern. This supersedes any file(s) found by the include file-pattern(s).",
    order: 303
  )]
  public string[] ExcludePatterns { get; set; } = [];

  /// <summary>
  /// Gets or sets the file pattern(s) to process.
  /// </summary>
  [UnhandledArguments(
    name: "file-patterns",
    description: "File patterns to process (files, directories, or wildcards).",
    required: true
  )]
  public List<string> FilePatterns { get; set; } = [];

  /// <summary>
  /// Performs the text manipulation(s) specified.
  /// </summary>
  /// <returns></returns>
  public int Run()
  {
    try {
      string NewLine;

      if (EOL?.Equals("os", StringComparison.OrdinalIgnoreCase) == true || EOL?.Equals("default", StringComparison.OrdinalIgnoreCase) == true) {
        NewLine = Environment.NewLine;
      } else if (!string.IsNullOrEmpty(EOL)) {
        NewLine = EOL.Replace("cr", "\r")
                        .Replace("lf", "\n")
                        .Replace("\\r", "\r")
                        .Replace("\\n", "\n");
        foreach (var c in NewLine) {
          if (c is '\r' or '\n') {
            continue;
          } else {
            Console.Out.WriteLine($"**** Invalid eol character found in `{EOL}`");
            return 5;
          }
        }
      } else {
        NewLine = Environment.NewLine;
      }

      // Get the encoding to write with.
      // If not specified, uses the operating system default encoding (Encoding.Default).
      var writeEncoding = EncodingHelper.ConvertEncoding(FileEncoding ?? "os");

      var recurseOption = Recurse
        ? SearchOption.AllDirectories
        : SearchOption.TopDirectoryOnly;

      var files = new List<string>();
      var message = "Working: ";
      var showProgress = !DryRun && (Verbose || ShowProgress);

      // Remove empty or whitespace-only patterns
      FilePatterns = FilePatterns.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
      var excludePatterns = ExcludePatterns
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .Select(p => p.Trim())
        .ToArray();

      // Process each file pattern and build the list of files to manipulate.
      // This should support individual files, directories, and wildcard patterns (e.g. *.txt).
      // Examples:
      // - `fixeol.exe /r *.txt` - processes all .txt files in current and sub-directories
      // - `fixeol.exe /r C:\MyFolder\*` - processes all files in C:\MyFolder and its sub-directories
      // - `fixeol.exe C:\MyFolder\file.txt` - processes only the specified file
      // - `fixeol.exe MyFolder\Subfolder` - processes all files in the specified relative directory (but not sub-directories)
      // - `fixeol.exe /r MyFolder\Subfolder\*.cs` - processes all .cs files in the specified relative directory and its sub-directories
      foreach (var pattern in FilePatterns) {
        var hasWildcard = pattern.IndexOfAny(['*', '?']) > -1;

        if (hasWildcard) {
          // Pattern contains wildcards - extract directory and search pattern
          var directory = Path.GetDirectoryName(pattern);
          var searchPattern = Path.GetFileName(pattern);

          if (string.IsNullOrEmpty(directory)) {
            // If no directory specified, use current directory
            directory = ".";
          }

          if (string.IsNullOrEmpty(searchPattern)) {
            // If no search pattern (e.g., "C:\MyFolder\"), default to "*"
            searchPattern = "*";
          }

          // Verify directory exists
          if (!Directory.Exists(directory)) {
            Console.Out.WriteLine("**** The directory was not found: {0}", Path.GetFullPath(directory));
            Console.Out.WriteLine();
            Console.Out.WriteLine("Type 'fixeol --help' for usage information.");
            return 1;
          }

          try {
            var matchedFiles = Directory.GetFiles(directory, searchPattern, recurseOption);
            if (matchedFiles.Length == 0 && Verbose && !DryRun) {
              Console.Out.WriteLine("Warning: No files matched the pattern: {0}", pattern);
            }
            files.AddRange(matchedFiles);
          } catch (Exception ex) {
            Console.Out.WriteLine("**** Error searching for files with pattern '{0}': {1}", pattern, ex.Message);
            return 1;
          }
        } else {
          // No wildcard - could be a specific file or directory
          if (File.Exists(pattern)) {
            // It's a file
            files.Add(pattern);
          } else if (Directory.Exists(pattern)) {
            // It's a directory - get all files within it
            try {
              var matchedFiles = Directory.GetFiles(pattern, "*", recurseOption);
              if (matchedFiles.Length == 0 && Verbose && !DryRun) {
                Console.Out.WriteLine("Warning: No files found in directory: {0}", pattern);
              }
              files.AddRange(matchedFiles);
            } catch (Exception ex) {
              Console.Out.WriteLine("**** Error reading directory '{0}': {1}", pattern, ex.Message);
              return 1;
            }
          } else {
            // Doesn't exist as file or directory
            Console.Out.WriteLine("**** The file or directory was not found: {0}", Path.GetFullPath(pattern));
            Console.Out.WriteLine();
            Console.Out.WriteLine("Type 'fixeol --help' for usage information.");
            return 1;
          }
        }
      }

      if (excludePatterns.Length > 0) {
        files = files
          .Where(file => !excludePatterns.Any(pattern => IsExcluded(file, pattern)))
          .Distinct(PathComparer)
          .ToList();
      } else {
        files = files
          .Distinct(PathComparer)
          .ToList();
      }

      foreach (var filename in files) {
        if (!File.Exists(filename)) {
          Console.Out.WriteLine("**** The file was not found: {0}", filename);
          Console.Out.WriteLine();
          Console.Out.WriteLine("Type 'fixeol --help' for usage information.");
          return 1;
        }

        var backupfile = filename + ".original";
        var fileInfo = new FileInfo(filename);
        var totalSize = fileInfo.Length;
        var curPos = 0.01F;

        //Console.CursorVisible = false;
        if (Verbose && !DryRun) {
          Console.WriteLine("Processing file: {0}", filename);
        }
        if (showProgress) {
          Console.Write(message);
        }

        try {
          if (!DryRun) {
            if (File.Exists(backupfile)) {
              File.SetAttributes(backupfile, FileAttributes.Normal);
              File.Delete(backupfile);
            }
            File.Copy(filename, backupfile);
          }
        } catch (Exception ex) {
          Console.WriteLine();
          Console.WriteLine("**** ERROR backing up file: " + filename + " \n" + ex.Message);
          return 100;
        } finally {
          //Console.CursorVisible = true;
        }

        try {
          // Detect the source file encoding for reading
          // WE ALWAYS detect the source file encoding, even if the user specified an encoding to write with.
          var sourceFile = DryRun ? filename : backupfile;
          var readEncoding = Encoding.Default;
          using (var detectStream = File.OpenRead(sourceFile)) {
            readEncoding = detectStream.DetectEncoding(Encoding.Default)!;
          }

          using var r = new StreamReader(sourceFile, readEncoding);
          var nextOutput = DateTime.Now.AddMilliseconds(200);

          using Stream outStream = DryRun
            ? new ComparingStream(File.OpenRead(filename))
            : File.Create(filename);
          using var writer = new StreamWriter(outStream, writeEncoding);

          while (!r.EndOfStream) {
            var line = r.ReadLine();

            writer.Write(line + NewLine);

            if (showProgress && DateTime.Now > nextOutput) { // || r.EndOfStream
              TrySetCursorLeft(message.Length);
              curPos += line.Length + NewLine.Length;
              Console.Write("{0:0.00}%   ", Math.Max(0, Math.Min(100, (curPos * 100F) / totalSize)));
              nextOutput = DateTime.Now.AddMilliseconds(5);
            }
          }

          if (DryRun) {
            // Include buffered bytes and the encoding preamble in the comparison.
            writer.Flush();
            if (((ComparingStream)outStream).HasChanges) {
              Console.WriteLine(filename);
            }
            continue;
          }
        } catch (Exception ex) {
          Console.WriteLine();
          Console.WriteLine($"**** ERROR {(DryRun ? "checking" : "writing to")} file: {filename} \n{ex.Message}");
          return 101;
        } finally {
          //Console.CursorVisible = true;
        }

        try {
          if (Backup) {
            // TODO: old(er) backup files should not be deleted..
            if (File.Exists(filename + ".bak")) {
              File.SetAttributes(filename + ".bak", FileAttributes.Normal);
              File.Delete(filename + ".bak");
            }
            File.Move(backupfile, filename + ".bak");
          } else {
            File.SetAttributes(backupfile, FileAttributes.Normal);
            File.Delete(backupfile);
          }
        } catch (Exception ex) {
          Console.WriteLine();
          Console.WriteLine("**** ERROR processing file: " + filename + " \n" + ex.Message);
          return 102;
        } finally {
          //Console.CursorVisible = true;
        }

        if (showProgress) {
          TrySetCursorLeft(message.Length);
          Console.WriteLine("100.00%  ");
        }
      }

      if (Verbose && !DryRun) {
        Console.Out.WriteLine();
      }
    } finally {
      if (Pause) {
        Console.Write("Press any key to exit: ");
        Console.ReadKey(true);
        Console.WriteLine();
      }
    }

    return 0;
  }

  private static void TrySetCursorLeft( int left )
  {
    try {
      if (!Console.IsOutputRedirected) {
        Console.CursorLeft = left;
      }
    } catch (IOException) {
      // Non-interactive hosts can report an invalid console handle.
    } catch (InvalidOperationException) {
      // Output redirection can change while the process is running.
    }
  }

  private static bool IsExcluded( string file, string pattern )
  {
    if (string.IsNullOrWhiteSpace(pattern)) {
      return false;
    }

    var fullPath = Path.GetFullPath(file);
    var normalizedPattern = NormalizePath(pattern.Trim());
    var fileName = Path.GetFileName(file);
    var relativePath = NormalizePath(Path.GetRelativePath(Environment.CurrentDirectory, fullPath));
    var normalizedFullPath = NormalizePath(fullPath);

    return MatchesPattern(fileName, normalizedPattern)
        || MatchesPattern(relativePath, normalizedPattern)
        || MatchesPattern(normalizedFullPath, normalizedPattern);
  }

  private static bool MatchesPattern( string value, string pattern )
  {
    if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(pattern)) {
      return false;
    }

    var normalizedValue = NormalizePath(value);
    if (ContainsWildcard(pattern)) {
      var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
      var regexOptions = RegexOptions.CultureInvariant;
      if (OperatingSystem.IsWindows()) {
        regexOptions |= RegexOptions.IgnoreCase;
      }

      return Regex.IsMatch(normalizedValue, regexPattern, regexOptions);
    }

    return normalizedValue.Equals(pattern, PathComparison)
        || normalizedValue.EndsWith("/" + pattern, PathComparison);
  }

  private static bool ContainsWildcard( string pattern )
  {
    return pattern.IndexOfAny(['*', '?']) > -1;
  }

  private static string NormalizePath( string value )
  {
    var normalized = value.Replace('\\', '/');
    while (normalized.StartsWith("./", StringComparison.Ordinal)) {
      normalized = normalized[2..];
    }

    return normalized;
  }

  private static StringComparison PathComparison
    => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

  private static StringComparer PathComparer
    => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;


}
