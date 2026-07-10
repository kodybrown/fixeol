# FixEol

![dotnet-core-build](https://github.com/kodybrown/fixeol/workflows/dotnet-core-build/badge.svg)
![dotnet-core-release](https://github.com/kodybrown/fixeol/workflows/dotnet-core-release/badge.svg)

FixEol normalizes line endings for one or more files, directories, or wildcard patterns.

Click here for the [latest release](https://github.com/kodybrown/fixeol/releases/latest/).

## Examples

```console
FixEol --recursive --eol crlf *.css *.js
FixEol --eol lf --encoding utf8 README.md
FixEol /r /backup src\*.cs
```

## Usage

```console
FixEol [options] "file-patterns" ["file-patterns"] [...]
```

File patterns can be specific files, directories, or wildcards. Use `--recursive`, `-r`, or `-s` to apply patterns to subdirectories.

## Options

- `--eol [os|crlf|cr|lf|\r\n|\r|\n]` chooses the output line ending. The default is `os`.
- `--encoding [os|ascii|ansi|utf32|utf32bom|utf7|utf8|utf8bom|unicode|windows1252|win1252]` chooses the output encoding. The default is `os`.
- `--recursive`, `-r`, `-s` scans subdirectories.
- `--backup` keeps the original file as `.bak`.
- `--progress` prints per-file progress.
- `--verbose` prints additional processing details.
- `--pause` waits for a key before exiting.
- `--help`, `-?`, `-h`, or `help` prints usage.
- `-v` prints the version; `--version` prints full app metadata.

On Windows, options can use `-`, `--`, or `/` prefixes. Use `!` to turn off a boolean option from the command line, for example `--!verbose`.

## Environment Variables

Command-line arguments override environment variables.

- `fixeol_eol`
- `fixeol_encoding`
- `fixeol_recursive`
- `fixeol_backup`
- `fixeol_progress`
- `fixeol_verbose`
- `fixeol_pause`
