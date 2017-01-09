//
// Copyright (C) 2003-2016 Kody Brown (kody@bricksoft.com).
//
// MIT License:
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;

namespace Bricksoft.PowerCode
{
	/// <summary>
	/// Provides a simple way to get the command-line arguments.
	/// <remarks>See CommandLineArguments.cs.txt for details on how to use this class.</remarks>
	/// </summary>
	internal static class EnvironmentVariables
	{
		/// <summary>
		/// Returns whether the environment variable specified exists.
		/// The target (scope) is the current process.
		/// </summary>
		/// <param name="variable"></param>
		/// <returns></returns>
		public static bool Exists( string variable ) { return Exists(variable, EnvironmentVariableTarget.Process); }

		/// <summary>
		/// Returns whether the environment variable specified exists.
		/// </summary>
		/// <param name="variable"></param>
		/// <param name="target"></param>
		/// <returns></returns>
		public static bool Exists( string variable, EnvironmentVariableTarget target ) { return Environment.GetEnvironmentVariable(variable, target) != null; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="variables"></param>
		/// <returns></returns>
		public static bool Contains( params string[] variables ) { return IndexOf(EnvironmentVariableTarget.Process, variables) > -1; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="target"></param>
		/// <param name="variables"></param>
		/// <returns></returns>
		public static bool Contains( EnvironmentVariableTarget target, params string[] variables ) { return IndexOf(target, variables) > -1; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="variables"></param>
		/// <returns></returns>
		public static int IndexOf( params string[] variables ) { return IndexOf(EnvironmentVariableTarget.Process, variables); }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="target"></param>
		/// <param name="variables"></param>
		/// <returns></returns>
		public static int IndexOf( EnvironmentVariableTarget target, params string[] variables )
		{
			for (int i = 0; i < variables.Length; i++) {
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
		public static string GetString( string variable ) { return GetString(string.Empty, variable); }

		/// <summary>
		/// Retrieves the value of an environment variable.
		/// If it does not exist or is empty, <paramref name="defaultValue"/> is returned.
		/// The target (scope) is the current process.
		/// </summary>
		/// <param name="defaultValue"></param>
		/// <param name="variable"></param>
		/// <returns></returns>
		public static string GetString( string defaultValue, string variable ) { return GetString(defaultValue, EnvironmentVariableTarget.Process, variable); }

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
			string result;

			if (variable == null || variable.Length == 0) {
				throw new ArgumentNullException("variable");
			}

			result = Environment.GetEnvironmentVariable(variable, target);
			if (result != null && result.Length > 0) {
				return result;
			}

			return defaultValue;
		}


		/// <summary>
		/// Returns a collection of values.
		/// </summary>
		/// <param name="defaultValues"></param>
		/// <param name="variable"></param>
		/// <param name="separator"></param>
		/// <returns></returns>
		public static List<String> GetStringList( List<String> defaultValues, string variable, String separator ) { return GetStringList(defaultValues, EnvironmentVariableTarget.Process, variable, separator); }

		/// <summary>
		/// Returns a collection of values.
		/// </summary>
		/// <param name="defaultValues"></param>
		/// <param name="target"></param>
		/// <param name="variable"></param>
		/// <param name="separator"></param>
		/// <returns></returns>
		public static List<String> GetStringList( List<String> defaultValues, EnvironmentVariableTarget target, string variable, String separator )
		{
			String result;

			if (separator == null || separator.Length == 0) {
				throw new ArgumentNullException("separator");
			}
			if (variable == null || variable.Length == 0) {
				throw new ArgumentNullException("variable");
			}

			result = GetString(String.Empty, target, variable);

			if (result.Length == 0) {
				return defaultValues;
			} else {
				return new List<string>(result.Split(separator, SplitOptions.RemoveEmptyEntries));
			}
		}


		/// <summary>
		/// Retrieves the value of an environment variable.
		/// If it does not exist or is empty, false is returned.
		/// The target (scope) is the current process.
		/// </summary>
		/// <param name="variable"></param>
		/// <returns></returns>
		public static bool GetBoolean( string variable ) { return GetBoolean(false, variable); }

		/// <summary>
		/// Retrieves the value of an environment variable.
		/// If it does not exist or is empty, <paramref name="defaultValue"/> is returned.
		/// The target (scope) is the current process.
		/// </summary>
		/// <param name="defaultValue"></param>
		/// <param name="variable"></param>
		/// <returns></returns>
		public static bool GetBoolean( bool defaultValue, string variable ) { return GetBoolean(defaultValue, EnvironmentVariableTarget.Process, variable); }

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
			string temp;
			bool result;

			temp = GetString(defaultValue.ToString().ToLower(), target, variable);
			if (temp != null && temp.Length > 0) {
				if (bool.TryParse(temp, out result)) {
					return result;
				} else if (temp.StartsWith("t", StringComparison.InvariantCultureIgnoreCase)
						|| temp.StartsWith("y", StringComparison.InvariantCultureIgnoreCase)
						|| temp.Equals("1")) {
					return true;
				} else {
					return false;
				}
			}

			return defaultValue;
		}


		///// <summary>
		///// Retrieves the value of an environment variable.
		///// If it does not exist or is empty, false is returned.
		///// The target (scope) is the current process.
		///// </summary>
		///// <param name="variable"></param>
		///// <returns></returns>
		//public static int GetInt32( string variable ) { return GetInt32(0, variable); }

		/// <summary>
		/// Retrieves the value of an environment variable.
		/// If it does not exist or is empty, <paramref name="defaultValue"/> is returned.
		/// The target (scope) is the current process.
		/// </summary>
		/// <param name="defaultValue"></param>
		/// <param name="variable"></param>
		/// <returns></returns>
		public static int GetInt32( int defaultValue, string variable ) { return GetInt32(defaultValue, EnvironmentVariableTarget.Process, variable); }

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
			string temp;
			int result;

			temp = GetString(defaultValue.ToString().ToLower(), target, variable);
			if (temp != null && temp.Length > 0) {
				if (int.TryParse(temp, out result)) {
					return result;
				}
			}

			return defaultValue;
		}
	}
}
