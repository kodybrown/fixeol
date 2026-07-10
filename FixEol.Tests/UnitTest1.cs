using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Bricksoft.fixeol.Tests;

public class FixEolCliTests
{
  [Fact]
  public void HelpReturnsUsageWhenOutputIsCaptured()
  {
    var result = RunFixEol("--help");

    Assert.True(result.ExitCode == 0, result.Output);
    Assert.Contains("Usage:", result.Output);
  }

  [Fact]
  public void AnsiEncodingOptionProcessesFile()
  {
    var tempDirectory = Directory.CreateTempSubdirectory("fixeol-tests-");

    try {
      var filePath = Path.Combine(tempDirectory.FullName, "sample.txt");
      File.WriteAllText(filePath, "alpha\r\nbeta", new UTF8Encoding(false));

      var result = RunFixEol("--encoding", "ansi", "--eol", "lf", filePath);

      Assert.True(result.ExitCode == 0, result.Output);
      Assert.Equal("alpha\nbeta\n", Encoding.ASCII.GetString(File.ReadAllBytes(filePath)));
    } finally {
      tempDirectory.Delete(true);
    }
  }

  private static CommandResult RunFixEol( params string[] arguments )
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
      throw new TimeoutException("FixEol did not exit within 30 seconds.");
    }

    return new CommandResult(process.ExitCode, output + error);
  }

  private static string FindProjectPath( [CallerFilePath] string sourceFilePath = "" )
  {
    var startDirectory = Path.GetDirectoryName(sourceFilePath)
      ?? throw new FileNotFoundException("Could not locate the test source directory.");

    var directory = new DirectoryInfo(startDirectory);
    while (directory != null) {
      var candidate = Path.Combine(directory.FullName, "FixEol.csproj");
      if (File.Exists(candidate)) {
        return candidate;
      }

      directory = directory.Parent;
    }

    throw new FileNotFoundException("Could not locate FixEol.csproj.");
  }

  private sealed record CommandResult( int ExitCode, string Output );
}
