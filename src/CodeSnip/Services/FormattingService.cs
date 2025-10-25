using CSharpier.Core.CSharp;
using CSharpier.Core.Xml;
using System.Diagnostics;
using System.IO;

namespace CodeSnip.Services
{
    /// <summary>
    /// Provides services for formatting code snippets using various external and internal formatters.
    /// </summary>
    public static class FormattingService
    {
        private static bool? _isPythonInstalled;
        private static bool? _isBlackInstalled;
        private static bool? _isRustfmtInstalled;

        /// <summary>
		/// Formats C# code using the built-in CSharpier library.
		/// </summary>
		/// <param name="code">The C# code to format.</param>
		/// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool isSuccess, string? formattedCode, string? errorMessage)> TryFormatCodeWithCSharpierAsync(string code)
        {
            try
            {
                var result = await CSharpFormatter.FormatAsync(code);
                return (true, result.Code, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Formats XML code using the built-in CSharpier library.
        /// </summary>
        /// <param name="code">The XML code to format.</param>
        /// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool isSuccess, string? formattedCode, string? errorMessage)> TryFormatXmlWithCSharpierAsync(string code)
        {
            try
            {
                var result = await Task.Run(() => XmlFormatter.Format(code));
                return (true, result.Code, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
		/// Formats code using the 'clang-format.exe' tool located in the 'Tools' directory.
		/// </summary>
		/// <param name="code">The source code to format.</param>
		/// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
		/// <param name="assumeFilename">An optional filename to help clang-format determine the language and style.</param>
		/// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithClangAsync(string code, int timeoutMs = 5000, string? assumeFilename = null)
        {
            string arguments = "";
            if (!string.IsNullOrEmpty(assumeFilename))
            {
                arguments = $"--assume-filename=\"{assumeFilename}\"";
            }

            return await TryFormatWithExternalProcessAsync("clang-format.exe", arguments, code, timeoutMs);
        }

        /// <summary>
		/// Formats D code using the 'dfmt.exe' tool from the 'Tools' directory.
		/// </summary>
		/// <param name="code">The D code to format.</param>
		/// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
		/// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithDfmtAsync(string code, int timeoutMs = 5000)
        {
            return await TryFormatWithExternalProcessAsync("dfmt.exe", "", code, timeoutMs);
        }

        /// <summary>
        /// Formats Rust code using the `rustfmt` command-line tool, if available in the system's PATH.
        /// </summary>
        /// <param name="code">The Rust code to format.</param>
        /// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
        /// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithRustFmtAsync(string code, int timeoutMs = 5000)
        {
            if (!await IsRustfmtInstalledAsync())
            {
                return (false, null, "rustfmt is not installed or not in your PATH.");
            }

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string toolPath = Path.Combine(baseDirectory, "Tools");

            toolPath = Directory.Exists(toolPath) ? toolPath : baseDirectory;

            var startInfo = new ProcessStartInfo
            {
                FileName = "rustfmt",
                Arguments = "", // rustfmt reads from stdin by default
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = toolPath // Set the working directory to the Tools folder to ensure it can locate its config file
            };

            try
            {
                using var process = new Process { StartInfo = startInfo };
                process.Start();

                await process.StandardInput.WriteAsync(code);
                process.StandardInput.Close();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                using var cts = new CancellationTokenSource(timeoutMs);
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(); } catch { /* Ignore errors */ }
                    return (false, null, "Timeout: rustfmt took too long to format the code.");
                }

                string stdError = await errorTask;
                if (process.ExitCode != 0)
                {
                    return (false, null, $"rustfmt formatting error: {stdError.Trim()}");
                }

                return (true, await outputTask, null);
            }
            catch (Exception ex)
            {
                return (false, null, $"An exception occurred while running rustfmt: {ex.Message}");
            }
        }

        /// <summary>
		/// Formats Python code using the 'ruff.exe' tool from the 'Tools' directory.
		/// </summary>
		/// <param name="code">The Python code to format.</param>
		/// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
		/// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithRuffAsync(string code, int timeoutMs = 5000)
        {
            string arguments = "format --no-cache --stdin-filename=temp.py -";
            return await TryFormatWithExternalProcessAsync("ruff.exe", arguments, code, timeoutMs);
        }

        /// <summary>
        /// Formats Go code using the 'gofmt.exe' tool from the 'Tools' directory.
        /// </summary>
        /// <param name="code">The go code to format</param>
        /// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
        /// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithGofmtAsync(string code, int timeoutMs = 5000) { 
            return await TryFormatWithExternalProcessAsync("gofmt.exe", "", code, timeoutMs);
        }

        /// <summary>
        /// Formats Lua code using the 'stylua.exe' tool from the 'Tools' directory.
        /// </summary>
        /// <param name="code">The Lua code to format.</param>
        /// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
        /// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithStyluaAsync(string code, int timeoutMs = 5000)
        {
            return await TryFormatWithExternalProcessAsync("stylua.exe", "-", code, timeoutMs);
        }

        /// <summary>
        /// A generic helper method to run an external formatting tool from the 'Tools' directory.
        /// </summary>
        /// <param name="executableName">The name of the executable file (e.g., 'dfmt.exe').</param>
        /// <param name="arguments">The command-line arguments to pass to the executable.</param>
        /// <param name="code">The source code to pass to the process's standard input.</param>
        /// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
        /// <returns>A tuple indicating success, the formatted code from standard output, and any error message from standard error.</returns>
        private static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatWithExternalProcessAsync(
            string executableName,
            string arguments,
            string code,
            int timeoutMs = 5000)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string toolPath = Path.Combine(baseDirectory, "Tools", executableName);

            if (!File.Exists(toolPath))
            {
                return (false, null, $"Formatter executable not found at: {toolPath}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(toolPath) // Set the working directory to the formatter's folder to ensure it can locate its config files
            };

            try
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    // Asynchronously write to the process's standard input and then close it
                    await process.StandardInput.WriteAsync(code);
                    process.StandardInput.Close();

                    // Start reading output and error streams asynchronously
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    // Asynchronously wait for the process to exit with a timeout
                    using var cts = new CancellationTokenSource(timeoutMs);
                    try
                    {
                        await process.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try { process.Kill(); } catch { /* Ignore errors if the process is already gone */ }
                        return (false, null, $"Timeout: The '{executableName}' process took too long to respond.");
                    }

                    // Await the results of the read operations
                    string output = await outputTask;
                    string error = await errorTask;

                    if (process.ExitCode != 0)
                    {
                        return (false, null, $"{executableName} error (exit code {process.ExitCode}): {error.Trim()}");
                    }

                    return (true, output, null);
                }
            }
            catch (Exception ex)
            {
                return (false, null, $"Formatting exception for '{executableName}':\n{ex.Message}");
            }
        }

        /// <summary>
		/// Formats Python code using the 'black' formatter via an installed Python environment.
		/// </summary>
		/// <param name="code">The Python code to format.</param>
		/// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
		/// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithBlackAsync(string code, int timeoutMs = 5000)
        {
            if (!await IsPythonInstalledAsync())
            {
                return (false, null, "Python is not installed.");
            }

            if (!await IsBlackInstalledAsync())
            {
                return (false, null, "Black formatter is not installed.");
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = "python",
                Arguments = "-m black -", // "-" means Black is reading from stdin
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                {
                    return (false, null, "Can't start the Python process.");
                }

                await process.StandardInput.WriteAsync(code);
                process.StandardInput.Close();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                using var cts = new CancellationTokenSource(timeoutMs);
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(); } catch { /* Ignore errors */ }
                    return (false, null, "Timeout when formatting code.");
                }

                string stdOutput = await outputTask;
                string stdError = await errorTask;

                if (process.ExitCode != 0)
                {
                    return (false, null, $"Formatting error: {stdError.Trim()}");
                }

                return (true, stdOutput.Trim(), null);
            }
            catch (Exception ex)
            {
                return (false, null, $"Error: {ex.Message}");
            }
        }

        /// <summary>
		/// Formats a Python file in-place using the 'black' formatter.
		/// </summary>
		/// <param name="code">The Python code to write to a temporary file and format.</param>
		/// <param name="timeoutMs">The timeout in milliseconds for the process.</param>
		/// <returns>A tuple indicating success, the formatted code, and any error message.</returns>
        public static async Task<(bool Success, string? FormattedCode, string? ErrorMessage)> TryFormatCodeWithBlackFileAsync(string code, int timeoutMs = 5000)
        {
            if (!await IsPythonInstalledAsync())
            {
                return (false, null, "Python is not installed.");
            }

            if (!await IsBlackInstalledAsync())
            {
                return (false, null, "Black formatter is not installed.");
            }

            string tempFilePath = Path.GetTempFileName() + ".py";

            try
            {
                await File.WriteAllTextAsync(tempFilePath, code);

                ProcessStartInfo startInfo = new()
                {
                    FileName = "python",
                    Arguments = $"-m black \"{tempFilePath}\" --fast",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    var errorTask = process.StandardError.ReadToEndAsync();

                    using var cts = new CancellationTokenSource(timeoutMs);
                    try
                    {
                        await process.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try { process.Kill(); } catch { /* Ignore errors */ }
                        return (false, null, "Timeout when formatting code.");
                    }

                    string stdError = await errorTask;

                    if (process.ExitCode != 0)
                    {
                        return (false, null, $"Formatting error: {stdError.Trim()}");
                    }
                }

                var formattedCode = await File.ReadAllTextAsync(tempFilePath);
                return (true, formattedCode, null);
            }
            catch (Exception ex)
            {
                return (false, null, $"Error: {ex.Message}");
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { /* Ignore errors */ }
                }
            }
        }

        static async Task<bool> IsPythonInstalledAsync()
        {
            if (_isPythonInstalled.HasValue)
                return _isPythonInstalled.Value;
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = "python",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    var result = process.ExitCode == 0 && (output.StartsWith("Python") || error.StartsWith("Python"));
                    _isPythonInstalled = result;
                    return result;
                }
            }
            catch
            {
                _isPythonInstalled = false;
                return false;
            }
        }


        static async Task<bool> IsBlackInstalledAsync()
        {
            if (_isBlackInstalled.HasValue)
                return _isBlackInstalled.Value;
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = "python",
                    Arguments = "-m black --version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    var result = process.ExitCode == 0 && output.Contains("black", StringComparison.CurrentCultureIgnoreCase);
                    _isBlackInstalled = result;
                    return result;
                }
            }
            catch
            {
                _isBlackInstalled = false;
                return false;
            }
        }

        static async Task<bool> IsRustfmtInstalledAsync()
        {
            if (_isRustfmtInstalled.HasValue)
                return _isRustfmtInstalled.Value;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "rustfmt",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var result = process.ExitCode == 0 && output.Contains("rustfmt", StringComparison.CurrentCultureIgnoreCase);
                _isRustfmtInstalled = result;
                return result;
            }
            catch
            {
                _isRustfmtInstalled = false;
                return false;
            }
        }

    }

}

