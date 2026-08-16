namespace Bricksoft.PowerCode;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

/// <summary>
/// Provides command-line argument parsing and binding functionality for applications.
/// This class uses reflection to bind command-line arguments to properties decorated with
/// NamedParameters, NamedCommands, and UnhandledArguments attributes.
/// </summary>
public class CliArgumentBinder
{
  private const int DefaultConsoleWidth = 80;

  public string AppName { get; set; } = string.Empty;
  public string? AppVersion { get; set; } = null;
  public string? AppEnvarPrefix { get; set; } = null;
  public string? AppDescription { get; set; } = null;
  public string? AppCopyright { get; set; } = null;
  public string? AppCompany { get; set; } = null;
  public string? AppProduct { get; set; } = null;
  public string? AppAuthors { get; set; } = null;
  public string? AppRepositoryUrl { get; set; } = null;

  private readonly List<string> _arguments = [];

  /// <summary>
  /// Initializes a new instance of the CliArgumentBinder class with the specified application name and
  /// command-line arguments.
  /// </summary>
  /// <remarks>
  /// The provided arguments are stored for later access and processing within the application. If
  /// appName is not provided, the application name is read from the assembly's Product or Title
  /// attribute. Version, description, copyright, and other metadata are automatically loaded from
  /// assembly attributes.
  /// </remarks>
  /// <param name="args">
  /// An array containing the command-line arguments to be processed by the application. Cannot be
  /// null.
  /// </param>
  /// <param name="appName">
  /// The name of the application to be used for display and identification purposes. If null or
  /// empty, the name is read from the assembly's Product or Title attribute.
  /// </param>
  public CliArgumentBinder( string[] args, string? appName = null )
  {
    _arguments = args?.ToList() ?? [];

    // Get the assembly to read metadata from
    var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

    // Get application name - prefer parameter, then Product, then Title, then assembly name
    if (!string.IsNullOrWhiteSpace(appName)) {
      AppName = appName;
    } else {
      var titleAttr = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
      var productAttr = assembly.GetCustomAttribute<AssemblyProductAttribute>();
      AppName = titleAttr?.Title
             ?? productAttr?.Product
             ?? assembly.GetName().Name
             ?? "CliArgumentBinder";
    }

    // Get version - try InformationalVersion first (supports semver), then FileVersion, then AssemblyVersion
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

    // Get description from assembly attribute
    var descAttr = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
    if (descAttr != null && !string.IsNullOrEmpty(descAttr.Description)) {
      AppDescription = descAttr.Description;
    }

    // Get copyright from assembly attribute
    var copyrightAttr = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
    if (copyrightAttr != null && !string.IsNullOrEmpty(copyrightAttr.Copyright)) {
      AppCopyright = copyrightAttr.Copyright;
    }

    // Get company from assembly attribute
    var companyAttr = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
    if (companyAttr != null && !string.IsNullOrEmpty(companyAttr.Company)) {
      AppCompany = companyAttr.Company;
    }

    // Get product from assembly attribute
    var productAttribute = assembly.GetCustomAttribute<AssemblyProductAttribute>();
    if (productAttribute != null && !string.IsNullOrEmpty(productAttribute.Product)) {
      AppProduct = productAttribute.Product;
    }

    // Get repository URL from assembly metadata (common in .NET 5+)
    var metadataAttrs = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
    foreach (var metadata in metadataAttrs) {
      if (metadata.Key == "RepositoryUrl" && !string.IsNullOrEmpty(metadata.Value)) {
        AppRepositoryUrl = metadata.Value;
      } else if (metadata.Key == "Authors" && !string.IsNullOrEmpty(metadata.Value)) {
        AppAuthors = metadata.Value;
      }
    }
  }

  private static (string arg, bool isFlag, bool flagValue) ParseArgument( string arg )
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

  private static bool TryParseEnum( Type enumType, string value, out object? result )
  {
    if (Enum.TryParse(enumType, value, ignoreCase: true, out result)) {
      return true;
    }

    return Enum.TryParse(enumType, enumType.Name + value, ignoreCase: true, out result);
  }

  /// <summary>
  /// Parses command-line arguments and binds them to properties on the specified target object
  /// decorated with NamedParameters, NamedCommands, and UnhandledArguments attributes.
  /// </summary>
  /// <param name="target">The object whose properties will be populated with parsed values.</param>
  /// <param name="allowEnvarValues">Whether to apply environment variable values before parsing command-line arguments.</param>
  /// <returns>A ParseResult indicating whether the application should exit and with what code.</returns>
  public ParseResult ParseAndBind( object target, bool allowEnvarValues = false )
  {
    ArgumentNullException.ThrowIfNull(target);

    var exit_code = 0;
    var targetType = target.GetType();

    // Get all CliArgument properties (including private ones).
    var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    var everythingElseProp = targetType.GetProperties(bindingFlags)
      .FirstOrDefault(p => Attribute.IsDefined(p, typeof(UnhandledArgumentsAttribute)));

    var namedParameters = targetType.GetProperties(bindingFlags)
      .Where(p => Attribute.IsDefined(p, typeof(CliArgumentAttribute)))
      .ToList();

    //
    // Apply default values from CliArgument attributes to properties that have them.
    // This gives defaults the lowest priority - they can be overridden by environment variables
    // or command-line arguments.
    //
    foreach (var paramProp in namedParameters) {
      var attr = (CliArgumentAttribute?)Attribute.GetCustomAttribute(paramProp, typeof(CliArgumentAttribute));
      if (attr?.DefaultIfMissing != null) {
        // Check if the default value type matches the property type
        if (paramProp.PropertyType == typeof(bool) && attr.DefaultIfMissing is bool boolDefault) {
          paramProp.SetValue(target, boolDefault);
        } else if (paramProp.PropertyType == typeof(int) && attr.DefaultIfMissing is int intDefault) {
          paramProp.SetValue(target, intDefault);
        } else if (paramProp.PropertyType == typeof(string) && attr.DefaultIfMissing is string stringDefault) {
          paramProp.SetValue(target, stringDefault);
        } else if (paramProp.PropertyType == typeof(string[])) {
          if (attr.DefaultIfMissing is string[] arrayDefault) {
            paramProp.SetValue(target, arrayDefault);
          } else if (attr.DefaultIfMissing is string stringValue) {
            // Support single string that gets converted to array
            paramProp.SetValue(target, new[] { stringValue });
          }
        } else {
          // Try to set the value directly if types match
          var defaultType = attr.DefaultIfMissing.GetType();
          if (paramProp.PropertyType.IsAssignableFrom(defaultType)) {
            paramProp.SetValue(target, attr.DefaultIfMissing);
          } else {
            Console.WriteLine($"Warning: Default value type mismatch for property {paramProp.Name}. Expected {paramProp.PropertyType.Name}, got {defaultType.Name}");
          }
        }
      }
    }

    void SetValue( PropertyInfo? paramProp, string arg, bool is_flag, bool flag_val, ref int i )
    {
      exit_code = 0;
      if (paramProp != null) {
        // Found a matching named parameter property.
        if (paramProp.PropertyType == typeof(bool)) {
          // Boolean flag
          paramProp.SetValue(target, flag_val);
        } else if (paramProp.PropertyType == typeof(string)) {
          // String parameter
          var attr = (CliArgumentAttribute?)Attribute.GetCustomAttribute(paramProp, typeof(CliArgumentAttribute));
          var isOptional = attr?.ValueIsOptional ?? false;

          // Accept next token even if it starts with '-' or '/' to allow absolute paths and values like '-foo'
          i = GetSubArgument(_arguments, i, out var found, out var value, ignoreFlagSymbols: !isOptional);

          if (found) {
            // Validate against allowed values if specified
            if (attr?.AllowedValues != null && attr.AllowedValues.Length > 0) {
              if (!attr.AllowedValues.Any(av => av.Equals(value, StringComparison.InvariantCultureIgnoreCase))) {
                Console.WriteLine($"Invalid value '{value}' for argument: {arg}");
                Console.WriteLine($"Allowed values: {string.Join(", ", attr.AllowedValues)}");
                exit_code = -104;
                return;
              }
            }
            paramProp.SetValue(target, value);
          } else if (isOptional) {
            // Value is optional - set to empty string to indicate flag was present but no value provided
            paramProp.SetValue(target, string.Empty);
          } else {
            Console.WriteLine($"Missing string value for argument: {arg}");
            exit_code = -100;
          }
        } else if (paramProp.PropertyType == typeof(int)) {
          // Integer parameter
          i = GetSubArgument(_arguments, i, out var found, out var value);
          if (found && int.TryParse(value, out var intValue)) {
            paramProp.SetValue(target, intValue);
          } else {
            Console.WriteLine($"Invalid or missing integer value for argument: {arg}");
            exit_code = -101;
          }
        } else if (paramProp.PropertyType == typeof(string[])) {
          // string[] parameter
          // Accept next token even if it starts with '-' or '/' to allow absolute paths and values like '-foo'
          i = GetSubArgument(_arguments, i, out var found, out var value, ignoreFlagSymbols: true);
          if (found) {
            var ar = paramProp.GetValue(target) as string[];

            // Is the property currently null?
            if (ar is null) {
              // Create a new array.
              ar = [];
              paramProp.SetValue(target, ar);
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
              paramProp.SetValue(target, ar);
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
            paramProp.SetValue(target, ar);
          } else {
            Console.WriteLine($"Missing string value for argument: {arg}");
            exit_code = -100;
          }
        } else if (paramProp.PropertyType.IsEnum) {
          var attr = (CliArgumentAttribute?)Attribute.GetCustomAttribute(paramProp, typeof(CliArgumentAttribute));
          var isOptional = attr?.ValueIsOptional ?? false;
          i = GetSubArgument(_arguments, i, out var found, out var value, ignoreFlagSymbols: !isOptional);

          if (found && value != null && TryParseEnum(paramProp.PropertyType, value, out var enumValue)) {
            paramProp.SetValue(target, enumValue);
          } else if (!found && isOptional) {
            var defaultValue = attr?.DefaultIfNoValue ?? Enum.GetValues(paramProp.PropertyType).GetValue(0);
            paramProp.SetValue(target, defaultValue);
          } else {
            Console.WriteLine(found
              ? $"Invalid value '{value}' for argument: {arg}"
              : $"Missing value for argument: {arg}");
            Console.WriteLine(FormatAllowedValues(Enum.GetNames(paramProp.PropertyType)));
            exit_code = found ? -104 : -100;
          }
        } else {
          Console.WriteLine($"Unsupported parameter type for argument: {arg} (must be bool, int, string, string[], or enum)");
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
        var attr = (CliArgumentAttribute?)Attribute.GetCustomAttribute(paramProp, typeof(CliArgumentAttribute));
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
              paramProp.SetValue(target, boolVal);
            } else if (paramProp.PropertyType == typeof(int)) {
              if (int.TryParse(envVal, out var intVal)) {
                paramProp.SetValue(target, intVal);
              } else {
                Console.WriteLine($"Invalid integer value for environment variable {envarName}: {envVal}");
              }
            } else if (paramProp.PropertyType == typeof(string)) {
              paramProp.SetValue(target, envVal);
            } else if (paramProp.PropertyType == typeof(string[])) {
              var ar = envVal.Split([';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToArray();
              paramProp.SetValue(target, ar);
            } else if (paramProp.PropertyType.IsEnum) {
              if (TryParseEnum(paramProp.PropertyType, envVal, out var enumValue)) {
                paramProp.SetValue(target, enumValue);
              } else {
                Console.WriteLine($"Invalid enum value for environment variable {envarName}: {envVal}");
                Console.WriteLine(FormatAllowedValues(Enum.GetNames(paramProp.PropertyType)));
              }
            } else {
              throw new Exception("Unsupported parameter type for environment variable property: " + paramProp.PropertyType.Name);
            }
          }
        }
      }
    }

    //
    // Parse the command-line arguments, binding them to properties with CliArgument attributes.
    // Supports both flag-style (--param) and command-style (param) arguments.
    //
    for (var i = 0; i < _arguments.Count; i++) {
      var (arg, is_flag, flag_val) = ParseArgument(_arguments[i]);
      var arg_lower = arg.ToLowerInvariant();

      PropertyInfo? matchedProp = null;

      if (is_flag) {
        // Argument has a flag prefix (-, --, /) - check NamedParameters
        matchedProp = namedParameters.FirstOrDefault(p =>
        {
          var attr = (CliArgumentAttribute?)Attribute.GetCustomAttribute(p, typeof(CliArgumentAttribute));
          return attr != null && attr.NamedParameters.Any(n => n.Equals(arg, StringComparison.InvariantCultureIgnoreCase));
        });
      } else {
        // No flag prefix - check NamedCommands
        matchedProp = namedParameters.FirstOrDefault(p =>
        {
          var attr = (CliArgumentAttribute?)Attribute.GetCustomAttribute(p, typeof(CliArgumentAttribute));
          return attr != null && attr.NamedCommands.Any(n => n.Equals(arg_lower, StringComparison.InvariantCultureIgnoreCase));
        });
      }

      if (matchedProp != null) {
        SetValue(matchedProp, arg, is_flag, flag_val, ref i);
        if (exit_code != 0) {
          break;
        }
        continue;
      }

      if (everythingElseProp != null) {
        // If we have an "everything else" property, handle it based on its type
        if (everythingElseProp.PropertyType == typeof(string)) {
          // string property - concatenate remaining arguments
          var prevValue = everythingElseProp.GetValue(target) as string;
          everythingElseProp.SetValue(target, $"{prevValue} {_arguments[i]}".Trim());
        } else if (everythingElseProp.PropertyType == typeof(List<string>)) {
          // List<string> property - add each argument to the list
          if (everythingElseProp.GetValue(target) is not List<string> list) {
            list = new List<string>();
            everythingElseProp.SetValue(target, list);
          }
          list.Add(_arguments[i]);
        } else {
          Console.WriteLine($"Unsupported PositionalArguments property type: {everythingElseProp.PropertyType.Name} (must be string or List<string>)");
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

    // Check the common flags by looking for properties on the target with specific names
    var optHelpProp = targetType.GetProperty("OptHelp", bindingFlags);
    var optHelpTopicProp = targetType.GetProperty("OptHelpTopic", bindingFlags);
    var optVersionProp = targetType.GetProperty("OptVersion", bindingFlags);
    var optVersionFullProp = targetType.GetProperty("OptVersionFull", bindingFlags);
    var optShowEnvarsProp = targetType.GetProperty("OptShowEnvVars", bindingFlags);
    var optPauseProp = targetType.GetProperty("OptPause", bindingFlags);

    var shouldShowHelp = (optHelpProp?.GetValue(target) as bool?) == true;
    var helpTopic = optHelpTopicProp?.GetValue(target) as string;
    var shouldShowVersion = (optVersionProp?.GetValue(target) as bool?) == true;
    var shouldShowVersionFull = (optVersionFullProp?.GetValue(target) as bool?) == true;
    var shouldShowEnvars = (optShowEnvarsProp?.GetValue(target) as bool?) == true;
    var shouldPause = (optPauseProp?.GetValue(target) as bool?) == true;

    // Check if help or error occurred
    if (shouldShowHelp || exit_code != 0) {
      ShowUsage(target, helpTopic);
      if (shouldPause) {
        PauseForUser();
      }
      return new ParseResult { ShouldExit = true, ExitCode = exit_code };
    }

    // Check for version flags
    if (shouldShowVersionFull || shouldShowVersion) {
      ShowVersion(full: shouldShowVersionFull);
      return new ParseResult { ShouldExit = true, ExitCode = 0 };
    }

    // Check for envars flag
    if (shouldShowEnvars) {
      ShowCurrentEnvars(target, showHeader: false);
      if (shouldPause) {
        PauseForUser();
      }
      return new ParseResult { ShouldExit = true, ExitCode = 0 };
    }

    // Validate required unhandled arguments
    if (everythingElseProp != null) {
      var unhandledAttr = (UnhandledArgumentsAttribute?)Attribute.GetCustomAttribute(everythingElseProp, typeof(UnhandledArgumentsAttribute));
      if (unhandledAttr != null && unhandledAttr.Required) {
        var isEmpty = false;

        if (everythingElseProp.PropertyType == typeof(string)) {
          var value = everythingElseProp.GetValue(target) as string;
          isEmpty = string.IsNullOrWhiteSpace(value);
        } else if (everythingElseProp.PropertyType == typeof(List<string>)) {
          var list = everythingElseProp.GetValue(target) as List<string>;
          isEmpty = list == null || list.Count == 0;
        }

        if (isEmpty) {
          Console.WriteLine();
          Console.WriteLine($"**** ERROR: Missing required argument: {unhandledAttr.Name}");
          if (!string.IsNullOrWhiteSpace(unhandledAttr.Description)) {
            Console.WriteLine($"  {unhandledAttr.Description}");
          }
          ShowHelpSuggestion();
          return new ParseResult { ShouldExit = true, ExitCode = -105 };
        }
      }
    }

    return new ParseResult { ShouldExit = exit_code != 0, ExitCode = exit_code };
  }

  /// <summary>
  /// Pauses execution and waits for user input before continuing.
  /// </summary>
  private static void PauseForUser()
  {
    Console.Write("Press any key to exit: ");
    Console.ReadKey(true);
    Console.CursorLeft = 0;
    Console.Write("                       ");
    Console.CursorLeft = 0;
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
  protected void ShowHeader( bool includeExtraInfo = false )
  {
    // Extract shortened version (major.minor only) from full version
    var shortVersion = AppVersion;
    if (!string.IsNullOrEmpty(AppVersion)) {
      var parts = AppVersion.Split(['.', '-', '+'], StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length >= 2) {
        shortVersion = $"{parts[0]}.{parts[1]}";
      } else if (parts.Length >= 1) {
        shortVersion = $"{parts[0]}.0";
      }
    }

    // First line: AppName + short version
    Console.Out.WriteLine($"{AppName} v{shortVersion}");

    // Copyright
    if (!string.IsNullOrEmpty(AppCopyright)) {
      Console.Out.WriteLine(AppCopyright);
    }

    // Description
    if (!string.IsNullOrEmpty(AppDescription)) {
      Console.Out.WriteLine(AppDescription);
    }

    if (includeExtraInfo) {
      Console.Out.WriteLine();

      // Authors
      if (!string.IsNullOrEmpty(AppAuthors)) {
        Console.Out.WriteLine($"authors: {AppAuthors}");
      }

      // AppRepositoryUrl
      if (!string.IsNullOrEmpty(AppRepositoryUrl)) {
        Console.Out.WriteLine($"url:     {AppRepositoryUrl}");
      }

      // Second line: Full version with 'v' prefix
      if (!string.IsNullOrEmpty(AppVersion)) {
        Console.Out.WriteLine($"build:   v{AppVersion}");
      }
    }
  }

  /// <summary>
  /// Displays version information to the console.
  /// </summary>
  /// <param name="full">If true, shows full version details; otherwise shows just the version number.</param>
  public void ShowVersion( bool full = false )
  {
    if (full) {
      ShowHeader(true);
    } else {
      Console.WriteLine($"{AppName} v{AppVersion}");
    }
  }

  /// <summary>
  /// Shows help information for a specific topic or general help if no topic is provided.
  /// </summary>
  protected void ShowHelpSuggestion()
  {
    Console.Out.WriteLine();
    Console.Out.WriteLine($"type '{AppName}.exe /?' for help");
  }

  public void ShowUsage( object target, string? topic = null )
  {
    ArgumentNullException.ThrowIfNull(target);
    var targetType = target.GetType();

    ShowHeader(true);
    Console.Out.WriteLine();

    // Determine console width (minimum 80 characters)
    var consoleWidth = GetConsoleWidth();
    var minColWidth = 20;
    var maxColWidth = 40;

    // Get binding flags for reflection
    var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // Find the UnhandledArguments property
    var unhandledProp = targetType.GetProperties(bindingFlags)
      .FirstOrDefault(p => Attribute.IsDefined(p, typeof(UnhandledArgumentsAttribute)));

    // Find all CliArgument properties
    var namedParams = targetType.GetProperties(bindingFlags)
      .Where(p => Attribute.IsDefined(p, typeof(CliArgumentAttribute)))
      .Select(p => new {
        Property = p,
        Attribute = (CliArgumentAttribute)Attribute.GetCustomAttribute(p, typeof(CliArgumentAttribute))!
      })
      .OrderBy(x => x.Attribute.Order)
      .ThenBy(x => x.Attribute.NamedParameters.Length > 0 ? x.Attribute.NamedParameters[0] : x.Attribute.NamedCommands[0])
      .ToList();

    // Calculate optimal column width based on longest option name
    var maxOptionNameLength = namedParams.Max(x => FormatOptionNames(x.Attribute.NamedParameters, x.Property.PropertyType).Length);
    var colWidth = Math.Clamp(maxOptionNameLength + 4, minColWidth, maxColWidth);

    // Check if any options allow environment variables
    var hasEnvarOptions = namedParams.Any(x => x.Attribute.AllowEnvar);

    // Check if any boolean options with envars exist (for the ! footer)
    var hasBoolEnvarOptions = namedParams.Any(x =>
      x.Property.PropertyType == typeof(bool) && x.Attribute.AllowEnvar);

    // ===== USAGE LINE =====
    Console.Out.WriteLine("Usage:");
    Console.Out.WriteLine();

    var usageLine = $"> {AppName} [options]";
    if (unhandledProp != null) {
      var unhandledAttr = (UnhandledArgumentsAttribute)Attribute.GetCustomAttribute(unhandledProp, typeof(UnhandledArgumentsAttribute))!;
      var argName = unhandledAttr.Name;

      if (unhandledProp.PropertyType == typeof(List<string>)) {
        usageLine += $" \"{argName}\" [\"{argName}\"] [...]";
      } else {
        usageLine += $" \"{argName}\"";
      }
    }
    Console.Out.WriteLine(usageLine);

    // ===== UNHANDLED ARGUMENTS SECTION =====
    if (unhandledProp != null) {
      var unhandledAttr = (UnhandledArgumentsAttribute)Attribute.GetCustomAttribute(unhandledProp, typeof(UnhandledArgumentsAttribute))!;
      Console.Out.WriteLine();

      var argName = unhandledAttr.Name.PadRight(colWidth - 2);
      var description = unhandledAttr.Description ?? string.Empty;

      // Add required/optional indicator
      if (unhandledAttr.Required) {
        description += " (required)";
      } else {
        description += " (optional)";
      }

      // Format and wrap description
      var formattedDesc = WrapText(description, consoleWidth - colWidth, colWidth);
      Console.Out.WriteLine($"  {argName}{formattedDesc}");
    }

    // ===== OPTIONS SECTION =====
    if (namedParams.Count > 0) {
      Console.Out.WriteLine();
      Console.Out.WriteLine("Options:");
      Console.Out.WriteLine();

      int? lastOrderGroup = null;

      foreach (var param in namedParams) {
        var attr = param.Attribute;
        var propType = param.Property.PropertyType;

        // Add blank line when crossing multiple of 100
        var currentOrderGroup = attr.Order / 100;
        if (lastOrderGroup.HasValue && currentOrderGroup != lastOrderGroup.Value) {
          Console.Out.WriteLine();
        }
        lastOrderGroup = currentOrderGroup;

        // Format option names
        var optionNames = FormatOptionNames(attr.NamedParameters, propType);

        // Build description
        var description = attr.Description ?? string.Empty;

        // Add default value indicator
        if (attr.DefaultIfMissing != null) {
          var defaultValueStr = attr.DefaultIfMissing switch {
            bool b => b.ToString().ToLowerInvariant(),
            _ => attr.DefaultIfMissing.ToString()
          };
          description += $" (default:{defaultValueStr})";
        } else if (propType == typeof(string[])) {
          description += " (repeatable)";
        } else if (attr.ValueIsOptional || propType == typeof(bool)) {
          description += " (optional)";
        } else {
          description += " (required)";
        }

        // Format and wrap description
        var wrappedDesc = WrapText(description, consoleWidth - colWidth, colWidth);

        // Handle long option names that exceed column width
        if (optionNames.Length >= colWidth - 2) {
          Console.Out.WriteLine($"  {optionNames}");
          Console.Out.WriteLine($"  {new string(' ', colWidth - 2)}{wrappedDesc}");
        } else {
          var paddedOptionNames = optionNames.PadRight(colWidth - 2);
          Console.Out.WriteLine($"  {paddedOptionNames}{wrappedDesc}");
        }

        // Show allowed values on separate line if present
        if (attr.AllowedValues != null && attr.AllowedValues.Length > 0) {
          var allowedText = FormatAllowedValues(attr.AllowedValues);
          var wrappedAllowed = WrapText(allowedText, consoleWidth - colWidth, colWidth);
          Console.Out.WriteLine($"  {new string(' ', colWidth - 2)}{wrappedAllowed}");
        } else if (propType.IsEnum) {
          var allowedText = FormatAllowedValues(Enum.GetNames(propType).Select(x => x.ToLowerInvariant()).ToArray());
          var wrappedAllowed = WrapText(allowedText, consoleWidth - colWidth, colWidth);
          Console.Out.WriteLine($"  {new string(' ', colWidth - 2)}{wrappedAllowed}");
        }
      }
    }

    // ===== FOOTER NOTES =====
    Console.Out.WriteLine();

    // Show prefix note
    Console.Out.WriteLine("Notes:");
    Console.Out.WriteLine();
    Console.Out.WriteLine("- Option prefixes can be -, --, or / (e.g., -help, --help, or /help).");
    Console.Out.WriteLine("- Options cannot be chained (e.g., -abc is not allowed; use -a -b -c).");

    // Show ! override note only if there are boolean options with envars
    if (hasBoolEnvarOptions) {
      var line = WrapText("- Use '!' to set any boolean option to opposite value. This overrides environment variables.", consoleWidth - 2, 2);
      Console.Out.WriteLine($"{line}");
      line = WrapText("- For example, use '-!verbose' to disable verbose mode (useful to override envars).", consoleWidth - 2, 2);
      Console.Out.WriteLine($"{line}");
    }

    // Show environment variables section only if there are options with envars
    if (hasEnvarOptions) {
      ShowCurrentEnvars(target, showHeader: false);
    }
  }

  /// <summary>
  /// Formats option names with appropriate value type indicators.
  /// </summary>
  private static string FormatOptionNames( string[] names, Type propertyType )
  {
    var formattedNames = string.Join(" -", names.Select(n => n));
    formattedNames = "-" + formattedNames;

    // Add value type indicator based on property type
    if (propertyType == typeof(bool)) {
      // Boolean flags don't need a value indicator
      return formattedNames;
    } else if (propertyType == typeof(string)) {
      return $"{formattedNames} [string]";
    } else if (propertyType == typeof(int) || propertyType == typeof(long) ||
               propertyType == typeof(short) || propertyType == typeof(uint) ||
               propertyType == typeof(ulong) || propertyType == typeof(ushort)) {
      return $"{formattedNames} [number]";
    } else if (propertyType == typeof(float) || propertyType == typeof(double) ||
               propertyType == typeof(decimal)) {
      return $"{formattedNames} [decimal]";
    } else if (propertyType == typeof(DateTime)) {
      return $"{formattedNames} [date]";
    } else if (propertyType == typeof(TimeSpan)) {
      return $"{formattedNames} [time]";
    } else if (propertyType == typeof(string[])) {
      return $"{formattedNames} [string[]]";
    } else if (propertyType.IsEnum) {
      return $"{formattedNames} [enum]";
    } else {
      return $"{formattedNames} [value]";
    }
  }

  /// <summary>
  /// Formats allowed values with commas and "or" before the last value.
  /// </summary>
  private static string FormatAllowedValues( string[] allowedValues )
  {
    if (allowedValues.Length == 0) {
      return string.Empty;
    }

    if (allowedValues.Length == 1) {
      return $"Allowed values: {allowedValues[0]}";
    }

    if (allowedValues.Length == 2) {
      return $"Allowed values: {allowedValues[0]} or {allowedValues[1]}";
    }

    var values = string.Join(", ", allowedValues.Take(allowedValues.Length - 1));
    return $"Allowed values: {values}, or {allowedValues[^1]}";
  }

  /// <summary>
  /// Wraps text to fit within the specified width, indenting continuation lines.
  /// </summary>
  private static string WrapText( string text, int maxWidth, int indent )
  {
    if (string.IsNullOrWhiteSpace(text)) {
      return string.Empty;
    }

    // Handle newlines in the text
    var lines = text.Split('\n');
    var result = new List<string>();

    foreach (var line in lines) {
      var trimmedLine = line.Trim();
      if (string.IsNullOrEmpty(trimmedLine)) {
        result.Add(string.Empty);
        continue;
      }

      var words = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      var currentLine = new List<string>();
      var currentLength = 0;

      foreach (var word in words) {
        var wordLength = word.Length;
        var spaceLength = currentLine.Count > 0 ? 1 : 0;

        if (currentLength + spaceLength + wordLength > maxWidth && currentLine.Count > 0) {
          // Need to wrap
          result.Add(string.Join(" ", currentLine));
          currentLine.Clear();
          currentLength = 0;
        }

        currentLine.Add(word);
        currentLength += wordLength + spaceLength;
      }

      if (currentLine.Count > 0) {
        result.Add(string.Join(" ", currentLine));
      }
    }

    // Format result with proper indentation
    if (result.Count == 0) {
      return string.Empty;
    }

    var indentStr = new string(' ', indent);
    var output = result[0];

    for (var i = 1; i < result.Count; i++) {
      if (string.IsNullOrEmpty(result[i])) {
        output += "\n" + indentStr;
      } else {
        output += "\n" + indentStr + result[i];
      }
    }

    return output;
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
  public void ShowCurrentEnvars( object target, bool showHeader = false )
  {
    ArgumentNullException.ThrowIfNull(target);
    var targetType = target.GetType();

    if (showHeader) {
      ShowHeader(false);
    }

    var consoleWidth = GetConsoleWidth();
    var minColWidth = 10;
    var maxColWidth = 40;

    Console.Out.WriteLine();
    Console.Out.WriteLine("Environment Variables: ");
    Console.Out.WriteLine();

    var line = WrapText("The following envars can be set in the system or user environment to configure the application without using command-line arguments.", consoleWidth, 0);
    Console.Out.WriteLine($"{line}");
    line = WrapText("If both an environment variable and a command-line argument are provided for the same option, the command-line argument takes precedence.", consoleWidth, 0);
    Console.Out.WriteLine($"{line}");

    Console.Out.WriteLine();

    // Find all properties with CliArgument attribute where AllowEnvar == true
    var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    var propertiesWithEnvar = targetType.GetProperties(bindingFlags)
      .Where(p =>
      {
        var attr = (CliArgumentAttribute?)Attribute.GetCustomAttribute(p, typeof(CliArgumentAttribute));
        return attr != null && attr.AllowEnvar;
      })
      .ToList();

    // Calculate optimal column width based on longest environment variable name
    var maxEnvarNameLength = propertiesWithEnvar
      .Select(p =>
      {
        var attr = (CliArgumentAttribute?)Attribute.GetCustomAttribute(p, typeof(CliArgumentAttribute));
        if (attr != null && attr.NamedParameters.Length > 0) {
          var envarName = attr.NamedParameters[0];
          if (AppEnvarPrefix is not null) {
            envarName = $"{AppEnvarPrefix}{envarName}";
          }
          return envarName.Length + 1; // +1 for a bit of padding
        }
        return 0;
      })
      .DefaultIfEmpty(0)
      .Max();
    var colWidth = Math.Clamp(maxEnvarNameLength, minColWidth, maxColWidth);

    if (propertiesWithEnvar.Count == 0) {
      Console.Out.WriteLine("  <none found>");
    } else {
      foreach (var prop in propertiesWithEnvar) {
        var attr = (CliArgumentAttribute?)Attribute.GetCustomAttribute(prop, typeof(CliArgumentAttribute));
        if (attr != null && attr.NamedParameters.Length > 0) {
          // Use the first parameter name + AppEnvarPrefix as the environment variable name
          var envarName = attr.NamedParameters[0];
          if (AppEnvarPrefix is not null) {
            envarName = $"{AppEnvarPrefix}{envarName}";
          }
          var envValue = Environment.GetEnvironmentVariable(envarName);

          if (string.IsNullOrEmpty(envValue)) {
            envValue = "<notset>";
          }

          line = WrapText(envValue, consoleWidth - colWidth, colWidth);
          var pad = new string(' ', Math.Max(0, colWidth - envarName.Length));
          Console.Out.WriteLine($"   {envarName}{pad}= {line}");
        }
      }
    }

    Console.Out.WriteLine();
  }

  private static int GetConsoleWidth()
  {
    try {
      if (Console.IsOutputRedirected) {
        return DefaultConsoleWidth;
      }

      return Math.Max(DefaultConsoleWidth, Console.WindowWidth);
    } catch (IOException) {
      return DefaultConsoleWidth;
    } catch (InvalidOperationException) {
      return DefaultConsoleWidth;
    }
  }
}

/// <summary>
/// Specifies command-line options that can be bound to a property. Supports both flag-style
/// (e.g., --verbose, -v) and command-style (e.g., verbose) argument parsing.
/// </summary>
/// <example>
/// // Flag-style only (requires -, --, or /)
/// [CliArgument(namedParameters: ["verbose", "v"])]
/// public bool Verbose { get; set; }
///
/// // Command-style only (no prefix required)
/// [CliArgument(namedCommands: ["init", "create"])]
/// public string Command { get; set; }
///
/// // Both styles supported
/// [CliArgument(namedParameters: ["encoding", "enc"], namedCommands: ["encoding"])]
/// public string Encoding { get; set; }
/// </example>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public class CliArgumentAttribute : Attribute
{
  public const int DefaultPropertyOrder = 5000;
  public const int DefaultGlobalOrder = 10000;

  /// <summary>
  /// Names that require a flag prefix (-, --, or /) to be recognized.
  /// Example: ["verbose", "v"] allows -verbose, --verbose, -v, --v, /verbose, /v
  /// </summary>
  public string[] NamedParameters { get; } = [];

  /// <summary>
  /// Names that can be used without a flag prefix.
  /// Example: ["init", "create"] allows: app init or app create
  /// </summary>
  public string[] NamedCommands { get; } = [];

  /// <summary>
  /// Indicates whether the value for this option can also be set via an environment variable.
  /// The environment variable name is derived from the first name in NamedParameters,
  /// prefixed with the application's environment variable prefix (if any).
  /// </summary>
  public bool AllowEnvar { get; }

  /// <summary>
  /// Indicates whether the value for this option is optional. If true, the option can be used
  /// as a flag without a value (e.g. --verbose), in which case the property will be set to an
  /// empty string to indicate that the flag was present but no value was provided.
  /// </summary>
  public bool ValueIsOptional { get; }

  /// <summary>
  /// Description for this option, used in help text and error messages.
  /// </summary>
  public string? Description { get; }

  /// <summary>
  /// Optional array of allowed values for this option. If specified, the value provided
  /// must be one of these allowed values (case-insensitive comparison).
  /// </summary>
  public string[]? AllowedValues { get; }

  /// <summary>
  /// Default value for this option. Applied before environment variables and command-line arguments.
  /// </summary>
  public object? DefaultIfMissing { get; } = null;

  /// <summary>
  /// Value applied when an optional-valued argument is present without a value.
  /// </summary>
  public object? DefaultIfNoValue { get; } = null;

  /// <summary>
  /// Display order in usage output. Options with lower Order values are displayed first.
  /// If two options have the same Order value, they are sorted alphabetically.
  /// </summary>
  public int Order { get; } = DefaultPropertyOrder;

  // Full constructor with all parameters
  public CliArgumentAttribute(
    string? namedParameter = null,
    string[]? namedParameters = null,
    string? namedCommand = null,
    string[]? namedCommands = null,
    bool allowEnvar = false,
    bool valueIsOptional = false,
    string? description = null,
    string[]? allowedValues = null,
    int order = DefaultPropertyOrder,
    object? defaultIfMissing = null,
    object? defaultIfNoValue = null )
  {
    NamedParameters = namedParameters is not null && namedParameters.Length > 0
      ? namedParameters
      : !string.IsNullOrEmpty(namedParameter)
        ? [namedParameter]
        : [];
    NamedCommands = namedCommands is not null && namedCommands.Length > 0
      ? namedCommands
      : !string.IsNullOrEmpty(namedCommand)
        ? [namedCommand]
        : [];
    AllowEnvar = allowEnvar;
    ValueIsOptional = valueIsOptional;
    Description = description;
    AllowedValues = allowedValues;
    Order = order;
    DefaultIfMissing = defaultIfMissing;
    DefaultIfNoValue = defaultIfNoValue;

    // Validation: at least one of NamedParameters or NamedCommands must be provided
    if (NamedParameters.Length == 0 && NamedCommands.Length == 0) {
      throw new ArgumentException("At least one of namedParameters or namedCommands must be specified.");
    }
  }
}

/// <summary>
/// Indicates that a property should be bound to positional (non-flag) arguments that don't match
/// any properties with the `NamedParameters` or `NamedCommands` attribute. This allows for a
/// "catch-all" property to receive positional arguments, which is useful for handling file paths,
/// patterns, or other free-form input.
/// </summary>
/// <example>
/// [PositionalArguments("file patterns")] public List&lt;string&gt; FilePatterns { get; set; }
/// Command line: myapp.exe -verbose *.txt file.dat Result: FilePatterns = ["*.txt", "file.dat"]
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class UnhandledArgumentsAttribute : Attribute
{
  public string Name { get; set; }
  public string Description { get; set; }
  public bool Required { get; set; }

  public UnhandledArgumentsAttribute( string name, string description, bool required = false )
  {
    Name = name;
    Description = description;
    Required = required;
  }
}

/// <summary>
/// Represents the result of parsing command-line arguments.
/// </summary>
public record ParseResult
{
  /// <summary>
  /// Indicates whether the application should exit immediately after parsing (e.g., help or version was requested).
  /// </summary>
  public bool ShouldExit { get; init; }

  /// <summary>
  /// The exit code to return if the application should exit.
  /// </summary>
  public int ExitCode { get; init; }
}
