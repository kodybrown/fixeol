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
using System.Collections;
using System.Collections.Generic;
using System.IO;

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
		public bool verbose { get; set; }

		/// <summary>
		/// Gets or sets whether to pause when finished.
		/// </summary>
		public bool pause { get; set; }

		public bool recurse { get; set; }
		protected SearchOption recurseOption = SearchOption.TopDirectoryOnly;

		public bool backup { get; set; }

		/// <summary>
		/// Gets or sets whether to correct line endings and also specifies what ending to use.
		/// Set to 'rn' for Windows format (\r\n). Set to 'n' for linux format (\n).
		/// </summary>
		public string eol { get; set; }

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
				pause = false;
			} else if (args.Contains("p", "pause")) {
				pause = args.GetBoolean(true, "p", "pause");
			} else if (EnvironmentVariables.Exists(envPrefix + "pause")) {
				pause = EnvironmentVariables.GetBoolean(true, envPrefix + "pause");
			} else {
				pause = false;
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
				verbose = false;
			} else if (args.Contains("v", "verbose")) {
				verbose = args.GetBoolean(true, "v", "verbose");
			} else if (EnvironmentVariables.Exists(envPrefix + "verbose")) {
				verbose = EnvironmentVariables.GetBoolean(true, envPrefix + "verbose");
			} else {
				verbose = false;
			}

			// Backup the original file.
			// Defaults to FALSE if not specified.
			if (args.Contains("!b", "!backup")) {
				backup = false;
			} else if (args.Contains("b", "backup")) {
				backup = args.GetBoolean(true, "b", "backup");
			} else if (EnvironmentVariables.Exists(envPrefix + "backup")) {
				backup = EnvironmentVariables.GetBoolean(true, envPrefix + "backup");
			} else {
				backup = false;
			}

			// Recurse sub-directories.
			// Defaults to FALSE if not specified.
			if (args.Contains("!r", "!recurse", "!s", "!subdir", "!subdirs")) {
				recurse = false;
			} else if (args.Contains("r", "recurse", "s", "subdir", "subdirs")) {
				recurse = args.GetBoolean(true, "r", "recurse", "s", "subdir", "subdirs");
			} else if (EnvironmentVariables.Exists(envPrefix + "recurse")) {
				recurse = EnvironmentVariables.GetBoolean(true, envPrefix + "recurse");
			} else {
				recurse = false;
			}
			recurseOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

			// Specify line ending to use.
			// Default to format of the underlying operating system if not specified.
			if (args.Contains("!eol")) {
				eol = string.Empty;
			} else if (args.Contains("eol")) {
				eol = args.GetString(Environment.NewLine, "eol");
			} else if (EnvironmentVariables.Exists(envPrefix + "eol")) {
				eol = EnvironmentVariables.GetString("rn", envPrefix + "eol");
			} else {
				eol = string.Empty;
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

			if (eol.Length > 0) {
				NewLine = eol.Replace("\\r", "\r")
							 .Replace("\\n", "\n")
							 .Replace("\\t", "\t")
							 //.Replace("r", "\r")
							 //.Replace("n", "\n")
							 //.Replace("t", "\t")
							 .Replace("\\\\", "\\");
			} else {
				//NewLine = w.NewLine;
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
					//if (recurse) {
					//	files.AddRange(Directory.GetFiles(".", f, recurseOption));
					//} else {
					//	files.Add(f);
					//}
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
				if (verbose) {
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

								if (verbose) {
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
					if (backup) {
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

				if (verbose) {
					Console.CursorLeft = message.Length;
					Console.WriteLine("100.00%  ");
				}
			}

			if (verbose) {
				Console.Out.WriteLine();
			}

			ShowPause();

			return 0;
		}

		private void ShowPause()
		{
			if (pause) {
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
				Console.Out.WriteLine("type '{0}.exe /?' for help", prodName);
				return;
			}

			Console.Out.WriteLine("{0}.exe - fixes eol for the specified file(s).", prodName);
			Console.Out.WriteLine("Copyright (C) 2003-2016 Kody Brown.");
			Console.Out.WriteLine("No warranties expressed or implied. Use at your own risk.");
			Console.Out.WriteLine();

			Console.Out.WriteLine("Usage: ");
			Console.Out.WriteLine();
			Console.Out.WriteLine("  {0}.exe [options] [commands] [-file] \"filename\" ", envName);
			Console.Out.WriteLine();

			Console.Out.WriteLine("     file      the full filename of the file to manipulate.");
			Console.Out.WriteLine("               file or {0}file (below) is required.", envPrefix);
			//Console.Out.WriteLine("     -f|-find  the text to find in each file");
			//Console.Out.WriteLine("               find or {0}find (below) is required", envPrefix);
			Console.Out.WriteLine();
			Console.Out.WriteLine("  commands: ");
			Console.Out.WriteLine();
			Console.Out.WriteLine("     /eol=[\\r\\n|\\n]        override the default eol settings of the operating system.");
			Console.Out.WriteLine();
			Console.Out.WriteLine("  options: ");
			Console.Out.WriteLine();
			Console.Out.WriteLine("     /v /verbose        output additional details (default:false)");
			Console.Out.WriteLine("     /b /backup         backup (default:false)");
			Console.Out.WriteLine("     /r /recurse        apply the file pattern(s) to the current");
			Console.Out.WriteLine("                        and all sub-directories (default:false)");
			Console.Out.WriteLine("     /p /pause          pause when finished (default:false)");
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
			int count;

			count = 0;
			Console.Out.WriteLine();
			Console.Out.WriteLine("{0}.exe - {1}", envName, prodName);
			Console.Out.WriteLine();
			Console.Out.WriteLine("environment variables: ");

			foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables()) {
				if (entry.Key.ToString().StartsWith(envPrefix)) {
					Console.Out.WriteLine("  {0} = {1}", entry.Key, entry.Value);
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
