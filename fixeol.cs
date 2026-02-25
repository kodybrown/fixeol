namespace Bricksoft.fixeol;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bricksoft.PowerCode;

public class FixEol : ConsoleApp
{
  /// <summary>
  /// Main entry point.
  /// </summary>
  /// <param name="arguments"></param>
  /// <returns></returns>
  public static int Main( string[] arguments )
  {
    var app = new FixEol(arguments);
    return app.Run();
  }

  [NamedParameters(["pause"], allowEnvar: true)]
  public override bool OptPause { get; protected set; } = false;

  [NamedParameters(["verbose", "e"], allowEnvar: true)]
  public override bool OptVerbose { get; protected set; } = false;

  /// <summary>
  /// Shows additional details during processing, such as the file being processed and progress
  /// percentage.
  /// </summary>
  [NamedParameters(["progress"], allowEnvar: true)]
  public bool OptShowProgress { get; protected set; } = false;

  /// <summary>
  /// Gets or sets whether to scan sub-directories.
  /// </summary>
  [NamedParameters(["recursive", "recurse", "r", "s", "subdir", "subdirs"], allowEnvar: true)]
  public bool OptRecurse { get; protected set; } = false;

  /// <summary>
  /// Gets or sets whether to backup the (changed) files.
  /// </summary>
  [NamedParameters(["backup", "b"], allowEnvar: true)]
  public bool OptBackup { get; protected set; } = false;

  /// <summary>
  /// Gets or sets whether to correct line endings and also specifies what ending to use. Set to
  /// 'crlf' for Windows format (\r\n). Set to 'lf' for linux format (\n).
  /// </summary>
  [NamedParameters(["eol"], allowEnvar: true)]
  public string? OptEOL { get; protected set; } = null;

  /// <summary>
  /// Gets or sets the encoding. If not specified, the source file encoding is used.
  /// </summary>
  [NamedParameters(["encoding", "enc"], allowEnvar: true)]
  public string? OptEncoding { get; protected set; } = null;

  /// <summary>
  /// Gets or sets the file pattern(s) to process.
  /// </summary>
  [EverythingElse]
  public List<string> FilePatterns { get; protected set; } = [];

  /// <summary>
  /// Show the app's environment variables.
  /// </summary>
  [NamedParameters("envars")]
  public bool OptShowEnvVars { get; protected set; } = false;

  /// <summary>
  /// Creates an instance of the class.
  /// </summary>
  /// <param name="arguments"></param>
  public FixEol( string[] arguments )
    : base(arguments, nameof(FixEol))
  {
    FilePatterns = [];
    //AppEnvarPrefix = nameof(FixEol).ToLower() + "_";
    AppEnvarPrefix = nameof(FixEol).ToLower() + "_";
    AppDescription = "Fixes the eol for the specified file(s).";
    AppCopyright = "Copyright (C) 2003-2026 Kody Brown.";
  }

  /// <summary>
  /// Performs the text manipulation(s) specified.
  /// </summary>
  /// <returns></returns>
  public int Run()
  {
    // Parse command-line arguments (and envars).
    var (shouldExit, exitCode) = ParseCommandLineArguments(allowEnvarValues: true);
    if (shouldExit) {
      return exitCode;
    }

    if (OptHelp) {
      ShowUsage(true);
      PauseFlagHandler();
      return 0;
    }

    if (OptShowEnvVars) {
      ShowCurrentEnvars();
      PauseFlagHandler();
      return 0;
    }

    var defaultEol = Path.DirectorySeparatorChar == '\\' ? "crlf" : "lf";

    string NewLine;

    if (!string.IsNullOrEmpty(OptEOL)) {
      NewLine = OptEOL.Replace("cr", "\r")
                      .Replace("lf", "\n")
                      .Replace("\\r", "\r")
                      .Replace("\\n", "\n");
      foreach (var c in NewLine) {
        if (c is '\r' or '\n') {
          continue;
        } else {
          Console.Out.WriteLine($"**** Invalid eol character found in `{OptEOL}`");
          return 5;
        }
      }
    } else {
      NewLine = Environment.NewLine;
    }

    var recurseOption = OptRecurse
      ? SearchOption.AllDirectories
      : SearchOption.TopDirectoryOnly;

    var files = new List<string>();
    var message = "Working: ";

    // Parse file patterns from the FilePatterns property
    if (FilePatterns == null || FilePatterns.Count == 0) {
      Console.Out.WriteLine("**** Missing command-line argument: {0}", "file");
      ShowUsage(false);
      PauseFlagHandler();
      return 1;
    }

    // Remove empty or whitespace-only patterns
    FilePatterns = FilePatterns.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

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
          ShowUsage(false);
          PauseFlagHandler();
          return 1;
        }

        try {
          var matchedFiles = Directory.GetFiles(directory, searchPattern, recurseOption);
          if (matchedFiles.Length == 0 && OptVerbose) {
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
            if (matchedFiles.Length == 0 && OptVerbose) {
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
          ShowUsage(false);
          PauseFlagHandler();
          return 1;
        }
      }
    }

    foreach (var filename in files) {
      if (!File.Exists(filename)) {
        Console.Out.WriteLine("**** The file was not found: {0}", filename);
        ShowUsage(false);
        PauseFlagHandler();
        return 1;
      }

      var backupfile = filename + ".original";
      var fileInfo = new FileInfo(filename);
      var totalSize = fileInfo.Length;
      var curPos = 0.01F;

      //Console.CursorVisible = false;
      if (OptVerbose) {
        Console.WriteLine("Processing file: {0}", filename);
        Console.Write(message);
      }

      try {
        if (File.Exists(backupfile)) {
          File.SetAttributes(backupfile, FileAttributes.Normal);
          File.Delete(backupfile);
        }
        File.Copy(filename, backupfile);
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
        var readEncoding = Encoding.Default;
        using (var detectStream = File.OpenRead(backupfile)) {
          readEncoding = detectStream.DetectEncoding();
        }

        // Get the encoding to write with.
        // If not specified, uses the operating system default encoding (Encoding.Default).
        var writeEncoding = GetEncoding(OptEncoding);

        using var r = new StreamReader(backupfile, readEncoding);
        var nextOutput = DateTime.Now.AddMilliseconds(200);

        using var outStream = File.Create(filename);
        using var writer = new StreamWriter(outStream, writeEncoding);

        while (!r.EndOfStream) {
          var line = r.ReadLine();

          writer.Write(line + NewLine);

          if (OptVerbose && DateTime.Now > nextOutput) { // || r.EndOfStream
            Console.CursorLeft = message.Length;
            curPos += line.Length + NewLine.Length;
            Console.Write("{0:0.00}%   ", Math.Max(0, Math.Min(100, (curPos * 100F) / totalSize)));
            nextOutput = DateTime.Now.AddMilliseconds(5);
          }
        }

        //writer.Flush();
        //writer.Close();
      } catch (Exception ex) {
        Console.WriteLine();
        Console.WriteLine("**** ERROR writing to file: " + filename + " \n" + ex.Message);
        return 101;
      } finally {
        //Console.CursorVisible = true;
      }

      try {
        if (OptBackup) {
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

      if (OptVerbose) {
        Console.CursorLeft = message.Length;
        Console.WriteLine("100.00%  ");
      }
    }

    if (OptVerbose) {
      Console.Out.WriteLine();
    }

    PauseFlagHandler();

    return 0;
  }

  private static Encoding GetEncoding( string? encoding )
  {
    switch (encoding?.Replace("-", "") ?? string.Empty) {
      case "ascii":
        return Encoding.ASCII;
      case "ansi":
        // Always Windows-1252
        return Encoding.GetEncoding(1252);
      case "utf32":
        // no BOM
        return new UTF32Encoding(false, false);
      case "utf32bom":
        // with BOM
        return new UTF32Encoding(false, true);
      case "utf7":
        // UTF-7 is obsolete but may be explicitly requested by users
#pragma warning disable SYSLIB0001
        return Encoding.UTF7;
#pragma warning restore SYSLIB0001
      case "utf8":
        // UTF8 without BOM
        return new UTF8Encoding(false);
      case "utf8bom":
        // UTF8 with BOM
        return new UTF8Encoding(true);
      case "unicode":
        return Encoding.Unicode;
      case "windows1252":
      case "win1252":
        // Western European
        return Encoding.GetEncoding(1252);
      case "default":
      default:
        Console.WriteLine($"Invalid or unknown encoding specified '{encoding}'. Using platform default.");
        // Platform-specific (Windows: code page, Linux/macOS: UTF-8)
        return Encoding.Default;
    }
  }

  private void ShowUsage( bool showDetails = true )
  {
    ShowHeader();
    Console.Out.WriteLine();

    if (!showDetails) {
      Console.Out.WriteLine($"type '{AppName}.exe /?' for help");
      return;
    }

    Console.Out.WriteLine(@$"Usage: 

> {AppName} [options] ""filepattern"" [""filepattern""] [...] 

  filepattern        The file(s) (or pattern) to manipulate.
                     The filepattern is required.

Options: 

  -eol [crlf|cr|lf]  Override the default eol settings of the operating system.
                     supports `crlf` (Windows format), `lf` (Linux format), and `cr` (old Mac format).
                     For example, use `-eol lf` to convert line endings to Linux format.
                     Omit to use the default for the operating system.

  -encoding [enc]    Override the default encoding settings of source file.
                     Supports `ascii`, `ansi` (Windows-1252), `default` (platform-specific),
                     `utf8`, `utf8bom`, `utf7`, `utf32`, `utf32bom`, `unicode`, and `windows-1252`.

  -b -backup         Backup (default:false)
  -r -recursive      Apply the file pattern(s) to the current
                     and all sub-directories (default:false)
  -p -pause          Pause when finished (default:false)
  -e -verbose        Output additional details (default:false)
  -progress          Output progress (default:false)

  -v                 Show version
  -version           Show full version details

  -envars            Displays the current environment variables
                     then exits. All other options are ignored.

  *use ! to set any option to opposite value. overrides environment variables.
  for example use /!e to not use verbose (useful to override envars).

Environment Variables:

    {AppEnvarPrefix}file=filename           sets -file ""filename""
    {AppEnvarPrefix}verbose=true|false      sets /v or /!v
    {AppEnvarPrefix}pause=true|false        sets /pause or /!pause

    *command-line arguments override environment variables
");
    ShowCurrentEnvars(false);
  }
}
