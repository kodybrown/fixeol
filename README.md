# fixeol

![dotnet-core-build-develop](https://github.com/kodybrown/fixeol/workflows/dotnet-core-build/badge.svg)
![dotnet-core-release](https://github.com/kodybrown/fixeol/workflows/dotnet-core-release/badge.svg)

[latest-release](https://github.com/kodybrown/fixeol/releases/latest/)
![latest-release](https://github.com/kodybrown/fixeol/releases/latest/badge.svg)

Fixes the line-endings of the specified file(s). The line-ending used can be specified via the command-line.

You can fix a file at a time or use wildcards; even multiple file patterns. For instance:

    fixeol /r *.css *.js

will fix the line endings of all .css and .js files in the current and all sub-directories.

## Usage

    feol.exe [options] [commands] [-file] "filename"

       file      the full filename of the file to manipulate.
                 file or feol_file (below) is required.

    commands:

       /eol [crlf|cr|lf]  override the default eol settings of the operating system.
                          also supports [\r\n|\r|\n] for backwards compatibility.

       /encoding [enc]    override the default encoding settings of source file.
                          supports `ascii`, `utf8`, `utf7`, `utc32`, and `unicode`.

    options:

       /v /verbose        output additional details (default:false)
       /b /backup         backup (default:false)
       /r /recurse        apply the file pattern(s) to the current
                          and all sub-directories (default:false)
       /p /pause          pause when finished (default:false)

       /set               displays the current environment variables
                          then exits. All other options are ignored.

       *use ! to set any option to opposite value. overrides environment variables.
        for example use /!v to not use verbose.

    environment variables:

       feol_file=filename           sets -file "filename"
       feol_verbose=true|false      sets /v or /!v
       feol_pause=true|false        sets /pause or /!pause

         *command-line arguments override environment variables
