namespace Bricksoft.PowerCode;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

public class ConsoleApp
{
  public string AppName { get; protected set; } = string.Empty;
  public string? AppVersion { get; protected set; } = null;
  public string? AppEnvarPrefix { get; protected set; } = null;
  public string? AppDescription { get; protected set; } = null;
  public string? AppCopyright { get; protected set; } = null;

  private readonly List<string> Arguments = [];

  /// <summary>
  /// When '-help' is specified on the command-line, the help information is output. If a topic is
  /// provided (e.g. '--help topic'), then help for that specific topic is shown if available.
  /// </summary>
  public bool OptHelp { get; protected set; } = false;

  /// <summary>
  /// This property is what actually captures the '-help' and '-help topic' command-line arguments.
  /// Setting this property will also set OptHelp to true, indicating that help should be shown. The
  /// presence of a value in this property indicates that the user requested help, and the value
  /// itself can be used to determine if they requested general help or help for a specific topic.
  /// </summary>
  [NamedParameters(["help", "?", "h"], valueIsOptional: true)]
  public string? OptHelpTopic {
    get => field;
    protected set {
      field = value;
      // Always set OptHelp to true even if a topic is not provided, since the presence of the help flag indicates that help should be shown.
      // Supports both `--help` and `--help topic` styles.
      OptHelp = true;
    }
  }

  /// <summary>
  /// When specified on the command-line, only the app name and version is output.
  /// </summary>
  [NamedParameters("v")]
  public bool OptVersion { get; protected set; } = false;

  /// <summary>
  /// When specified on the command-line, the app details, copyright, and version information is
  /// output. This includes additional details such as build date, commit hash, and other relevant
  /// metadata.
  /// </summary>
  [NamedParameters("version")]
  public bool OptVersionFull { get; protected set; } = false;

  /// <summary>
  /// Pauses the console application before exiting, allowing the user to see any final output
  /// before the window closes. This is especially useful when running the application by
  /// double-clicking the executable in a file explorer, where the console window would otherwise
  /// close immediately after execution completes.
  /// </summary>
  /// <remarks>
  /// This property is virtual so the app can override it, setting the envar name.
  /// </remarks>
  [NamedParameters("pause")]
  public virtual bool OptPause { get; protected set; } = false;

  /// <summary>
  /// Enables verbose output for the console application, providing additional details about the
  /// execution process. This is useful for debugging or understanding the internal workings of the
  /// application.
  /// </summary>
  /// <remarks>
  /// This property is virtual so the app can override it, setting the envar name.
  /// </remarks>
  [NamedParameters("verbose", "e")]
  public virtual bool OptVerbose { get; protected set; } = false;

  /// <summary>
  /// Initializes a new instance of the ConsoleApp class with the specified application name and
  /// command-line arguments.
  /// </summary>
  /// <remarks>
  /// The provided arguments are stored for later access and processing within the application.
  /// Ensure that the appName parameter is a non-empty string to avoid initialization errors.
  /// </remarks>
  /// <param name="args">
  /// An array containing the command-line arguments to be processed by the application. Cannot be
  /// null.
  /// </param>
  /// <param name="appName">
  /// The name of the application to be used for display and identification purposes. Cannot be null
  /// or empty.
  /// </param>
  public ConsoleApp( string[] args, string appName )
  {
    Arguments = args?.ToList() ?? [];
    AppName = appName;

    // Get the version from the assembly - try InformationalVersion first (supports semver),
    // then FileVersion, then fall back to AssemblyVersion
    var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

    // Try InformationalVersion (e.g., "1.2.3-beta+abc123")
    var infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
    if (infoVersionAttr != null && !string.IsNullOrEmpty(infoVersionAttr.InformationalVersion)) {
      AppVersion = infoVersionAttr.InformationalVersion;
    } else {
      // Try FileVersion (e.g., "1.2.3.4")
      var fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
      if (fileVersionAttr != null && !string.IsNullOrEmpty(fileVersionAttr.Version)) {
        AppVersion = fileVersionAttr.Version;
      } else {
        // Fall back to AssemblyVersion
        var version = assembly.GetName().Version;
        if (version != null) {
          AppVersion = version.ToString();
        }
      }
    }
  }

  protected void PauseFlagHandler()
  {
    if (OptPause) {
      Console.Write("Press any key to exit: ");
      Console.ReadKey(true);
      Console.CursorLeft = 0;
      Console.Write("                       ");
      Console.CursorLeft = 0;
    }
  }

  protected static (string arg, bool isFlag, bool flagValue) ParseArgument( string arg )
  {
    var isFlag = false;
    var slashIsFlag = OperatingSystem.IsWindows();

    while (arg.StartsWith('-') || (slashIsFlag && arg.StartsWith('/'))) {
      isFlag = true;
      arg = arg[1..];
    }

    var flagVal = true;
    if (isFlag && arg.StartsWith('!')) {
      flagVal = false;
      arg = arg.TrimStart('!');
    }
    return (arg, isFlag, flagVal);
  }

  /// <summary>
  /// Parses the command-line arguments provided to the application, binding them to properties
  /// decorated with the `NamedParameters` and `NamedCommands` attributes. It also handles any
  /// remaining arguments using a property decorated with the `EverythingElse` attribute if defined.
  /// The method returns a tuple indicating whether the application should exit immediately (e.g.
  /// after showing help or version information) and an exit code to be used when exiting. The
  /// `allowEnvarValues` parameter controls whether environment variable values should be applied to
  /// properties with the `NamedParameters` attribute before parsing command-line arguments,
  /// allowing for a flexible configuration where users can set options via environment variables as
  /// an alternative to command-line arguments, with command-line arguments taking precedence if
  /// both are provided.
  /// </summary>
  protected (bool exit, int code) ParseCommandLineArguments( bool allowEnvarValues = false )
  {
    var exit_code = 0;

    // Get all NamedParameter and NamedCommand properties (including private ones).
    var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    var everythingElseProp = GetType().GetProperties(bindingFlags)
      .FirstOrDefault(p => Attribute.IsDefined(p, typeof(EverythingElseAttribute)));

    var namedParameters = GetType().GetProperties(bindingFlags)
      .Where(p => Attribute.IsDefined(p, typeof(NamedParametersAttribute)))
      .ToList();

    var namedCommands = GetType().GetProperties(bindingFlags)
      .Where(p => Attribute.IsDefined(p, typeof(NamedCommandsAttribute)))
      .ToList();

    void SetValue( PropertyInfo? paramProp, string arg, bool is_flag, bool flag_val, ref int i )
    {
      exit_code = 0;
      if (paramProp != null) {
        // Found a matching named parameter property.
        if (paramProp.PropertyType == typeof(bool)) {
          // Boolean flag
          paramProp.SetValue(this, flag_val);
        } else if (paramProp.PropertyType == typeof(string)) {
          // String parameter
          var attr = (NamedParametersAttribute?)Attribute.GetCustomAttribute(paramProp, typeof(NamedParametersAttribute));
          var isOptional = attr?.ValueIsOptional ?? false;

          // Accept next token even if it starts with '-' or '/' to allow absolute paths and values like '-foo'
          i = GetSubArgument(Arguments, i, out var found, out var value, ignoreFlagSymbols: !isOptional);

          if (found) {
            paramProp.SetValue(this, value);
          } else if (isOptional) {
            // Value is optional - set to empty string to indicate flag was present but no value provided
            paramProp.SetValue(this, string.Empty);
          } else {
            Console.WriteLine($"Missing string value for argument: {arg}");
            exit_code = -100;
          }
        } else if (paramProp.PropertyType == typeof(int)) {
          // Integer parameter
          i = GetSubArgument(Arguments, i, out var found, out var value);
          if (found && int.TryParse(value, out var intValue)) {
            paramProp.SetValue(this, intValue);
          } else {
            Console.WriteLine($"Invalid or missing integer value for argument: {arg}");
            exit_code = -101;
          }
        } else if (paramProp.PropertyType == typeof(string[])) {
          // string[] parameter
          // Accept next token even if it starts with '-' or '/' to allow absolute paths and values like '-foo'
          i = GetSubArgument(Arguments, i, out var found, out var value, ignoreFlagSymbols: true);
          if (found) {
            var ar = paramProp.GetValue(this) as string[];

            // Is the property currently null?
            if (ar is null) {
              // Create a new array.
              ar = [];
              paramProp.SetValue(this, ar);
            }

            // Remove surrounding quotes if present.
            while ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\''))) {
              value = value[1..^1];
            }

            // Check for remove syntax.
            if (value.StartsWith('!')) {
              // Remove the value from the array.
              var toRemove = value[1..];
              ar = ar.Where(x => !x.Equals(toRemove, StringComparison.InvariantCultureIgnoreCase)).ToArray();
              paramProp.SetValue(this, ar);
              return;
            }

            // Does value already exist in the array?
            if (ar.Contains(value)) {
              // If so, skip it.
              return;
            }

            // Add value to the array.
            Array.Resize(ref ar, ar.Length + 1);
            ar[^1] = value!;
            paramProp.SetValue(this, ar);
          } else {
            Console.WriteLine($"Missing string value for argument: {arg}");
            exit_code = -100;
          }
        } else {
          Console.WriteLine($"Unsupported parameter type for argument: {arg} (must be bool, int, or string)");
          exit_code = -102;
        }
      }
    }

    //
    // Apply environment variable values to any properties with the NamedParameters attribute where
    // AllowEnvar is true. The environment variable name is derived from the first element in the
    // NamedParameters array, prefixed with AppEnvarPrefix. This allows users to set options via
    // environment variables as an alternative to command-line arguments, with command-line arguments
    // taking precedence if both are provided.
    //
    if (allowEnvarValues) {
      foreach (var paramProp in namedParameters) {
        var attr = (NamedParametersAttribute?)Attribute.GetCustomAttribute(paramProp, typeof(NamedParametersAttribute));
        if (attr != null && attr.AllowEnvar && attr.NamedParameters.Length > 0) {
          // Use first parameter name + prefix as the environment variable name
          var envarName = $"{AppEnvarPrefix}{attr.NamedParameters[0]}";
          var envVal = Environment.GetEnvironmentVariable(envarName);
          if (!string.IsNullOrEmpty(envVal)) {
            // We have an environment variable value for this parameter.
            if (paramProp.PropertyType == typeof(bool)) {
              var boolVal = envVal switch {
                "true" or "t" or "yes" or "1" => true,
                _ => false,
              };
              paramProp.SetValue(this, boolVal);
            } else if (paramProp.PropertyType == typeof(int)) {
              if (int.TryParse(envVal, out var intVal)) {
                paramProp.SetValue(this, intVal);
              } else {
                Console.WriteLine($"Invalid integer value for environment variable {envarName}: {envVal}");
              }
            } else if (paramProp.PropertyType == typeof(string)) {
              paramProp.SetValue(this, envVal);
            } else if (paramProp.PropertyType == typeof(string[])) {
              var ar = envVal.Split([';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToArray();
              paramProp.SetValue(this, ar);
            } else {
              throw new Exception("Unsupported parameter type for environment variable property: " + paramProp.PropertyType.Name);
            }
          }
        }
      }
    }

    //
    // Parse the command-line arguments, binding them to properties with the NamedParameters and NamedCommands attributes,
    // and handling any remaining arguments with the EverythingElse property if defined.
    //
    for (var i = 0; i < Arguments.Count; i++) {
      var (arg, is_flag, flag_val) = ParseArgument(Arguments[i]);
      var arg_lower = arg.ToLowerInvariant();

      if (is_flag) {
        // Check if any of the NamedParameter properties match this argument.
        var paramProp = namedParameters.FirstOrDefault(p =>
        {
          var attr = (NamedParametersAttribute?)Attribute.GetCustomAttribute(p, typeof(NamedParametersAttribute));
          return attr != null && attr.NamedParameters.Any(n => n.Equals(arg, StringComparison.InvariantCultureIgnoreCase));
        });
        if (paramProp != null) {
          SetValue(paramProp, arg, is_flag, flag_val, ref i);
          if (exit_code != 0) {
            break;
          }
          continue;
        }
      }

      // Check if any of the NamedCommand properties match this argument.
      var commandProp = namedCommands.FirstOrDefault(p =>
      {
        var attr = (NamedCommandsAttribute?)Attribute.GetCustomAttribute(p, typeof(NamedCommandsAttribute));
        return attr != null && attr.NamedCommands.Any(n => n.Equals(arg_lower, StringComparison.InvariantCultureIgnoreCase));
      });
      if (commandProp != null) {
        SetValue(commandProp, arg, is_flag, flag_val, ref i);
        if (exit_code != 0) {
          break;
        }
        continue;
      }

      if (everythingElseProp != null) {
        // If we have an "everything else" property, handle it based on its type
        if (everythingElseProp.PropertyType == typeof(string)) {
          // string property - concatenate remaining arguments
          var prevValue = everythingElseProp.GetValue(this) as string;
          everythingElseProp.SetValue(this, $"{prevValue} {Arguments[i]}".Trim());
        } else if (everythingElseProp.PropertyType == typeof(List<string>)) {
          // List<string> property - add each argument to the list
          if (everythingElseProp.GetValue(this) is not List<string> list) {
            list = new List<string>();
            everythingElseProp.SetValue(this, list);
          }
          list.Add(Arguments[i]);
        } else {
          Console.WriteLine($"Unsupported EverythingElse property type: {everythingElseProp.PropertyType.Name} (must be string or List<string>)");
          exit_code = -103;
          break;
        }
        continue; // Continue processing remaining arguments instead of breaking
      } else {
        Console.WriteLine($"Unknown argument: {arg}");
        exit_code = -110;
        break;
      }
    }

    // Check the common flags.
    //if (OptHelp || exit_code != 0) {
    //  ShowHelp(OptHelpTopic);
    //  return (true, exit_code);
    //}
    if (OptVersionFull || OptVersion) {
      ShowVersion();
      return (true, 0);
    }

    return (exit_code != 0, exit_code);
  }

  /// <summary>
  /// Retrieves the next argument from the specified list, optionally skipping flag symbols, and
  /// indicates whether a valid argument was found.
  /// </summary>
  /// <remarks>
  /// If the next argument appears to be a flag symbol and ignoreFlagSymbols is set to true, the
  /// method will skip it. On Windows, both '-' and '/' are considered flag symbols. The caller
  /// should ensure that the index parameter is within the valid range of the arguments list.
  /// </remarks>
  /// <param name="arguments">The list of command-line arguments to process. Cannot be null.</param>
  /// <param name="i">
  /// The index of the current argument in the list. Used to determine the position of the next
  /// argument.
  /// </param>
  /// <param name="found">
  /// When the method returns, contains a value indicating whether a valid argument was found at the
  /// next position.
  /// </param>
  /// <param name="result">
  /// When the method returns, contains the value of the next argument if found; otherwise, null.
  /// </param>
  /// <param name="ignoreFlagSymbols">
  /// A value indicating whether to ignore flag symbols (such as '-' or '/' on Windows) when
  /// determining the next argument.
  /// </param>
  /// <returns>The updated index in the arguments list after processing.</returns>
  protected static int GetSubArgument( List<string> arguments, int i, out bool found, out string? result, bool ignoreFlagSymbols = false )
  {
    ArgumentNullException.ThrowIfNull(arguments);

    found = false;
    result = null;

    var slashIsFlag = OperatingSystem.IsWindows();

    if (i < arguments.Count - 1) {
      var next = arguments[i + 1];
      var looksLikeFlag = next.StartsWith('-') || (slashIsFlag && next.StartsWith('/'));
      if (ignoreFlagSymbols || !looksLikeFlag) {
        found = true;
        result = arguments[++i];
      }
    }

    return i;
  }

  /// <summary>
  /// Retrieves the sub-argument from a list of command-line arguments at the specified index and
  /// attempts to convert it to the specified type.
  /// </summary>
  /// <remarks>
  /// This method supports conversion of sub-arguments to common types such as string, bool,
  /// DateTime, TimeSpan, and numeric types. If the sub-argument cannot be converted to the
  /// specified type, <paramref name="found"/> is set to <see langword="false"/> and
  /// <paramref name="result"/> is set to the default value for <typeparamref name="T"/> . For
  /// boolean types, if no sub-argument is present, <paramref name="result"/> is set to
  /// <see langword="true"/> .
  /// </remarks>
  /// <typeparam name="T">
  /// The type to which the sub-argument will be converted. Supported types include string, bool,
  /// DateTime, TimeSpan, and numeric types.
  /// </typeparam>
  /// <param name="arguments">
  /// A list of command-line arguments from which the sub-argument is extracted. Cannot be null or
  /// empty.
  /// </param>
  /// <param name="index">
  /// The zero-based index of the argument to examine. Must be within the bounds of the arguments
  /// list.
  /// </param>
  /// <param name="found">
  /// When the method returns, contains <see langword="true"/> if a valid sub-argument was found and
  /// converted; otherwise, <see langword="false"/> .
  /// </param>
  /// <param name="result">
  /// When the method returns, contains the converted sub-argument if found; otherwise, the default
  /// value for type <typeparamref name="T"/> .
  /// </param>
  /// <param name="ignoreFlagSymbols">
  /// If <see langword="true"/> , ignores flag symbols (such as '-' or '/') when searching for
  /// sub-arguments.
  /// </param>
  /// <returns>
  /// The index of the argument containing the retrieved sub-argument, or the original index if no
  /// valid sub-argument is found.
  /// </returns>
  /// <exception cref="ArgumentNullException">
  /// Thrown if <paramref name="arguments"/> is null or empty.
  /// </exception>
  /// <exception cref="NotSupportedException">
  /// Thrown if the specified type parameter <typeparamref name="T"/> is not supported for
  /// conversion.
  /// </exception>
  protected static int GetSubArgument<T>( List<string> arguments, int index, out bool found, out T? result, bool ignoreFlagSymbols = false )
  {
    if (arguments is null || arguments.Count == 0) {
      throw new ArgumentNullException(nameof(arguments));
    }

    string? subItem = null;

    if (index < arguments.Count - 1) {
      if (ignoreFlagSymbols || (!arguments[index + 1].StartsWith('-')
                             && !arguments[index + 1].StartsWith('/'))) {
        subItem = arguments[index + 1];
      }
    }

    if (subItem != null) {
      index++;
      found = true;

      if (typeof(T) == typeof(string)) {
        result = (T)(object)subItem;
        return index;
      } else if (typeof(T) == typeof(bool)) {
        result = subItem switch {
          "true" or "t" or "yes" or "1" => (T)(object)true,
          _ => (T)(object)false,
        };
        return index;
      } else if (typeof(T) == typeof(DateTime)) {
        if (subItem.ToLower() is "now" or "utcnow") {
          result = (T)(object)DateTime.UtcNow;
          return index;
        }
        if (DateTime.TryParse(subItem, null, DateTimeStyles.AssumeUniversal, out var val)) {
          if (val.Kind != DateTimeKind.Utc) {
            val = val.ToUniversalTime();
          }
          result = (T)(object)val;
          return index;
        }
      } else if (typeof(T) == typeof(TimeSpan)) {
        if (TimeSpan.TryParse(subItem, out var val)) {
          result = (T)(object)val;
          return index;
        }
      } else if (typeof(T) == typeof(short)) {
        if (short.TryParse(subItem, out var val)) {
          result = (T)(object)val;
          return index;
        }
      } else if (typeof(T) == typeof(int)) {
        if (int.TryParse(subItem, out var val)) {
          result = (T)(object)val;
          return index;
        }
      } else if (typeof(T) == typeof(long)) {
        if (long.TryParse(subItem, out var val)) {
          result = (T)(object)val;
          return index;
        }
      } else if (typeof(T) == typeof(ushort)) {
        if (ushort.TryParse(subItem, out var val)) {
          result = (T)(object)val;
          return index;
        }
      } else if (typeof(T) == typeof(uint)) {
        if (uint.TryParse(subItem, out var val)) {
          result = (T)(object)val;
          return index;
        }
      } else if (typeof(T) == typeof(ulong)) {
        if (ulong.TryParse(subItem, out var val)) {
          result = (T)(object)val;
          return index;
        }
      } else if (typeof(T) == typeof(float)) {
        if (float.TryParse(subItem, out var val)) {
          result = (T)(object)val;
          return index;
        }
      } else if (typeof(T) == typeof(double)) {
        if (double.TryParse(subItem, out var val)) {
          result = (T)(object)val;
          return index;
        }
      } else if (typeof(T) == typeof(decimal)) {
        if (decimal.TryParse(subItem, out var val)) {
          result = (T)(object)val;
          return index;
        }
      } else {
        throw new NotSupportedException(nameof(T));
      }
    }

    if (typeof(T) == typeof(bool)) {
      result = (T)(object)true;
      found = true;
    } else {
      result = default;
      found = false;
    }

    return index;
  }

  /// <summary>
  /// Extracts sub-arguments from a list of command-line arguments, starting at the specified index,
  /// and adds them to the provided items list.
  /// </summary>
  /// <remarks>
  /// On Windows, both '-' and '/' are considered flag symbols. Sub-arguments are extracted until a
  /// flag symbol is encountered or the end of the list is reached. Empty or whitespace-only
  /// arguments are skipped.
  /// </remarks>
  /// <param name="arguments">
  /// The list of command-line arguments to process. This parameter must not be null.
  /// </param>
  /// <param name="i">
  /// The zero-based index in the arguments list from which to begin extracting sub-arguments. Must
  /// be within the bounds of the arguments list.
  /// </param>
  /// <param name="found">
  /// When this method returns, contains a value indicating whether any sub-arguments were found and
  /// added to the items list.
  /// </param>
  /// <param name="items">
  /// The list to which extracted sub-arguments are added. This parameter must not be null.
  /// </param>
  /// <param name="ignoreFlagSymbols">
  /// A value indicating whether to ignore flag symbols (such as '-' or '/') when processing
  /// arguments. If set to <see langword="true"/> , flag symbols are ignored; otherwise, processing
  /// stops at the first flag symbol.
  /// </param>
  /// <returns>The index in the arguments list after processing the sub-arguments.</returns>
  protected static int GetSubArguments( List<string> arguments, int i, out bool found, List<string> items, bool ignoreFlagSymbols = false )
  {
    ArgumentNullException.ThrowIfNull(arguments);
    ArgumentNullException.ThrowIfNull(items);

    found = false;

    var slashIsFlag = OperatingSystem.IsWindows();

    while (i < arguments.Count - 1) {
      var next = arguments[i + 1];
      var looksLikeFlag = next.StartsWith('-') || (slashIsFlag && next.StartsWith('/'));
      if (!ignoreFlagSymbols && looksLikeFlag) {
        break;
      }
      if (string.IsNullOrWhiteSpace(next)) {
        i++;
        continue;
      }

      found = true;
      items.Add(arguments[++i]);
    }

    return i;
  }

  /// <summary>
  /// Processes command-line arguments starting at the specified index and extracts sub-arguments
  /// into the provided items list.
  /// </summary>
  /// <remarks>
  /// On Windows, arguments starting with '/' are treated as flag symbols unless ignoreFlagSymbols
  /// is set to true. The method skips empty or whitespace-only arguments and stops processing when
  /// a flag symbol is encountered, unless ignored.
  /// </remarks>
  /// <typeparam name="T">
  /// The type of item to be added to the items list. Must be compatible with the parsed
  /// sub-arguments.
  /// </typeparam>
  /// <param name="arguments">The list of command-line arguments to process. Cannot be null.</param>
  /// <param name="i">
  /// The index in the arguments list from which to begin processing. Must be within the bounds of
  /// the list.
  /// </param>
  /// <param name="found">
  /// When the method returns, contains a value indicating whether a valid sub-argument was found
  /// during processing.
  /// </param>
  /// <param name="items">
  /// The list to which extracted sub-arguments are added. Cannot be null.
  /// </param>
  /// <param name="ignoreFlagSymbols">
  /// A value indicating whether to ignore flag symbols (such as '-' or '/' on Windows) when
  /// processing arguments. Defaults to false.
  /// </param>
  /// <returns>The index of the last processed argument in the list.</returns>
  protected static int GetSubArguments<T>( List<string> arguments, int i, out bool found, List<T> items, bool ignoreFlagSymbols = false )
  {
    ArgumentNullException.ThrowIfNull(arguments);
    ArgumentNullException.ThrowIfNull(items);

    found = false;

    var slashIsFlag = OperatingSystem.IsWindows();

    while (i < arguments.Count - 1) {
      var next = arguments[i + 1];
      var looksLikeFlag = next.StartsWith('-') || (slashIsFlag && next.StartsWith('/'));
      if (!ignoreFlagSymbols && looksLikeFlag) {
        break;
      }
      if (string.IsNullOrWhiteSpace(next)) {
        i++;
        continue;
      }

      i = GetSubArgument<T>(arguments, i, out found, out var result, ignoreFlagSymbols);
      if (found && result != null) {
        items.Add(result);
      }
    }

    return i;
  }

  /// <summary>
  /// Displays application header information, including version, description, and copyright
  /// details, to the console.
  /// </summary>
  /// <remarks>
  /// This method is intended for informational output and does not affect application state or
  /// functionality. It can be used to provide users with basic application details at startup or
  /// upon request.
  /// </remarks>
  protected void ShowHeader()
  {
    Console.Out.WriteLine($"{AppName} v{AppVersion}");
    if (!string.IsNullOrEmpty(AppDescription)) {
      Console.Out.WriteLine(AppDescription);
    }
    if (!string.IsNullOrEmpty(AppCopyright)) {
      Console.Out.WriteLine(AppCopyright);
    }
  }

  /// <summary>
  /// Displays the current version information to the user then exits. Showing the full version with
  /// details ('-version'; <see cref="OptVersionFull"/> ), or just the version ('-v';
  /// <see cref="OptVersion"/> ).
  /// </summary>
  protected void ShowVersion()
  {
    if (OptVersionFull) {
      ShowHeader();
    } else {
      Console.WriteLine($"{AppName} v{AppVersion}");
    }
  }

  /// <summary>
  /// Shows help information for a specific topic or general help if no topic is provided.
  /// </summary>
  protected void ShowHelp( string? topic = null, bool? includeExamples = null )
  {
    ShowHeader();

    //var to_flag = string.IsNullOrEmpty(topic) ? string.Empty : " " + topic;
    //var ex_flag = includeExamples.HasValue ? (includeExamples.Value ? " true" : "") : string.Empty;
    //var prefix = App.UserPrefix;

    //var cmd = new HelpCommand();
    //var response = cmd.Execute(new InputCommand { Command = $"{prefix}{cmd.Keywords.First()}{to_flag}{ex_flag}", IsFromAssistant = false });

    //if (response is ToolOutput.ProduceResponseText rt) {
    //  // Help was requested via command line (--help) → strip color variables for console output.
    //  Console.WriteLine(ColorExtensions.StripColorVariables(rt.Text));

    //  //if (OptHelp) {
    //  //} else {
    //  //  // Help was requested in-app → render with color.
    //  //  //  Renderer.RenderOutput(new RenderItem(MessageType.FromSystem | MessageType.ForUser, rt.Text ?? string.Empty));
    //  //}
    //}
  }

  /// <summary>
  /// Displays the current environment variables that are supported by properties decorated with the
  /// NamedParameters attribute where AllowEnvar is true.
  /// </summary>
  /// <remarks>
  /// If no supported environment variables are found, a message indicating this is displayed. Each
  /// environment variable is shown with the application-specific prefix if one is defined. The
  /// environment variable name is derived from the first parameter name in the NamedParameters
  /// array.
  /// </remarks>
  protected void ShowCurrentEnvars( bool showHeader = true )
  {
    if (showHeader) {
      ShowHeader();
    }

    Console.Out.WriteLine();
    Console.Out.WriteLine("Environment Variables: ");

    // Find all properties with NamedParameters attribute where AllowEnvar == true
    var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    var propertiesWithEnvar = GetType().GetProperties(bindingFlags)
      .Where(p =>
      {
        var attr = (NamedParametersAttribute?)Attribute.GetCustomAttribute(p, typeof(NamedParametersAttribute));
        return attr != null && attr.AllowEnvar;
      })
      .ToList();

    if (propertiesWithEnvar.Count == 0) {
      Console.Out.WriteLine("  <none found>");
    } else {
      foreach (var prop in propertiesWithEnvar) {
        var attr = (NamedParametersAttribute?)Attribute.GetCustomAttribute(prop, typeof(NamedParametersAttribute));
        if (attr != null && attr.NamedParameters.Length > 0) {
          // Use the first parameter name + AppEnvarPrefix as the environment variable name
          var envarName = attr.NamedParameters[0];
          if (AppEnvarPrefix is not null) {
            envarName = $"{AppEnvarPrefix}{envarName}";
          }
          var envValue = Environment.GetEnvironmentVariable(envarName);
          var envarNameLabel = envarName + ":";

          if (string.IsNullOrEmpty(envValue)) {
            envValue = "<notset>";
          }
          Console.Out.WriteLine($" - {envarNameLabel,-16} {envValue}");
        }
      }
    }

    Console.Out.WriteLine("");
    Console.Out.WriteLine("  *command-line arguments override environment variables");

    Console.Out.WriteLine();
  }
}

/// <summary>
/// Specifies alternative names that can be used to reference a property when binding named
/// parameters. See the `new` in this example: `GroundZero.exe --new session-name`.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
internal class NamedParametersAttribute : Attribute
{
  public string[] NamedParameters { get; } = [];
  public bool AllowEnvar { get; }
  public bool ValueIsOptional { get; }

  public NamedParametersAttribute( params string[] names )
  {
    NamedParameters = names ?? [];
    AllowEnvar = false;
    ValueIsOptional = false;
  }

  public NamedParametersAttribute( string[] names, bool allowEnvar = false, bool valueIsOptional = false )
  {
    NamedParameters = names ?? [];
    AllowEnvar = allowEnvar;
    ValueIsOptional = valueIsOptional;
  }
}

/// <summary>
/// Specifies alternative names that can be used to reference a property when binding named
/// commands. See the `new` in this example: `GroundZero.exe new session-name`.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
internal class NamedCommandsAttribute : Attribute
{
  public string[] NamedCommands { get; }
  public NamedCommandsAttribute( params string[] names ) { NamedCommands = names; }
}

/// <summary>
/// Indicates that a property should be bound to any command-line arguments that don't match any
/// other properties with the `NamedParameters` or `NamedCommands` attribute. This allows for a
/// "catch-all" property to receive unrecognized arguments, which can be useful for handling
/// free-form input or passing through arguments to other components.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
internal class EverythingElseAttribute : Attribute
{
  public EverythingElseAttribute() { }
}

//internal class CliArgumentAttribute : Attribute
//{
//  public bool? HandlesAllNonParameters { get; init; } = false;
//  public string[] NamedParameters { get; } = [];
//  public string[] NamedCommands { get; } = [];
//  public string EnvarName { get; }
//  public CliArgumentAttribute( string[]? namedParameters = null, string[]? namedCommands = null, string? envarName = null, bool? handlesAllNonParameters = null )
//  {
//    NamedParameters = namedParameters ?? [];
//    NamedCommands = namedCommands ?? [];
//    EnvarName = envarName ?? NamedParameters.FirstOrDefault() ?? string.Empty;
//    HandlesAllNonParameters = handlesAllNonParameters ?? false;
//  }
//}
