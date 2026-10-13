using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using PowerCode;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace fixeol.tests;

public class CliTests
{
  [Fact]
  public void HelpReturnsUsageWhenOutputIsCaptured()
  {
    var result = RunCli("--help");

    Assert.True(result.ExitCode == 0, result.Output);
    Assert.Contains("USAGE:", result.Output);
    Assert.Contains("-exclude [string[]]", result.Output);
    Assert.Contains("(repeatable)", result.Output);
    Assert.Contains("-dry-run", result.Output);
    Assert.Contains("Missing paths and unmatched patterns are skipped", Regex.Replace(result.Output, @"\s+", " "));
  }

  [Theory]
  [InlineData("alpha\r\nbeta\n", "utf8", "utf8", "lf", true)]
  [InlineData("alpha\nbeta\n", "utf8", "utf8", "lf", false)]
  [InlineData("alpha\r\nbeta\r\n", "utf8", "utf8", "crlf", false)]
  [InlineData("alpha\rbeta\r", "utf8", "utf8", "cr", false)]
  [InlineData("alpha\nbeta", "utf8", "utf8", "lf", true)]
  [InlineData("alpha\n\n", "utf8", "utf8", "lf", false)]
  [InlineData("alpha\n", "utf8", "utf8bom", "lf", true)]
  [InlineData("alpha\n", "utf8bom", "utf8", "lf", true)]
  [InlineData("alpha\n", "utf8bom", "utf8bom", "lf", false)]
  [InlineData("caf\u00e9\n", "utf16", "utf8", "lf", true)]
  [InlineData("caf\u00e9\n", "utf16", "utf16", "lf", false)]
  [InlineData("caf\u00e9\n", "utf8", "ascii", "lf", true)]
  [InlineData("", "utf8", "utf8", "lf", false)]
  [InlineData("", "utf8", "utf8bom", "lf", true)]
  [InlineData("", "utf8bom", "utf8", "lf", true)]
  [InlineData("", "utf8bom", "utf8bom", "lf", false)]
  public void DryRunMatchesActualConversionWithoutWriting(
    string content, string sourceEncoding, string targetEncoding, string eol, bool shouldChange )
  {
    var tempDirectory = Directory.CreateTempSubdirectory("fixeol-tests-");

    try {
      var filePath = Path.Combine(tempDirectory.FullName, "sample.txt");
      File.WriteAllText(filePath, content, EncodingHelper.ConvertEncoding(sourceEncoding));
      var originalBytes = File.ReadAllBytes(filePath);
      File.SetLastWriteTimeUtc(filePath, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
      var originalWriteTime = File.GetLastWriteTimeUtc(filePath);

      var preview = RunCli("--dry-run", "--encoding", targetEncoding, "--eol", eol, filePath);

      Assert.True(preview.ExitCode == 0, preview.Output);
      Assert.Equal(shouldChange ? filePath + Environment.NewLine : "", preview.Output);
      Assert.Equal(originalBytes, File.ReadAllBytes(filePath));
      Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(filePath));
      Assert.Equal(new[] { filePath }, Directory.GetFiles(tempDirectory.FullName));

      var actual = RunCli("--encoding", targetEncoding, "--eol", eol, filePath);

      Assert.True(actual.ExitCode == 0, actual.Output);
      Assert.Equal(shouldChange, !originalBytes.SequenceEqual(File.ReadAllBytes(filePath)));
    } finally {
      tempDirectory.Delete(true);
    }
  }

  [Fact]
  public void DryRunHonorsPatternsAndPreservesExistingBackups()
  {
    var tempDirectory = Directory.CreateTempSubdirectory("fixeol-tests-");

    try {
      var nestedDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.FullName, "nested"));
      var changedPath = Path.Combine(nestedDirectory.FullName, "changed.txt");
      var unchangedPath = Path.Combine(tempDirectory.FullName, "unchanged.txt");
      var excludedPath = Path.Combine(nestedDirectory.FullName, "excluded.txt");
      File.WriteAllText(changedPath, "change\r\n");
      File.WriteAllText(unchangedPath, "keep\n");
      File.WriteAllText(excludedPath, "exclude\r\n");
      File.WriteAllText(changedPath + ".original", "existing original");
      File.WriteAllText(changedPath + ".bak", "existing backup");
      var originals = Directory.GetFiles(tempDirectory.FullName, "*", SearchOption.AllDirectories)
        .ToDictionary(path => path, path => (Bytes: File.ReadAllBytes(path), WriteTime: File.GetLastWriteTimeUtc(path)));

      var result = RunCli(
        "--dry-run", "--backup", "--verbose", "--progress", "--recursive",
        "--encoding", "utf8", "--eol", "lf", "--exclude", "excluded.txt",
        Path.Combine(tempDirectory.FullName, "*.txt"), changedPath
      );

      Assert.True(result.ExitCode == 0, result.Output);
      Assert.Equal(changedPath + Environment.NewLine, result.Output);
      Assert.Equal(originals.Keys.Order(), Directory.GetFiles(tempDirectory.FullName, "*", SearchOption.AllDirectories).Order());
      foreach (var (path, original) in originals) {
        Assert.Equal(original.Bytes, File.ReadAllBytes(path));
        Assert.Equal(original.WriteTime, File.GetLastWriteTimeUtc(path));
      }
    } finally {
      tempDirectory.Delete(true);
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void MissingIncludesDoNotPreventProcessingExistingMatches( bool dryRun )
  {
    var tempDirectory = Directory.CreateTempSubdirectory("fixeol-tests-");

    try {
      var nestedDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.FullName, "src", "nested"));
      var literalPath = Path.Combine(tempDirectory.FullName, "README.md");
      var wildcardPath = Path.Combine(tempDirectory.FullName, "build.csproj");
      var nestedPath = Path.Combine(nestedDirectory.FullName, "sample.cs");
      var paths = new[] { literalPath, wildcardPath, nestedPath };
      foreach (var path in paths) {
        File.WriteAllText(path, "alpha\r\nbeta", new UTF8Encoding(false));
      }

      var arguments = new List<string> { "--recursive", "--encoding", "utf8", "--eol", "lf" };
      if (dryRun) {
        arguments.AddRange(["--dry-run", "--verbose", "--progress"]);
      }
      arguments.AddRange([
        Path.Combine(tempDirectory.FullName, "Makefile"), literalPath,
        Path.Combine(tempDirectory.FullName, "missing-directory") + Path.DirectorySeparatorChar,
        Path.Combine(tempDirectory.FullName, "*.csproj"),
        Path.Combine(tempDirectory.FullName, "absent", "*.txt"),
        Path.Combine(tempDirectory.FullName, "src"),
        Path.Combine(tempDirectory.FullName, "*.nomatch")
      ]);

      var result = RunCli(arguments.ToArray());

      Assert.True(result.ExitCode == 0, result.Output);
      Assert.Equal(dryRun ? string.Join(Environment.NewLine, paths) + Environment.NewLine : "", result.Output);
      foreach (var path in paths) {
        Assert.Equal(dryRun ? "alpha\r\nbeta" : "alpha\nbeta\n", File.ReadAllText(path));
      }
      Assert.Equal(paths.Order(), Directory.GetFiles(tempDirectory.FullName, "*", SearchOption.AllDirectories).Order());
    } finally {
      tempDirectory.Delete(true);
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void NoMatchingIncludesSucceedsWithoutWriting( bool dryRun )
  {
    var tempDirectory = Directory.CreateTempSubdirectory("fixeol-tests-");

    try {
      var emptyDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.FullName, "empty"));
      var arguments = new List<string> { "--recursive", "--backup", "--encoding", "utf8", "--eol", "lf" };
      if (dryRun) {
        arguments.AddRange(["--dry-run", "--verbose", "--progress"]);
      }
      arguments.AddRange([
        Path.Combine(tempDirectory.FullName, "Makefile"),
        Path.Combine(tempDirectory.FullName, "missing-directory") + Path.DirectorySeparatorChar,
        Path.Combine(tempDirectory.FullName, "absent", "*.txt"),
        Path.Combine(tempDirectory.FullName, "*.nomatch"), emptyDirectory.FullName
      ]);

      var result = RunCli(arguments.ToArray());

      Assert.True(result.ExitCode == 0, result.Output);
      Assert.Equal("", result.Output);
      Assert.Empty(Directory.GetFiles(tempDirectory.FullName, "*", SearchOption.AllDirectories));
      Assert.Equal(new[] { emptyDirectory.FullName }, Directory.GetDirectories(tempDirectory.FullName));
    } finally {
      tempDirectory.Delete(true);
    }
  }

  [Theory]
  [InlineData("--eol", "invalid", "Makefile")]
  [InlineData("--encoding", "invalid", "Makefile")]
  [InlineData("--unknown-option", "Makefile")]
  [InlineData("-unknown-option", "Makefile")]
  [InlineData("--eol")]
  [InlineData("--encoding", "utf8")]
  public void InvalidArgumentsStillFail( params string[] arguments )
  {
    var result = RunCli(arguments);

    Assert.NotEqual(0, result.ExitCode);
    Assert.NotEmpty(result.Output);
  }

  [Theory]
  [InlineData("invalid\0path")]
  [InlineData("invalid\0path/*.txt")]
  public void InvalidIncludePathsStillFail( string pattern )
  {
    // NUL cannot be passed to a child process, so exercise the entry point directly.
    Assert.Equal(1, Program.Main([pattern]));
  }

  [Fact]
  public void ExistingFileUsedAsWildcardDirectoryStillFails()
  {
    var tempDirectory = Directory.CreateTempSubdirectory("fixeol-tests-");

    try {
      var filePath = Path.Combine(tempDirectory.FullName, "file.txt");
      File.WriteAllText(filePath, "alpha\r\n");

      var result = RunCli(Path.Combine(tempDirectory.FullName, "Makefile"), Path.Combine(filePath, "*.txt"));

      Assert.Equal(1, result.ExitCode);
      Assert.Contains("Error searching for files", result.Output);
      Assert.Equal("alpha\r\n", File.ReadAllText(filePath));
    } finally {
      tempDirectory.Delete(true);
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void ReadFailuresStillFailWithMissingIncludes( bool dryRun )
  {
    var tempDirectory = Directory.CreateTempSubdirectory("fixeol-tests-");

    try {
      var filePath = Path.Combine(tempDirectory.FullName, "locked.txt");
      File.WriteAllText(filePath, "alpha\r\n");
      using var locked = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
      var arguments = new List<string> { "--encoding", "utf8", "--eol", "lf" };
      if (dryRun) {
        arguments.Add("--dry-run");
      }
      arguments.AddRange([Path.Combine(tempDirectory.FullName, "Makefile"), filePath]);

      var result = RunCli(arguments.ToArray());

      Assert.NotEqual(0, result.ExitCode);
      Assert.Contains("ERROR", result.Output);
      Assert.Contains(filePath, result.Output);
    } finally {
      tempDirectory.Delete(true);
    }
  }

  [Fact]
  public void WriteFailuresStillFailWithMissingIncludes()
  {
    var tempDirectory = Directory.CreateTempSubdirectory("fixeol-tests-");

    try {
      var filePath = Path.Combine(tempDirectory.FullName, "sample.txt");
      File.WriteAllText(filePath, "alpha\r\n");
      // A directory at the backup destination makes the required write fail on every platform.
      Directory.CreateDirectory(filePath + ".original");

      var result = RunCli("--eol", "lf", Path.Combine(tempDirectory.FullName, "Makefile"), filePath);

      Assert.NotEqual(0, result.ExitCode);
      Assert.Contains("ERROR backing up file", result.Output);
      Assert.Equal("alpha\r\n", File.ReadAllText(filePath));
    } finally {
      tempDirectory.Delete(true);
    }
  }

  [Fact]
  public void DryRunDetectsChangesBeyondTheFirstBuffer()
  {
    var tempDirectory = Directory.CreateTempSubdirectory("fixeol-tests-");

    try {
      var filePath = Path.Combine(tempDirectory.FullName, "large.txt");
      var content = new string('x', 20000) + "\n";
      File.WriteAllText(filePath, content);

      var unchanged = RunCli("--dry-run", "--encoding", "utf8", "--eol", "lf", filePath);
      Assert.True(unchanged.ExitCode == 0, unchanged.Output);
      Assert.Equal("", unchanged.Output);

      File.AppendAllText(filePath, "last line");
      var changed = RunCli("--dry-run", "--encoding", "utf8", "--eol", "lf", filePath);
      Assert.True(changed.ExitCode == 0, changed.Output);
      Assert.Equal(filePath + Environment.NewLine, changed.Output);
      Assert.Equal(content + "last line", File.ReadAllText(filePath));
    } finally {
      tempDirectory.Delete(true);
    }
  }

  [Fact]
  public void AnsiEncodingOptionProcessesFile()
  {
    var tempDirectory = Directory.CreateTempSubdirectory("fixeol-tests-");

    try {
      var filePath = Path.Combine(tempDirectory.FullName, "sample.txt");
      File.WriteAllText(filePath, "alpha\r\nbeta", new UTF8Encoding(false));

      var result = RunCli("--encoding", "ansi", "--eol", "lf", filePath);

      Assert.True(result.ExitCode == 0, result.Output);
      Assert.Equal("alpha\nbeta\n", Encoding.ASCII.GetString(File.ReadAllBytes(filePath)));
    } finally {
      tempDirectory.Delete(true);
    }
  }

  [Fact]
  public void HyphenatedUtf8EncodingOptionProcessesFile()
  {
    var tempDirectory = Directory.CreateTempSubdirectory("fixeol-tests-");

    try {
      var filePath = Path.Combine(tempDirectory.FullName, "sample.txt");
      File.WriteAllText(filePath, "alpha\r\nbeta", new UTF8Encoding(false));

      var result = RunCli("--encoding", "utf-8", "--eol", "lf", filePath);

      Assert.True(result.ExitCode == 0, result.Output);
      Assert.Equal("alpha\nbeta\n", Encoding.UTF8.GetString(File.ReadAllBytes(filePath)));
    } finally {
      tempDirectory.Delete(true);
    }
  }

  [Fact]
  public void ExcludePatternSkipsMatchingFileName()
  {
    var tempDirectory = Directory.CreateTempSubdirectory("fixeol-tests-");

    try {
      var includedPath = Path.Combine(tempDirectory.FullName, "keep.cs");
      var excludedPath = Path.Combine(tempDirectory.FullName, "specific-file.bat");
      File.WriteAllText(includedPath, "include\n", new UTF8Encoding(false));
      File.WriteAllText(excludedPath, "exclude\n", new UTF8Encoding(false));

      var result = RunCli(
        "--encoding", "utf8",
        "--eol", "crlf",
        "-r",
        Path.Combine(tempDirectory.FullName, "*.cs"),
        Path.Combine(tempDirectory.FullName, "*.bat"),
        "-exclude", "specific-file.bat"
      );

      Assert.True(result.ExitCode == 0, result.Output);
      Assert.Equal("include\r\n", File.ReadAllText(includedPath, Encoding.UTF8));
      Assert.Equal("exclude\n", File.ReadAllText(excludedPath, Encoding.UTF8));
    } finally {
      tempDirectory.Delete(true);
    }
  }

  [Fact]
  public void BinderParsesFriendlyEnumNames()
  {
    var options = new EnumOptions();
    var binder = new CliArgumentBinder(["--mode", "always"], "test");

    var result = binder.ParseAndBind(options);

    Assert.False(result.ShouldExit);
    Assert.Equal(TestMode.TestModeAlways, options.Mode);
  }

  private enum TestMode
  {
    TestModeNever,
    TestModeAlways,
  }

  private sealed class EnumOptions
  {
    [CliArgument(namedParameter: "mode")]
    public TestMode Mode { get; set; }
  }

  private static CommandResult RunCli( params string[] arguments )
  {
    var projectPath = FindProjectPath();
    var startInfo = new ProcessStartInfo("dotnet") {
      RedirectStandardError = true,
      RedirectStandardOutput = true,
      UseShellExecute = false,
      WorkingDirectory = Path.GetDirectoryName(projectPath)!
    };

    startInfo.ArgumentList.Add("run");
    startInfo.ArgumentList.Add("--no-restore");
    startInfo.ArgumentList.Add("--no-launch-profile");
    startInfo.ArgumentList.Add("--project");
    startInfo.ArgumentList.Add(projectPath);
    startInfo.ArgumentList.Add("--");

    foreach (var argument in arguments) {
      startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start dotnet.");
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();

    if (!process.WaitForExit(TimeSpan.FromSeconds(30))) {
      process.Kill(entireProcessTree: true);
      throw new TimeoutException("fixeol did not exit within 30 seconds.");
    }

    return new CommandResult(process.ExitCode, output + error);
  }

  private static string FindProjectPath( [CallerFilePath] string sourceFilePath = "" )
  {
    var startDirectory = Path.GetDirectoryName(sourceFilePath)
      ?? throw new FileNotFoundException("Could not locate the test source directory.");

    var directory = new DirectoryInfo(startDirectory);
    while (directory != null) {
      var candidate = Path.Combine(directory.FullName, "fixeol.csproj");
      if (File.Exists(candidate)) {
        return candidate;
      }

      directory = directory.Parent;
    }

    throw new FileNotFoundException("Could not locate fixeol.csproj.");
  }

  private sealed record CommandResult( int ExitCode, string Output );
}
