//
// Copyright (C) 2003-2017 Kody Brown (kody@bricksoft.com).
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Bricksoft.PowerCode;

namespace Bricksoft.DosToys
{
	public class fixeol
	{
		/// <summary>
		/// Main entry point.
		/// </summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		static public int Main( string[] arguments )
		{
			fixeol f;

			f = new fixeol(arguments);
			return f.Run();
		}

		private CommandLine args;
		private string prodName = "fixeol";
		private string envName = "fixeol";
		private string envPrefix = "fixeol_";

		/// <summary>
		/// Gets or sets whether to output details of every line found.
		/// </summary>
		public bool OptVerbose { get; set; }

		/// <summary>
		/// Gets or sets whether to pause when finished.
		/// </summary>
		public bool OptPause { get; set; }

		/// <summary>
		/// Gets or sets whether to scan sub-directories.
		/// </summary>
		public bool OptRecurse {
			get { return recurseOption == SearchOption.AllDirectories; }
			set {
				if (value) {
					recurseOption = SearchOption.AllDirectories;
				} else {
					recurseOption = SearchOption.TopDirectoryOnly;
				}
			}
		}
		protected SearchOption recurseOption = SearchOption.TopDirectoryOnly;

		/// <summary>
		/// Gets or sets whether to backup the (changed) files.
		/// </summary>
		public bool OptBackup { get; set; }

		/// <summary>
		/// Gets or sets whether to correct line endings and also specifies what ending to use.
		/// Set to 'crlf' for Windows format (\r\n). Set to 'lf' for linux format (\n).
		/// </summary>
		public string OptEOL { get; set; }

		/// <summary>
		/// Creates an instance of the class.
		/// </summary>
		/// <param name="arguments"></param>
		public fixeol( string[] arguments )
		{
			args = new CommandLine(arguments);
		}

		/// <summary>
		/// Performs the text manipulation(s) specified.
		/// </summary>
		/// <returns></returns>
		public int Run()
		{
			// Pause when finished.
			// Defaults to FALSE if not specified.
			if (args.Contains("!p", "!pause")) {
				OptPause = false;
			} else if (args.Contains("p", "pause")) {
				OptPause = args.GetBoolean(true, "p", "pause");
			} else if (EnvironmentVariables.Exists(envPrefix + "pause")) {
				OptPause = EnvironmentVariables.GetBoolean(true, envPrefix + "pause");
			} else {
				OptPause = false;
			}

			if (args.Contains("h", "?", "help")) {
				ShowUsage(true);
				ShowPause();
				return 0;
			}

			if (args.Contains("set")) {
				ShowSetVars();
				ShowPause();
				return 0;
			}

			//
			// txtr.exe [/set] [/verbose] [/pause] "filename|wildcard"
			//

			#region Read command-line arguments

			// Verbose output.
			// Defaults to FALSE if not specified.
			if (args.Contains("!v", "!verbose")) {
				OptVerbose = false;
			} else if (args.Contains("v", "verbose")) {
				OptVerbose = args.GetBoolean(true, "v", "verbose");
			} else if (EnvironmentVariables.Exists(envPrefix + "verbose")) {
				OptVerbose = EnvironmentVariables.GetBoolean(true, envPrefix + "verbose");
			} else {
				OptVerbose = false;
			}

			// Backup the original file.
			// Defaults to FALSE if not specified.
			if (args.Contains("!b", "!backup")) {
				OptBackup = false;
			} else if (args.Contains("b", "backup")) {
				OptBackup = args.GetBoolean(true, "b", "backup");
			} else if (EnvironmentVariables.Exists(envPrefix + "backup")) {
				OptBackup = EnvironmentVariables.GetBoolean(true, envPrefix + "backup");
			} else {
				OptBackup = false;
			}

			// Recurse sub-directories.
			// Defaults to FALSE if not specified.
			if (args.Contains("!r", "!recurse", "!s", "!subdir", "!subdirs")) {
				OptRecurse = false;
			} else if (args.Contains("r", "recurse", "s", "subdir", "subdirs")) {
				OptRecurse = args.GetBoolean(true, "r", "recurse", "s", "subdir", "subdirs");
			} else if (EnvironmentVariables.Exists(envPrefix + "recurse")) {
				OptRecurse = EnvironmentVariables.GetBoolean(true, envPrefix + "recurse");
			} else {
				OptRecurse = false;
			}
			recurseOption = OptRecurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

			// Specify line ending to use.
			// Default to format of the underlying operating system if not specified.
			if (args.Contains("!eol")) {
				OptEOL = string.Empty;
			} else if (args.Contains("eol")) {
				OptEOL = args.GetString(Environment.NewLine, "eol");
			} else if (EnvironmentVariables.Exists(envPrefix + "eol")) {
				OptEOL = EnvironmentVariables.GetString("rn", envPrefix + "eol");
			} else {
				OptEOL = string.Empty;
			}

			// I don't really care what is specified as the eol..
			// Allowing any characters makes it more a versatile utility.
			//// Validate the specified eol.
			//StringBuilder s = new StringBuilder();
			//foreach (char c in eol) {
			//	if (c == 'r' || c == 'n') {
			//		s.Append(c);
			//	}
			//}
			//eol = s.ToString();

			#endregion

			List<string> fileArgs;
			List<string> files;
			string line = "";
			string NewLine = "";
			string backupfile;
			FileInfo fileInfo;
			float curPos;
			float totalSize;
			string message;

			if (OptEOL.Length > 0) {
				NewLine = OptEOL.Replace("cr", "\r")
								.Replace("lf", "\n")
								.Replace("\\r", "\r")
								.Replace("\\n", "\n");
				foreach (char c in NewLine) {
					if (c == '\r' || c == '\n') {
						continue;
					} else {
						Console.Out.WriteLine($"**** Invalid eol character found in `{OptEOL}`");
						return 5;
					}
				}
			} else {
				NewLine = Environment.NewLine;
			}

			fileArgs = new List<string>();
			files = new List<string>();
			message = "Working: ";

			for (int i = 0; i < args.Count; i++) {
				if (args.IsUnnamedItem(i)) {
					fileArgs.Add(args.GetString(i));
				}
			}

			if (fileArgs.Count == 0) {
				Console.Out.WriteLine("**** Missing command-line argument: {0}", "file");
				ShowUsage(false);
				ShowPause();
				return 1;
			}

			foreach (string f in fileArgs) {
				if (f.IndexOfAny(new char[] { '*', '?' }) > -1) {
					if (f.IndexOf('\\') > -1) {
						files.AddRange(Directory.GetFiles(Path.GetDirectoryName(f), Path.GetFileName(f), recurseOption));
					} else {
						files.AddRange(Directory.GetFiles(".", f, recurseOption));
					}
				} else {
					files.AddRange(Directory.GetFiles(".", f, recurseOption));
				}
			}

			foreach (string filename in files) {
				if (!File.Exists(filename)) {
					Console.Out.WriteLine("**** The file was not found: {0}", filename);
					ShowUsage(false);
					ShowPause();
					return 1;
				}

				backupfile = filename + ".original";
				fileInfo = new FileInfo(filename);
				totalSize = fileInfo.Length;
				curPos = 0.01F;

				Console.CursorVisible = false;
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
					Console.CursorVisible = true;
				}

				try {
					using (StreamReader r = File.OpenText(backupfile)) {
						using (StreamWriter w = File.CreateText(filename)) {
							while (!r.EndOfStream) {
								line = r.ReadLine();

								//// replace the newline characters..
								//while (line.IndexOf("\\\\n", StringComparison.InvariantCulture) > -1) {
								//   line = line.Replace("\\\\n", "\\n");
								//}

								w.Write(line + NewLine);

								if (OptVerbose) {
									Console.CursorLeft = message.Length;
									curPos += line.Length + NewLine.Length;
									Console.Write("{0:0.00}%   ", Math.Max(0, Math.Min(100, (curPos * 100F) / totalSize)));
								}
							}

							// ensure every file ends with an empty line..
							//if (line.Trim().Length > 0) {
							//	w.Write(NewLine);
							//}

							w.Flush();
							w.Close();
						}

						r.Close();
					}
				} catch (Exception ex) {
					Console.WriteLine();
					Console.WriteLine("**** ERROR writing to file: " + filename + " \n" + ex.Message);
					return 101;
				} finally {
					Console.CursorVisible = true;
				}

				try {
					if (OptBackup) {
						// TODO: backup files should not be deleted..
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
					Console.CursorVisible = true;
				}

				if (OptVerbose) {
					Console.CursorLeft = message.Length;
					Console.WriteLine("100.00%  ");
				}
			}

			if (OptVerbose) {
				Console.Out.WriteLine();
			}

			ShowPause();

			return 0;
		}

		private void ShowPause()
		{
			if (OptPause) {
				Console.Out.Write("Press any key to continue: ");
				Console.ReadKey(true);
				Console.Out.WriteLine();
				Console.Out.WriteLine();
			}
		}

		private void ShowUsage() { ShowUsage(true); }

		private void ShowUsage( bool showDetails )
		{
			if (!showDetails) {
				Console.Out.WriteLine();
				Console.Out.WriteLine($"type '{prodName}.exe /?' for help");
				return;
			}

			Console.Out.WriteLine($"{prodName}.exe - fixes eol for the specified file(s).");
			Console.Out.WriteLine("Copyright (C) 2003-2017 Kody Brown.");
			Console.Out.WriteLine("No warranties expressed or implied. Use at your own risk.");
			Console.Out.WriteLine();

			Console.Out.WriteLine("Usage: ");
			Console.Out.WriteLine();
			Console.Out.WriteLine($"  {envName}.exe [options] [commands] \"filepattern\" [...] ");
			Console.Out.WriteLine();

			Console.Out.WriteLine("     filepattern        the file(s) (or pattern) to manipulate.");
			Console.Out.WriteLine("                        the filepattern is required.");
			Console.Out.WriteLine();
			Console.Out.WriteLine("  commands: ");
			Console.Out.WriteLine();
			Console.Out.WriteLine("     /eol [crlf|cr|lf]  override the default eol settings of the operating system.");
			Console.Out.WriteLine("                        also supports [\\r\\n|\\r|\\n] for backwards compatibility.");
			Console.Out.WriteLine();
			Console.Out.WriteLine("  options: ");
			Console.Out.WriteLine();
			Console.Out.WriteLine("     /v -verbose        output additional details (default:false)");
			Console.Out.WriteLine("     /b -backup         backup (default:false)");
			Console.Out.WriteLine("     /r -recurse        apply the file pattern(s) to the current");
			Console.Out.WriteLine("                        and all sub-directories (default:false)");
			Console.Out.WriteLine("     /p -pause          pause when finished (default:false)");
			Console.Out.WriteLine();
			Console.Out.WriteLine("     /set               displays the current environment variables");
			Console.Out.WriteLine("                        then exits. All other options are ignored.");
			Console.Out.WriteLine();
			Console.Out.WriteLine("     *use ! to set any option to opposite value. overrides environment variables.");
			Console.Out.WriteLine("      for example use /!v to not use verbose.");
			Console.Out.WriteLine();

			Console.Out.WriteLine("  environment variables:");
			Console.Out.WriteLine();
			Console.Out.WriteLine("     {0}file=filename           sets -file \"filename\"", envPrefix);
			Console.Out.WriteLine("     {0}verbose=true|false      sets /v or /!v", envPrefix);
			Console.Out.WriteLine("     {0}pause=true|false        sets /pause or /!pause", envPrefix);
			Console.Out.WriteLine();
			Console.Out.WriteLine("     *command-line arguments override environment variables");
			Console.Out.WriteLine();
		}

		private void ShowSetVars()
		{
			Console.Out.WriteLine();
			Console.Out.WriteLine($"{envName}.exe - {prodName}");
			Console.Out.WriteLine();
			Console.Out.WriteLine("environment variables: ");

			int count = 0;

			foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables()) {
				if (entry.Key.ToString().StartsWith(envPrefix)) {
					Console.Out.WriteLine($"  {entry.Key} = {entry.Value}");
					count++;
				}
			}

			if (count == 0) {
				Console.Out.WriteLine("  <none found>");
			}

			Console.Out.WriteLine();
		}
	}
}
