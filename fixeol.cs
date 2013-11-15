//	update the information contained herein.
//
///////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.IO;
using Bricksoft.PowerCode;
using System.Collections.Generic;

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
		private string prodName = "FixEOL";
		private string envName = "feol";
		private string envPrefix = "feol_";

		/// <summary>
		/// Gets or sets whether to output details of every line found.
		/// </summary>
		public bool verbose { get; set; }

		/// <summary>
		/// Gets or sets whether to pause when finished.
		/// </summary>
		public bool pause { get; set; }

		public bool recurse { get; set; }

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
			if (args.Contains("!r", "!recurse")) {
				recurse = false;
			} else if (args.Contains("r", "recurse")) {
				recurse = args.GetBoolean(true, "r", "recurse");
			} else if (EnvironmentVariables.Exists(envPrefix + "recurse")) {
				recurse = EnvironmentVariables.GetBoolean(true, envPrefix + "recurse");
			} else {
				recurse = false;
			}

			// Specify line ending to use.
			// Default to format of the underlying operating system if not specified.
			if (args.Contains("!eol")) {
				eol = string.Empty;
			} else if (args.Contains("eol")) {
				eol = args.GetString("rn", "eol");
			} else if (EnvironmentVariables.Exists(envPrefix + "eol")) {
				eol = EnvironmentVariables.GetString("rn", envPrefix + "eol");
			} else {
				eol = string.Empty;
			}

			if (eol.Length > 0 && !eol.Contains("r", StringComparison.InvariantCultureIgnoreCase)
					&& !eol.Contains("n", StringComparison.InvariantCultureIgnoreCase)) {
				Console.Out.WriteLine("invalid eol specified");
				ShowPause();
				return 1;
			}

			#endregion

			List<string> fileArgs;
			List<string> files;
			string line;
			string NewLine;
			FileInfo fileInfo;
			float curPos;
			float totalSize;
			string message;

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
						files.AddRange(Directory.GetFiles(Path.GetDirectoryName(f), f.Substring(f.LastIndexOf('\\') + 1), recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly));
					} else {
						files.AddRange(Directory.GetFiles(".", f, recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly));
					}
				} else {
					files.Add(f);
				}
			}

			foreach (string f in files) {
				if (!File.Exists(f)) {
					Console.Out.WriteLine("**** The file was not found: {0}", f);
					ShowUsage(false);
					ShowPause();
					return 1;
				}

				fileInfo = new FileInfo(f);
				totalSize = fileInfo.Length;
				curPos = 0.01F;

				Console.CursorVisible = false;
				if (verbose) {
					Console.WriteLine("Processing file: {0}", Path.GetFileName(f));
					Console.Write(message);
				}

				try {
					if (File.Exists(f + ".tmp")) {
						File.SetAttributes(f + ".tmp", FileAttributes.Normal);
						File.Delete(f + ".tmp");
					}

					using (StreamReader r = File.OpenText(f)) {
						using (StreamWriter w = File.CreateText(f + ".tmp")) {
							if (eol.Length > 0) {
								NewLine = eol.Replace("r", "\r").Replace("n", "\n");
							} else {
								NewLine = w.NewLine;
							}

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

							w.Flush();
							w.Close();
						}

						r.Close();
					}

					File.SetAttributes(f, FileAttributes.Normal);
					if (backup) {
						File.Move(f, f + ".bak");
					} else {
						File.Delete(f);
					}

					File.Move(f + ".tmp", f);

					if (verbose) {
						Console.CursorLeft = message.Length;
						Console.WriteLine("100.00%  ");
					}
				} catch (Exception ex) {
					Console.WriteLine();
					Console.WriteLine("****Error: " + ex.Message);
				} finally {
					Console.CursorVisible = true;
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
				Console.Out.WriteLine(string.Empty);
				Console.Out.WriteLine(string.Empty);
			}
		}

		private void ShowUsage() { ShowUsage(true); }

		private void ShowUsage( bool showDetails )
		{
			if (!showDetails) {
				Console.Out.WriteLine();
				Console.Out.WriteLine("type '{0}.exe /?' for help", envName);
				return;
			}

			Console.Out.WriteLine();
			Console.Out.WriteLine("{0}.exe - {1}", envName, prodName);
			Console.Out.WriteLine("Copyright (C) 2003-2012 Kody Brown.");
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
			Console.Out.WriteLine("     /eol[=rn|n]        override the default eol settings of the operating system.");
			Console.Out.WriteLine();
			Console.Out.WriteLine("  options: ");
			Console.Out.WriteLine();
			Console.Out.WriteLine("     /v /verbose        output additional details (default:false)");
			Console.Out.WriteLine("     /b /backup         backup (default:false)");
			Console.Out.WriteLine("     /p /pause          pause when finished (default:false)");
			Console.Out.WriteLine();
			Console.Out.WriteLine("     /set               displays the current environment variables");
			Console.Out.WriteLine("                        then exits. All other options are ignored.");
			Console.Out.WriteLine();
			Console.Out.WriteLine("     *use ! to set any option to opposite value. overrides option.");
			Console.Out.WriteLine("      for example use /!w to not search for whole word.");
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
			Console.Out.WriteLine(string.Empty);
			Console.Out.WriteLine("{0}.exe - {1}", envName, prodName);
			Console.Out.WriteLine(string.Empty);
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

			Console.Out.WriteLine(string.Empty);
		}
	}
}
