using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Confuser.Core;
using Confuser.Core.Project;

namespace Confuser.CLI {
	internal class Program {
		static int Main(string[] args) {
			ConsoleColor original = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.White;
			string originalTitle = null;
			if (OperatingSystem.IsWindows()) {
				originalTitle = Console.Title;
				Console.Title = "Neo-ConfuserEx";
			}

			try {
				return CreateRootCommand().Parse(args).Invoke();
			}
			finally {
				Console.ForegroundColor = original;
				if (originalTitle != null)
					Console.Title = originalTitle;
			}
		}

		static RootCommand CreateRootCommand() {
			var noPauseOption = new Option<bool>("--no-pause") {
				Description = "Do not pause after protection finishes."
			};
			noPauseOption.Aliases.Add("-n");
			noPauseOption.Aliases.Add("--nopause");

			var outputOption = new Option<string>("--out") {
				Description = "Output directory for protected modules."
			};
			outputOption.Aliases.Add("-o");

			var probeOption = new Option<string[]>("--probe") {
				Description = "Additional assembly probe directory. May be specified more than once."
			};
			var pluginOption = new Option<string[]>("--plugin") {
				Description = "Plugin assembly path. May be specified more than once."
			};
			var debugOption = new Option<bool>("--debug") {
				Description = "Generate debug symbols."
			};
			var inputsArgument = new Argument<string[]>("inputs") {
				Description = "A project file, or one or more modules followed by an optional project template.",
				Arity = ArgumentArity.OneOrMore
			};

			var command = new RootCommand("Protect .NET assemblies with Neo-ConfuserEx.") {
				noPauseOption,
				outputOption,
				probeOption,
				pluginOption,
				debugOption,
				inputsArgument
			};
			command.SetAction(parseResult => Execute(
				parseResult.GetValue(inputsArgument),
				parseResult.GetValue(noPauseOption),
				parseResult.GetValue(outputOption),
				parseResult.GetValue(probeOption) ?? Array.Empty<string>(),
				parseResult.GetValue(pluginOption) ?? Array.Empty<string>(),
				parseResult.GetValue(debugOption)));
			return command;
		}

		static int Execute(
			string[] files,
			bool noPause,
			string outDir,
			string[] probePaths,
			string[] plugins,
			bool debug) {
			try {
				var parameters = new ConfuserParameters();

				if (files.Length == 1 && HasExtension(files[0], ".crproj")) {
					var proj = new ConfuserProject();
					try {
						var xmlDoc = new XmlDocument();
						xmlDoc.Load(files[0]);
						proj.Load(xmlDoc);
						proj.BaseDirectory = Path.GetFullPath(
							proj.BaseDirectory,
							Path.GetDirectoryName(Path.GetFullPath(files[0])) ?? Environment.CurrentDirectory);
					}
					catch (Exception ex) {
						WriteLineWithColor(ConsoleColor.Red, "Failed to load project:");
						WriteLineWithColor(ConsoleColor.Red, ex.ToString());
						return -1;
					}

					parameters.Project = proj;
				}
				else {
					if (string.IsNullOrEmpty(outDir)) {
						Console.WriteLine("ConfuserEx.CLI: No output directory specified.");
						return -1;
					}

					var proj = new ConfuserProject();
					if (HasExtension(files[files.Length - 1], ".crproj")) {
						var templateProj = new ConfuserProject();
						var xmlDoc = new XmlDocument();
						xmlDoc.Load(files[files.Length - 1]);
						templateProj.Load(xmlDoc);
						Array.Resize(ref files, files.Length - 1);

						foreach (var rule in templateProj.Rules)
							proj.Rules.Add(rule);
						proj.Seed = templateProj.Seed;
						proj.Debug = templateProj.Debug;
					}

					if (files.Length == 0)
						throw new ArgumentException("No input modules specified.");

					proj.BaseDirectory = Path.GetFullPath(Path.GetDirectoryName(files[0]) ?? ".");
					foreach (var input in files) {
						string fullInputPath = Path.GetFullPath(input);
						proj.Add(new ProjectModule {
							Path = Path.GetRelativePath(proj.BaseDirectory, fullInputPath)
						});
					}

					proj.OutputDirectory = Path.GetFullPath(outDir);
					foreach (var path in probePaths)
						proj.ProbePaths.Add(path);
					foreach (var path in plugins)
						proj.PluginPaths.Add(path);
					proj.Debug = debug;
					parameters.Project = proj;
				}

				int retVal = RunProject(parameters);
				if (NeedPause() && !noPause) {
					Console.WriteLine("Press any key to continue...");
					Console.ReadKey(true);
				}
				return retVal;
			}
			catch (Exception ex) {
				WriteLineWithColor(ConsoleColor.Red, "ConfuserEx.CLI: " + ex.Message);
				return -1;
			}
		}

		static bool HasExtension(string path, string extension) {
			return string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);
		}

		static int RunProject(ConfuserParameters parameters) {
			var logger = new ConsoleLogger();
			parameters.Logger = logger;

			if (OperatingSystem.IsWindows())
				Console.Title = "ConfuserEx - Running...";
			ConfuserEngine.Run(parameters).Wait();
			return logger.ReturnValue;
		}

		static bool NeedPause() {
			return Debugger.IsAttached || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROMPT"));
		}

		static void WriteLineWithColor(ConsoleColor color, string txt) {
			ConsoleColor original = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.WriteLine(txt);
			Console.ForegroundColor = original;
		}

		static void WriteLine(string txt) {
			Console.WriteLine(txt);
		}

		static void WriteLine() {
			Console.WriteLine();
		}

		sealed class ConsoleLogger : ILogger {
			readonly DateTime begin = DateTime.Now;

			public int ReturnValue { get; private set; }

			public void Debug(string msg) {
				WriteLineWithColor(ConsoleColor.Gray, "[DEBUG] " + msg);
			}

			public void DebugFormat(string format, params object[] args) {
				WriteLineWithColor(ConsoleColor.Gray, "[DEBUG] " + string.Format(format, args));
			}

			public void Info(string msg) {
				WriteLineWithColor(ConsoleColor.White, " [INFO] " + msg);
			}

			public void InfoFormat(string format, params object[] args) {
				WriteLineWithColor(ConsoleColor.White, " [INFO] " + string.Format(format, args));
			}

			public void Warn(string msg) {
				WriteLineWithColor(ConsoleColor.Yellow, " [WARN] " + msg);
			}

			public void WarnFormat(string format, params object[] args) {
				WriteLineWithColor(ConsoleColor.Yellow, " [WARN] " + string.Format(format, args));
			}

			public void WarnException(string msg, Exception ex) {
				WriteLineWithColor(ConsoleColor.Yellow, "[WARN] " + msg);
				WriteLineWithColor(ConsoleColor.Yellow, "Exception: " + ex);
			}

			public void Error(string msg) {
				WriteLineWithColor(ConsoleColor.Red, "[ERROR] " + msg);
			}

			public void ErrorFormat(string format, params object[] args) {
				WriteLineWithColor(ConsoleColor.Red, "[ERROR] " + string.Format(format, args));
			}

			public void ErrorException(string msg, Exception ex) {
				WriteLineWithColor(ConsoleColor.Red, "[ERROR] " + msg);
				WriteLineWithColor(ConsoleColor.Red, "Exception: " + ex);
			}

			public void Progress(int progress, int overall) { }

			public void EndProgress() { }

			public void Finish(bool successful) {
				DateTime now = DateTime.Now;
				string timeString = string.Format(
					"at {0}, {1}:{2:d2} elapsed.",
					now.ToShortTimeString(),
					(int)now.Subtract(begin).TotalMinutes,
					now.Subtract(begin).Seconds);
				if (successful) {
					if (OperatingSystem.IsWindows())
						Console.Title = "ConfuserEx - Success";
					WriteLineWithColor(ConsoleColor.Green, "Finished " + timeString);
					ReturnValue = 0;
				}
				else {
					if (OperatingSystem.IsWindows())
						Console.Title = "ConfuserEx - Fail";
					WriteLineWithColor(ConsoleColor.Red, "Failed " + timeString);
					ReturnValue = 1;
				}
			}
		}
	}
}
