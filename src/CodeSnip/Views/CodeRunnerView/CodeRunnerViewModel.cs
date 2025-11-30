using CodeSnip.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace CodeSnip.Views.CodeRunnerView
{
    public partial class CodeRunnerViewModel : ObservableObject
    {
        private readonly CompilerSettingsService _compilersSettings = new();

        private readonly HttpClient _httpClient = new();

        private readonly Func<string> _getLatestCode;

        private readonly GodboltService _godboltService;

        [ObservableProperty]
        private List<CompilerInfo>? _compilers = [];

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        [NotifyCanExecuteChangedFor(nameof(GetLinkCommand))]
        private CompilerInfo? _selectedCompiler;

        [ObservableProperty]
        private string _flags = "";

        [ObservableProperty]
        private string _stdOut = "";

        [ObservableProperty]
        private string _errorText = "";

        [ObservableProperty]
        private string _code = "";

        [ObservableProperty]
        private string _extension = "";

        [ObservableProperty]
        private string _shortLink = "";

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private string _reloadBadge = "";

        [ObservableProperty]
        private string _asmCode = "";

        [ObservableProperty]
        private string _asmHighlightingName = "asm"; // Default to "asm"

        [ObservableProperty]
        private bool _showAsm = false;

        [ObservableProperty]
        private bool _hasError = false;

        [ObservableProperty]
        private bool _hasOut = true;

        public Process? RunningProcess { get; private set; }

        private static readonly Dictionary<string, (string exe, string args)> Interpreters = new()
        {
            ["py"] = ("python.exe", "-u -"),
            ["lua"] = ("lua.exe", "-"),
            ["js"] = ("node.exe", "-"),
            ["rb"] = ("ruby.exe", "-"),
            ["pl"] = ("perl.exe", "-"),
            ["php"] = ("php.exe", ""),
            ["java"] = ("jshell.exe", "-s -"),
            ["ps1"] = ("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand ")
            //["ps1"] = ("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command -")
        };

        public CodeRunnerViewModel(string languageExtension, string code, Func<string> getLatestCode)
        {
            Extension = languageExtension;// // Must set before triggering OnSelectedCompilerChanged (it depends on Extension)
            Compilers = _compilersSettings.GetCompilersByExtension(languageExtension);
            var defaultCompilerId = _compilersSettings.GetDefaultCompilerIdByExtension(languageExtension);
            SelectedCompiler = Compilers.FirstOrDefault(c => c.Id == defaultCompilerId) ?? Compilers.FirstOrDefault();
            Code = code;
            _getLatestCode = getLatestCode;
            _godboltService = new GodboltService(_httpClient);

        }

        // This method maps the source language extension to the appropriate assembly highlighting definition name.
        private static string MapLanguageExtensionToAsmHighlighting(string languageExtension, CompilerInfo? compiler)
        {
            if (languageExtension.Equals("cs", StringComparison.OrdinalIgnoreCase) &&
                compiler?.Id?.Contains("ildasm", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "il";
            }

            return languageExtension.ToLowerInvariant() switch
            {

                "java" => "javaopc",
                _ => "asm", // Default for C++, Rust, D, etc.
            };
        }

        partial void OnSelectedCompilerChanged(CompilerInfo? oldValue, CompilerInfo? newValue)
        {
            Flags = newValue?.Flags ?? "";
            AsmHighlightingName = MapLanguageExtensionToAsmHighlighting(Extension, newValue);
            // clear previous outputs
            AsmCode = "";
            StdOut = "";
            ErrorText = "";
        }

        private bool CanExecuteCompilerActions()
        {
            return SelectedCompiler != null;
        }

        [RelayCommand(CanExecute = nameof(CanExecuteCompilerActions))]
        private async Task Run()
        {
            try
            {
                IsRunning = true;
                await CompileSnippetAsync();
            }
            finally
            {
                IsRunning = false;
            }
        }

        partial void OnErrorTextChanged(string value)
        {
            HasError = !string.IsNullOrEmpty(value);
        }

        partial void OnStdOutChanged(string value)
        {
            HasOut = !string.IsNullOrEmpty(value);
        }

        private async Task CompileSnippetAsync()
        {
            string? langId = _compilersSettings.GetLanguageIdByExtension(Extension); // godbolt languageId (c++, csharp ...)
            // Set skipAsm to false to get both execution output and assembler
            var (stdout, stderr, asm, error) = await _godboltService.CompileAndRunAsync(
                Code, SelectedCompiler!.Id ?? "", langId ?? "", Flags, !ShowAsm); // if ShowAsm is true, then SkipAsm must be false

            StdOut = string.IsNullOrEmpty(stdout) ? "" : stdout;
            ErrorText = RemoveAnsiCodes(stderr);
            AsmCode = asm ?? ""; // Store raw asm

            if (!string.IsNullOrEmpty(error))
            {
                ErrorText = error;
                StdOut = "";
                AsmCode = "";
                return;
            }
            // The AsmCode property is already set. The UI will bind to this directly.SS
        }

        [RelayCommand(CanExecute = nameof(CanExecuteCompilerActions))]
        private async Task GetLink()
        {
            try
            {
                IsRunning = true;
                await GetShortenerLinkAsync();
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task GetShortenerLinkAsync()
        {
            string? langId = _compilersSettings.GetLanguageIdByExtension(Extension); // godbolt languageId (c++, csharp ...)
            var (link, error) = await _godboltService.GetShortLinkAsync(langId ?? "", Code, SelectedCompiler!.Id ?? "", Flags);
            ShortLink = string.IsNullOrEmpty(link) ? "" : link;
            ErrorText = string.IsNullOrEmpty(error) ? "" : error;
        }

        private static string RemoveAnsiCodes(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var ansiRegex = new Regex(@"\x1B\[[0-9;]*[mK]");
            return ansiRegex.Replace(input, "");
        }

        [RelayCommand]
        private async Task Reload()
        {
            Code = _getLatestCode();
            ReloadBadge = "✓";
            await Task.Delay(1000);
            ReloadBadge = "";
        }

        private bool CanNavigateToLink()
        {
            return !string.IsNullOrWhiteSpace(ShortLink);
        }

        [RelayCommand(CanExecute = nameof(CanNavigateToLink))]
        private void NavigateToLink()
        {
            if (!string.IsNullOrWhiteSpace(ShortLink))
            {
                if (Uri.TryCreate(ShortLink, UriKind.Absolute, out var uri))
                {
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunLocal))]
        private async Task RunLocal()
        {
            try
            {
                IsRunning = true;
                StdOut = "";
                ErrorText = "";

                var (interpreterPath, arguments) = GetLocalInterpreter(Extension);

                if (string.IsNullOrWhiteSpace(interpreterPath))
                {
                    ErrorText = $"No local interpreter configured for extension '{Extension}'.";
                    return;
                }

                int timeout = Timeout.Infinite; // No timeout, rely on manual kill, beacuse some script may run long tasks
                switch (Extension.ToLowerInvariant())
                {
                    case "ps1": // PowerShell case
                        {
                            string base64Script = Convert.ToBase64String(Encoding.Unicode.GetBytes(Code));
                            arguments += base64Script;

                            // For PS, the code is in the arguments, so the 'input' parameter is empty.
                            await RunProcessAsync_Dynamic_Reading(interpreterPath, arguments, "", timeout,
                                                  outputLine => Application.Current.Dispatcher.Invoke(() => StdOut += outputLine + "\n"),
                                                  errorLine => Application.Current.Dispatcher.Invoke(() => ErrorText += errorLine + "\n"));

                            break;
                        }
                    default: // Default case for all other interpreters
                        // New way of reading output dynamically
                        await RunProcessAsync_Dynamic_Reading(interpreterPath, arguments, Code, timeout,
                                              outputLine => Application.Current.Dispatcher.Invoke(() => StdOut += outputLine + "\n"),
                                              errorLine => Application.Current.Dispatcher.Invoke(() => ErrorText += errorLine + "\n"));
                        // Old way of reading all output at once after process ends
                        //var (success, output, error) = await RunProcessAsync(interpreterPath, arguments, Code, timeout);
                        //StdOut = output ?? "";
                        //ErrorText = error ?? "";
                        break;
                }
            }
            catch (Exception ex)
            {
                ErrorText = $"Error running local interpreter:\n{ex.Message}";
                StdOut = "";
            }
            finally
            {
                IsRunning = false;
                RunningProcess = null;
            }
        }

        /// <summary>
        /// Attempts to locate a local interpreter executable and its default arguments for the specified compiler file
        /// extension.
        /// </summary>
        /// <remarks>The method searches for interpreter executables in the "Tools\Interpreters" directory
        /// under the application's base directory. For C# scripts ("cs"), it looks for "csrunner.exe". If the
        /// interpreter executable is not found locally, the method may return the executable name as a fallback, which
        /// may require the interpreter to be available in the system PATH.</remarks>
        /// <param name="compilerExtension">The file extension of the compiler or script language, with or without a leading period (e.g., ".cs" or
        /// "cs"). Case-insensitive. If null or whitespace, no interpreter is returned.</param>
        /// <returns>A tuple containing the full path to the interpreter executable and its default arguments, or (null, null) if
        /// no suitable interpreter is found.</returns>
        private static (string? path, string? args) GetLocalInterpreter(string? compilerExtension)
        {
            if (string.IsNullOrWhiteSpace(compilerExtension))
                return (null, null);

            compilerExtension = compilerExtension.TrimStart('.').ToLowerInvariant();

            string toolsDir = Path.Combine(AppContext.BaseDirectory, "Tools\\Interpreters");
            // Special case for C# scripting
            if (compilerExtension == "cs")
            {
                string csrunnerPath = Path.Combine(toolsDir, "csrunner.exe");
                return File.Exists(csrunnerPath) ? (csrunnerPath, null) : (null, null);
            }

            if (!Interpreters.TryGetValue(compilerExtension, out var info))
                return (null, null);


            string interpreterPath = Path.Combine(toolsDir, info.exe);

            return File.Exists(interpreterPath)
                ? (interpreterPath, info.args)
                : (info.exe, info.args);
        }

        private bool CanRunLocal()
        {
            return !string.IsNullOrWhiteSpace(GetLocalInterpreter(Extension).path);
        }

        /// <summary>
        /// Runs an external process asynchronously with the specified input and captures its standard output and error
        /// streams.
        /// </summary>
        /// <remarks>If the process does not exit within the specified timeout, it is forcefully
        /// terminated and the Error field contains a timeout message. The method redirects standard input, output, and
        /// error streams. The working directory is set to the directory containing the executable file, or the
        /// application's base directory if not specified.</remarks>
        /// <param name="fileName">The path to the executable file to run. Cannot be null or empty.</param>
        /// <param name="arguments">The command-line arguments to pass to the process, or null to pass no arguments.</param>
        /// <param name="input">The text to write to the standard input stream of the process. Can be an empty string if no input is
        /// required.</param>
        /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for the process to complete before terminating it. Must be
        /// greater than zero.</param>
        /// <returns>A tuple containing a boolean indicating whether the process completed successfully, the captured standard
        /// output (or null if none), and the captured standard error (or null if none). If the process fails to start
        /// or times out, Success is false and Error contains a descriptive message.</returns>
        private async Task<(bool Success, string? Output, string? Error)> RunProcessAsync(string fileName, string? arguments, string input, int timeoutMs)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? "",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(fileName) ?? AppContext.BaseDirectory
            };

            using var process = new Process { StartInfo = psi };
            RunningProcess = process;
            if (!process.Start())
            {
                return (false, null, $"Failed to start process: {fileName}");
            }

            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                await process.WaitForExitAsync(cts.Token);

                string output = await outputTask;
                string error = await errorTask;

                return (process.ExitCode == 0,
                        string.IsNullOrWhiteSpace(output) ? null : output,
                        string.IsNullOrWhiteSpace(error) ? null : error);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch { /* Ignore errors during forceful termination */ }

                return (false, null, $"Timeout: The process '{process.ProcessName}' (PID: {process.Id}) took too long to respond and was terminated.");
            }
        }

        /// <summary>
        /// Runs an external process asynchronously with the specified input and captures its standard output and error
        /// streams in real time.
        /// </summary>
        /// <remarks>If the process does not complete within the specified timeout, it is terminated and
        /// an error message is sent to the error callback. Both output and error callbacks are invoked for each line of
        /// output or error received from the process. This method does not throw exceptions for process errors;
        /// instead, error information is provided via the error callback.</remarks>
        /// <param name="fileName">The path to the executable file to run. Must not be null or empty.</param>
        /// <param name="arguments">The command-line arguments to pass to the process, or null to run the process without arguments.</param>
        /// <param name="input">The input string to write to the process's standard input. If empty, no input is sent.</param>
        /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for the process to complete before terminating it. Must be
        /// greater than zero.</param>
        /// <param name="onOutputReceived">A callback invoked each time a line of text is received from the process's standard output. Cannot be null.</param>
        /// <param name="onErrorReceived">A callback invoked each time a line of text is received from the process's standard error, or when an error
        /// or timeout occurs. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task completes when the process exits and all output
        /// has been read.</returns>
        private async Task RunProcessAsync_Dynamic_Reading(
                   string fileName, string? arguments, string input, int timeoutMs,
                   Action<string> onOutputReceived, Action<string> onErrorReceived)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? "",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(fileName) ?? AppContext.BaseDirectory
            };

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            RunningProcess = process;

            var processCompletion = new TaskCompletionSource<bool>();
            int streamReadersFinished = 0;

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data is null)
                {
                    if (Interlocked.Increment(ref streamReadersFinished) == 2)
                        processCompletion.TrySetResult(true);
                }
                else
                {
                    onOutputReceived?.Invoke(e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data is null)
                {
                    if (Interlocked.Increment(ref streamReadersFinished) == 2)
                        processCompletion.TrySetResult(true);
                }
                else
                {
                    onErrorReceived?.Invoke(e.Data);
                }
            };

            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                if (!process.Start())
                {
                    onErrorReceived?.Invoke($"Failed to start process: {fileName}");
                    return;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!string.IsNullOrEmpty(input))
                {
                    await process.StandardInput.WriteAsync(input);
                }
                process.StandardInput.Close();

                var completedTask = await Task.WhenAny(processCompletion.Task, Task.Delay(timeoutMs, cts.Token));

                if (completedTask != processCompletion.Task)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch { /* Ignore exceptions during kill */ }

                    onErrorReceived?.Invoke($"Timeout: The process '{psi.FileName}' took too long and was terminated.");
                }
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                    try { process.Kill(entireProcessTree: true); } catch { }

                onErrorReceived?.Invoke("Timeout: The process took too long and was terminated.");
            }
            catch (Exception ex)
            {
                onErrorReceived?.Invoke(ex.Message);
            }
            finally
            {
                process.Dispose();
            }
        }

        [RelayCommand]
        private void KillRunningProcess()
        {
            try
            {
                if (RunningProcess != null && !RunningProcess.HasExited)
                {
                    RunningProcess.Kill(entireProcessTree: true);
                    ErrorText += $"Process '{RunningProcess?.ProcessName}' (ID: {RunningProcess?.Id}) was terminated by the user.\n";
                }
            }
            catch (Exception ex)
            {
                ErrorText += $"\nError terminating process: {RunningProcess?.ProcessName} (ID: {RunningProcess?.Id})\n{ex.Message}";
            }
            finally
            {
                RunningProcess = null;
            }
        }

    }
}
