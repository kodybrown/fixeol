namespace Bricksoft.PowerCode;

using System;
using System.Collections.Generic;

/// <summary>
/// Provides a simple way to get the command-line arguments.
/// <remarks>See CommandLineArguments.cs.txt for details on how to use this class.</remarks>
/// </summary>
internal static class EnvVars
{
  /// <summary>
  /// Returns whether the environment variable specified exists.
  /// The target (scope) is the current process.
  /// </summary>
  /// <param name="variable"></param>
  /// <returns></returns>
  public static bool Exists( string variable )
  {
    return Exists(variable, EnvironmentVariableTarget.Process);
  }

  /// <summary>
  /// Returns whether the environment variable specified exists.
  /// </summary>
  /// <param name="variable"></param>
  /// <param name="target"></param>
  /// <returns></returns>
  public static bool Exists( string variable, EnvironmentVariableTarget target )
  {
    return Environment.GetEnvironmentVariable(variable, target) != null;
  }

  /// <summary>
  /// 
  /// </summary>
  /// <param name="variables"></param>
  /// <returns></returns>
  public static bool Contains( params string[] variables )
  {
    return IndexOf(EnvironmentVariableTarget.Process, variables) > -1;
  }

  /// <summary>
  /// 
  /// </summary>
  /// <param name="target"></param>
  /// <param name="variables"></param>
  /// <returns></returns>
  public static bool Contains( EnvironmentVariableTarget target, params string[] variables )
  {
    return IndexOf(target, variables) > -1;
  }

  /// <summary>
  /// 
  /// </summary>
  /// <param name="variables"></param>
  /// <returns></returns>
  public static int IndexOf( params string[] variables )
  {
    return IndexOf(EnvironmentVariableTarget.Process, variables);
  }

  /// <summary>
  /// 
  /// </summary>
  /// <param name="target"></param>
  /// <param name="variables"></param>
  /// <returns></returns>
  public static int IndexOf( EnvironmentVariableTarget target, params string[] variables )
  {
    for (var i = 0; i < variables.Length; i++) {
      if (Exists(variables[i], target)) {
        return i;
      }
    }

    return -1;
  }

  /// <summary>
  /// Retrieves the value of an environment variable.
  /// If it does not exist, an empty string is returned.
  /// The target (scope) is the current process.
  /// </summary>
  /// <param name="variable"></param>
  /// <returns></returns>
  public static string GetString( string variable )
  {
    return GetString(string.Empty, variable);
  }

  /// <summary>
  /// Retrieves the value of an environment variable.
  /// If it does not exist or is empty, <paramref name="defaultValue"/> is returned.
  /// The target (scope) is the current process.
  /// </summary>
  /// <param name="defaultValue"></param>
  /// <param name="variable"></param>
  /// <returns></returns>
  public static string GetString( string defaultValue, string variable )
  {
    return GetString(defaultValue, EnvironmentVariableTarget.Process, variable);
  }

  /// <summary>
  /// Retrieves the value of an environment variable.
  /// If it does not exist or is empty, <paramref name="defaultValue"/> is returned.
  /// </summary>
  /// <param name="defaultValue"></param>
  /// <param name="variable"></param>
  /// <param name="target"></param>
  /// <returns></returns>
  public static string GetString( string defaultValue, EnvironmentVariableTarget target, string variable )
  {
    if (variable == null || variable.Length == 0) {
      throw new ArgumentNullException(nameof(variable));
    }

    var result = Environment.GetEnvironmentVariable(variable, target);
    return result != null && result.Length > 0
      ? result
      : defaultValue;
  }

  /// <summary>
  /// Returns a collection of values.
  /// </summary>
  /// <param name="defaultValues"></param>
  /// <param name="variable"></param>
  /// <param name="separator"></param>
  /// <returns></returns>
  public static List<string> GetStringList( List<string> defaultValues, string variable, string separator )
  {
    return GetStringList(defaultValues, EnvironmentVariableTarget.Process, variable, separator);
  }

  /// <summary>
  /// Returns a collection of values.
  /// </summary>
  /// <param name="defaultValues"></param>
  /// <param name="target"></param>
  /// <param name="variable"></param>
  /// <param name="separator"></param>
  /// <returns></returns>
  public static List<string> GetStringList( List<string> defaultValues, EnvironmentVariableTarget target, string variable, string separator )
  {
    string result;

    if (separator == null || separator.Length == 0) {
      throw new ArgumentNullException(nameof(separator));
    }
    if (variable == null || variable.Length == 0) {
      throw new ArgumentNullException(nameof(variable));
    }

    result = GetString(string.Empty, target, variable);

    return result.Length == 0
      ? defaultValues
      : new List<string>(result.Split(separator, StringSplitOptions.RemoveEmptyEntries));
  }

  /// <summary>
  /// Retrieves the value of an environment variable.
  /// If it does not exist or is empty, false is returned.
  /// The target (scope) is the current process.
  /// </summary>
  /// <param name="variable"></param>
  /// <returns></returns>
  public static bool GetBoolean( string variable )
  {
    return GetBoolean(false, variable);
  }

  /// <summary>
  /// Retrieves the value of an environment variable.
  /// If it does not exist or is empty, <paramref name="defaultValue"/> is returned.
  /// The target (scope) is the current process.
  /// </summary>
  /// <param name="defaultValue"></param>
  /// <param name="variable"></param>
  /// <returns></returns>
  public static bool GetBoolean( bool defaultValue, string variable )
  {
    return GetBoolean(defaultValue, EnvironmentVariableTarget.Process, variable);
  }

  /// <summary>
  /// Retrieves the value of an environment variable.
  /// If it does not exist or is empty, <paramref name="defaultValue"/> is returned.
  /// </summary>
  /// <param name="defaultValue"></param>
  /// <param name="variable"></param>
  /// <param name="target"></param>
  /// <returns></returns>
  public static bool GetBoolean( bool defaultValue, EnvironmentVariableTarget target, string variable )
  {
    var temp = GetString(defaultValue.ToString().ToLower(), target, variable);
    if (string.IsNullOrEmpty(temp)) {
      return defaultValue;
    }

    return bool.TryParse(temp, out var result)
        ? result
        : temp.ToLower() is "true" or "t" or "yes" or "y" or "1";
  }

  ///// <summary>
  ///// Retrieves the value of an environment variable.
  ///// If it does not exist or is empty, false is returned.
  ///// The target (scope) is the current process.
  ///// </summary>
  ///// <param name="variable"></param>
  ///// <returns></returns>
  //public static int GetInt32( string variable ) {
  //  return GetInt32(0, variable);
  //}

  /// <summary>
  /// Retrieves the value of an environment variable.
  /// If it does not exist or is empty, <paramref name="defaultValue"/> is returned.
  /// The target (scope) is the current process.
  /// </summary>
  /// <param name="defaultValue"></param>
  /// <param name="variable"></param>
  /// <returns></returns>
  public static int GetInt32( int defaultValue, string variable )
  {
    return GetInt32(defaultValue, EnvironmentVariableTarget.Process, variable);
  }

  /// <summary>
  /// Retrieves the value of an environment variable.
  /// If it does not exist or is empty, <paramref name="defaultValue"/> is returned.
  /// </summary>
  /// <param name="defaultValue"></param>
  /// <param name="variable"></param>
  /// <param name="target"></param>
  /// <returns></returns>
  public static int GetInt32( int defaultValue, EnvironmentVariableTarget target, string variable )
  {
    var temp = GetString(defaultValue.ToString().ToLower(), target, variable);
    if (temp != null && temp.Length > 0) {
      if (int.TryParse(temp, out var result)) {
        return result;
      }
    }

    return defaultValue;
  }
}
