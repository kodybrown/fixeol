# fixeol

![dotnet-core-build](https://github.com/kodybrown/fixeol/workflows/dotnet-core-build/badge.svg)
![dotnet-core-release](https://github.com/kodybrown/fixeol/workflows/dotnet-core-release/badge.svg)

fixeol normalizes line endings for one or more files, directories, or wildcard patterns.

Click here for the [latest release](https://github.com/kodybrown/fixeol/releases/latest/).

## Examples

```console
fixeol --recursive --eol crlf *.css *.js
fixeol --eol lf --encoding utf8 README.md
fixeol --dry-run --recursive --eol lf --encoding utf8 "*.cs"
fixeol /r /backup src\*.cs
fixeol --encoding utf8 --eol crlf -r *.code-workspace *.cs *.csproj *.bat -exclude specific-file.bat
```

## Usage

```console
fixeol [options] "file-patterns" ["file-patterns"] [...]
```

File patterns can be specific files, directories, or wildcards. Use `--recursive`, `-r`, or `-s` to apply patterns to subdirectories.

## Options

- `--eol [os|crlf|cr|lf|\r\n|\r|\n]` chooses the output line ending. The default is `os`.
- `--encoding` chooses the output encoding. Accepted values are `os`, `default`,
  `ascii`, `ansi`, `utf32`, `utf-32`, `utf32bom`, `utf-32-bom`, `utf7`,
  `utf-7`, `utf8`, `utf-8`, `utf8bom`, `utf-8-bom`, `unicode`, `utf16`,
  `utf-16`, `windows1252`, `windows-1252`, `win1252`, and `win-1252`. The
  default is `os`; hyphens are optional.
- `--recursive`, `-r`, `-s` scans subdirectories.
- `--exclude [pattern]` skips matching file names or paths. Repeat it for multiple excludes; excludes win over include patterns.
- `--backup` keeps the original file as `.bak`.
- `--dry-run` prints only the paths of files whose contents would change, one per line,
  without writing files or creating, replacing, or deleting backups (even with `--backup`).
  It checks line endings, the final newline, encoding, and BOM changes using the same
  conversion as a normal run. It honors recursion and exclusions, suppresses progress
  and verbose output, and returns exit code 0 on success whether or not changes are found.
- `--progress` prints per-file progress.
- `--verbose` prints additional processing details.
- `--pause` waits for a key before exiting.
- `--help`, `-?`, `-h`, or `help` prints usage.
- `-v` prints the version; `--version` prints full app metadata.

On Windows, options can use `-`, `--`, or `/` prefixes. Use `!` to turn off a boolean option from the command line, for example `--!verbose`.

## Environment Variables

Command-line arguments override environment variables. Use semicolons to separate multiple `fixeol_exclude` patterns.

- `fixeol_eol`
- `fixeol_encoding`
- `fixeol_recursive`
- `fixeol_exclude`
- `fixeol_backup`
- `fixeol_progress`
- `fixeol_verbose`
- `fixeol_pause`
