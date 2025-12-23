using CodeSnip.Services.Shortener;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeSnip.Services
{
    /// <summary>
    /// A service for interacting with the Compiler Explorer (godbolt.org) API.
    /// It handles compiling code, running executables, and generating short links.
    /// </summary>
    public class GodboltService
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="GodboltService"/> class.
        /// </summary>
        /// <param name="client">An <see cref="HttpClient"/> instance to use for API requests.</param>
        public GodboltService(HttpClient client)
        {
            _httpClient = client;
            //_httpClient.Timeout = TimeSpan.FromSeconds(30); // ?
            // default Accept headers ...
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );
        }

        /// <summary>
        /// Compiles and optionally executes source code using the Godbolt API.
        /// </summary>
        /// <param name="sourceCode">The source code to compile.</param>
        /// <param name="compilerId">The ID of the compiler to use (e.g., "g132").</param>
        /// <param name="lang">The language ID (e.g., "cpp", "csharp").</param>
        /// <param name="userArgs">Command-line arguments to pass to the compiler.</param>
        /// <param name="skipAsm">If true, skips assembly generation and only executes the code.</param>
        /// <returns>A tuple containing stdout, stderr, the generated assembly (if requested), and any error message.</returns>
        public async Task<(string Stdout, string Stderr, string? Asm, string? ErrorMessage)> CompileAndRunAsync(
    string sourceCode, string compilerId, string lang, string userArgs = "", bool skipAsm = true)
        {
            try
            {
                var request = new GodboltCompileRequest
                {
                    Source = sourceCode,
                    Lang = lang,
                    AllowStoreCodeDebug = true,
                    Options = new GodboltOptions
                    {
                        UserArguments = userArgs,
                        CompilerOptions = new CompilerOptions
                        {
                            SkipAsm = skipAsm,
                            ExecutorRequest = skipAsm
                        },
                        Filters = new Filters
                        {
                            Execute = true,
                            Intel = true,
                            Labels = true,
                            DebugCalls = false,
                            Binary = false,
                            CommentOnly = true,
                            Demangle = true,
                            Trim = false,
                            BinaryObject = false,
                            Directives = true,
                            LibraryCode = false
                        }
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"https://godbolt.org/api/compiler/{compilerId}/compile", request);

                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    return ("", "", null, $"HTTP Error {response.StatusCode}: {errBody}");
                }
                var json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json))
                    return ("", "", null, "Godbolt API returned an empty response.");

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var resp = JsonSerializer.Deserialize<GodboltResponse>(json, options);
                if (resp == null)
                    return ("", "", null, "Unexpected response format (unable to parse JSON).");

                var (stdout, stderr, asmList) = ParseOutputs(resp);
                string? asm = asmList != null
                    ? string.Join(Environment.NewLine, asmList.Select(a => a.Text))
                    : null;

                return (stdout, stderr, asm, null);
            }
            catch (Exception ex)
            {
                return ("", "", null, $"Error:\n {ex.Message}");
            }
        }

        /// <summary>
        /// Parses the complex response from the Godbolt API into structured stdout, stderr, and assembly output.
        /// It handles different response formats for compile-only, execute-only, and compile-and-run requests.
        /// </summary>
        /// <param name="resp">The deserialized <see cref="GodboltResponse"/> from the API.</param>
        /// <returns>A tuple containing the parsed stdout, stderr, and a list of assembly lines.</returns>
        public static (string Stdout, string Stderr, List<AsmLine>? Asm) ParseOutputs(GodboltResponse resp)
        {
            string stdout;
            string stderr;
            List<AsmLine>? asm = resp.Asm;

            // Case 1: Compile & Execute response (like WithAsm.json example). This has an `ExecResult` property.
            if (resp.ExecResult != null)
            {
                stdout = string.Join(Environment.NewLine, resp.ExecResult.Stdout?.Select(s => s.Text) ?? Enumerable.Empty<string>());

                var compilerStderr = string.Join(Environment.NewLine, resp.Stderr?.Select(s => s.Text) ?? Enumerable.Empty<string>());
                var execStderr = string.Join(Environment.NewLine, resp.ExecResult.Stderr?.Select(s => s.Text) ?? Enumerable.Empty<string>());

                var stderrBuilder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(compilerStderr))
                {
                    stderrBuilder.Append(compilerStderr);
                }
                if (!string.IsNullOrWhiteSpace(execStderr))
                {
                    if (stderrBuilder.Length > 0)
                    {
                        stderrBuilder.AppendLine().AppendLine("--- Execution Stderr ---");
                    }
                    stderrBuilder.Append(execStderr);
                }
                stderr = stderrBuilder.ToString();
            }
            // Case 2: Execute-only response (like NoAsm.json example). This has no `ExecResult`.
            else
            {
                stdout = string.Join(Environment.NewLine, resp.Stdout?.Select(s => s.Text) ?? Enumerable.Empty<string>());

                // Stderr (compiler warnings for the wrapper) is in the BuildResult.
                var buildStderr = string.Join(Environment.NewLine, resp.BuildResult?.Stderr?.Select(s => s.Text) ?? Enumerable.Empty<string>());
                if (!string.IsNullOrWhiteSpace(buildStderr))
                {
                    stderr = buildStderr;
                }
                else
                {
                    // Fallback to root stderr for execution errors.
                    stderr = string.Join(Environment.NewLine, resp.Stderr?.Select(s => s.Text) ?? Enumerable.Empty<string>());
                }
            }

            return (stdout, stderr, asm);
        }

        /// <summary>
        /// Generates a short URL for a Compiler Explorer session.
        /// </summary>
        /// <param name="language">The language ID (e.g., "cpp", "csharp").</param>
        /// <param name="sourceCode">The source code for the session.</param>
        /// <param name="compilerId">The ID of the compiler to use.</param>
        /// <param name="compilerOptions">Command-line arguments for the compiler.</param>
        /// <returns>A tuple containing the generated short URL and any error message.</returns>
        public async Task<(string link, string? errorMessage)> GetShortLinkAsync(
    string language, string sourceCode, string compilerId, string compilerOptions)
        {
            if (string.IsNullOrWhiteSpace(language))
                return ("", "Parameter 'language' cannot be empty.");
            if (string.IsNullOrWhiteSpace(sourceCode))
                return ("", "Parameter 'source' cannot be empty.");
            if (string.IsNullOrWhiteSpace(compilerId))
                return ("", "Parameter 'compilerId' cannot be empty.");

            var root = new Root
            {
                Sessions = new List<Session>
                {
                    new Session
                    {
                        Id = 1,
                        Language = language,
                        Source = sourceCode,
                        Compilers = new List<Compiler>
                        {
                            new Compiler { Id = compilerId, Options = compilerOptions ?? string.Empty }
                        }
                    }
                }
            };

            var url = "https://godbolt.org/api/shortener";
            var json = JsonSerializer.Serialize(root);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                return ("", $"Network or HTTP error: {ex.Message}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return ("", $"API error ({(int)response.StatusCode}): {responseContent}");
            }

            ShortenerResult? shortenerResult = null;
            try
            {
                shortenerResult = JsonSerializer.Deserialize<ShortenerResult>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                return ("", $"Response parsing error: {ex.Message}");
            }

            if (shortenerResult == null || string.IsNullOrEmpty(shortenerResult.Url))
            {
                return ("", "Empty or invalid short link URL received");
            }

            return (shortenerResult.Url, null);
        }


    } // GodBoltService

    #region API Response DTOs

    /// <summary>
    /// Represents the top-level response from a Godbolt API compile/execute request.
    /// </summary>
    public class GodboltResponse
    {
        /// <summary>Gets or sets the exit code of the operation.</summary>
        public int Code { get; set; }

        /// <summary>Gets or sets a value indicating whether the code was executed. Present in exec-only responses.</summary>
        public bool DidExecute { get; set; }

        /// <summary>Gets or sets the standard output from execution.</summary>
        public List<StdText>? Stdout { get; set; }

        /// <summary>
        /// Gets or sets the standard error. In compile responses, it's compiler stderr. In exec-only responses, it's runtime stderr.
        /// </summary>
        public List<StdText>? Stderr { get; set; }

        /// <summary>Gets or sets the build result for the execution wrapper. Present in exec-only responses.</summary>
        public BuildResult? BuildResult { get; set; }

        /// <summary>Gets or sets the generated assembly code. Present in compile responses.</summary>
        public List<AsmLine>? Asm { get; set; }

        /// <summary>Gets or sets the nested result of an execution step. Present in compile-and-run responses.</summary>
        public GodboltResponse? ExecResult { get; set; }
    }

    /// <summary>
    /// Represents the result of the compilation of the execution wrapper.
    /// </summary>
    public class BuildResult
    {
        /// <summary>Gets or sets the exit code of the build.</summary>
        public int Code { get; set; }
        /// <summary>Gets or sets the standard output of the build.</summary>
        public List<StdText>? Stdout { get; set; }
        /// <summary>Gets or sets the standard error of the build.</summary>
        public List<StdText>? Stderr { get; set; }
    }

    /// <summary>
    /// Represents a line of text from stdout or stderr.
    /// </summary>
    public class StdText
    {
        /// <summary>Gets or sets the text content of the line.</summary>
        public string? Text { get; set; }
    }

    /// <summary>
    /// Represents a line of assembly code.
    /// </summary>
    public class AsmLine
    {
        /// <summary>Gets or sets the text content of the assembly line.</summary>
        public string? Text { get; set; }
    }
    #endregion

    #region API Request DTOs

    /// <summary>
    /// Represents the request body for a Godbolt compilation request.
    /// </summary>
    public class GodboltCompileRequest
    {
        /// <summary>Gets or sets the source code to be compiled.</summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        /// <summary>Gets or sets the language ID (e.g., "cpp", "csharp").</summary>
        [JsonPropertyName("lang")]
        public string? Lang { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to allow the Godbolt service to store the code for debugging purposes.
        /// </summary>
        [JsonPropertyName("allowStoreCodeDebug")]
        public bool AllowStoreCodeDebug { get; set; }

        /// <summary>
        /// Gets or sets the compilation options, including user arguments, compiler-specific options, and filters.
        /// </summary>
        [JsonPropertyName("options")]
        public GodboltOptions Options { get; set; } = new GodboltOptions();
    }

    public class GodboltOptions
    {
        [JsonPropertyName("userArguments")]
        /// <summary>Gets or sets the command-line arguments for the compiler.</summary>
        public string? UserArguments { get; set; }

        [JsonPropertyName("compilerOptions")]
        /// <summary>Gets or sets specific options for the compiler process.</summary>
        public CompilerOptions CompilerOptions { get; set; } = new CompilerOptions();

        [JsonPropertyName("filters")]
        /// <summary>Gets or sets filters to apply to the assembly output.</summary>
        public Filters Filters { get; set; } = new Filters();
    }

    /// <summary>
    /// Represents specific options for the compiler process itself.
    /// </summary>
    public class CompilerOptions
    {
        /// <summary>Gets or sets a value indicating whether to skip assembly generation.</summary>
        [JsonPropertyName("skipAsm")]
        public bool SkipAsm { get; set; }

        /// <summary>Gets or sets a value indicating whether to request execution of the compiled code.</summary>
        [JsonPropertyName("executorRequest")]
        public bool ExecutorRequest { get; set; }
    }

    /// <summary>
    /// Represents filters to control the appearance of the generated assembly code.
    /// </summary>
    public class Filters
    {
        /// <summary>Filter out everything but the binary code.</summary>
        [JsonPropertyName("binary")]
        public bool Binary { get; set; }

        /// <summary>Filter out everything but the binary code, but for object files.</summary>
        [JsonPropertyName("binaryObject")]
        public bool BinaryObject { get; set; }

        /// <summary>Filter out everything but comments.</summary>
        [JsonPropertyName("commentOnly")]
        public bool CommentOnly { get; set; }

        /// <summary>Demangle symbol names.</summary>
        [JsonPropertyName("demangle")]
        public bool Demangle { get; set; }

        /// <summary>Filter out all assembler directives.</summary>
        [JsonPropertyName("directives")]
        public bool Directives { get; set; }

        /// <summary>Execute the compiled output.</summary>
        [JsonPropertyName("execute")]
        public bool Execute { get; set; }

        /// <summary>Use Intel syntax for assembly.</summary>
        [JsonPropertyName("intel")]
        public bool Intel { get; set; }

        /// <summary>Filter out all labels.</summary>
        [JsonPropertyName("labels")]
        public bool Labels { get; set; }

        /// <summary>Include code from libraries.</summary>
        [JsonPropertyName("libraryCode")]
        public bool LibraryCode { get; set; }

        /// <summary>Trim whitespace from the output.</summary>
        [JsonPropertyName("trim")]
        public bool Trim { get; set; }

        /// <summary>Show debug calls in the assembly.</summary>
        [JsonPropertyName("debugCalls")]
        public bool DebugCalls { get; set; }
    }
    #endregion

    namespace Shortener
    {
        /// <summary>
        /// Represents a compiler configuration within a shortener session.
        /// </summary>
        public class Compiler
        {
            /// <summary>Gets or sets the compiler ID.</summary>
            [JsonPropertyName("id")]
            public string? Id { get; set; }
            /// <summary>Gets or sets the compiler options/flags.</summary>
            [JsonPropertyName("options")]
            public string? Options { get; set; }
        }

        /// <summary>
        /// Represents a single session in a shortener request, containing source code and compilers.
        /// </summary>
        public class Session
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("language")]
            public string? Language { get; set; }

            [JsonPropertyName("source")]
            public string? Source { get; set; }

            [JsonPropertyName("compilers")]
            public List<Compiler>? Compilers { get; set; }
        }

        /// <summary>
        /// The root object for a shortener API request.
        /// </summary>
        public class Root
        {
            /// <summary>Gets or sets the list of sessions to be included in the short link.</summary>
            [JsonPropertyName("sessions")]
            public List<Session>? Sessions { get; set; }
        }

        /// <summary>
        /// Represents the result from the shortener API.
        /// </summary>
        public class ShortenerResult
        {
            /// <summary>Gets or sets the generated short URL.</summary>
            [JsonPropertyName("url")]
            public string? Url { get; set; }
        }

    } // namespace Shortener


} // namespace CodeSnip.Services
