using CodeSnip.Services.Shortener;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CodeSnip.Services
{
    public class GodboltService
    {
        private readonly HttpClient _httpClient;

        public GodboltService(HttpClient client)
        {
            _httpClient = client;
            // default Accept headers ...
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );
        }

        public async Task<(string Stdout, string Stderr, string? ErrorMessage)> CompileAndRunAsync(
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
                            Intel = true
                        }
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"https://godbolt.org/api/compiler/{compilerId}/compile", request);

                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    return ("", "", $"HTTP Error {response.StatusCode}: {errBody}");
                }
                var json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json))
                    return ("", "", "Godbolt API vratio je prazan odgovor.");

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var resp = JsonSerializer.Deserialize<GodboltResponse>(json, options);
                if (resp == null)
                    return ("", "", "Neočekivan format odgovora (nije moguće parsirati JSON).");

                var (stdout, stderr) = ParseOutputs(resp);
                return (stdout, stderr, null); // null means no error
            }
            catch (Exception ex)
            {
                return ("", "", $"Error:\n {ex.Message}");
            }
        }

        public static (string Stdout, string Stderr) ParseOutputs(GodboltResponse resp)
        {
            // User program output – prefer root stdout, fallback to buildResult.stdout
            string stdout = resp.Stdout != null && resp.Stdout.Count > 0
                ? string.Join(Environment.NewLine, resp.Stdout.Select(s => s.Text))
                : (
                    resp.BuildResult?.Stdout != null && resp.BuildResult.Stdout.Count > 0
                        ? string.Join(Environment.NewLine, resp.BuildResult.Stdout.Select(s => s.Text))
                        : ""
                  );

            // Error output – prefer buildResult.stderr, fallback to root.stderr
            string stderr =
                resp.BuildResult?.Stderr != null && resp.BuildResult.Stderr.Count > 0
                    ? string.Join(Environment.NewLine, resp.BuildResult.Stderr.Select(s => s.Text))
                    : (
                        resp.Stderr != null && resp.Stderr.Count > 0
                            ? string.Join(Environment.NewLine, resp.Stderr.Select(s => s.Text))
                            : ""
                      );

            return (stdout, stderr);
        }

        public static string RemoveAnsiCodes(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var ansiRegex = new Regex(@"\x1B\[[0-9;]*[mK]");
            return ansiRegex.Replace(input, "");
        }

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

    // Response
    public class GodboltResponse
    {
        public int Code { get; set; }
        public bool DidExecute { get; set; }
        public List<StdText>? Stdout { get; set; }
        public List<StdText>? Stderr { get; set; }
        public BuildResult? BuildResult { get; set; }
    }

    public class BuildResult
    {
        public int Code { get; set; }
        public List<StdText>? Stdout { get; set; }
        public List<StdText>? Stderr { get; set; }
    }

    public class StdText
    {
        public string? Text { get; set; }
    }

    // Request
    public class GodboltCompileRequest
    {
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("lang")]
        public string? Lang { get; set; }

        [JsonPropertyName("allowStoreCodeDebug")]
        public bool AllowStoreCodeDebug { get; set; }

        [JsonPropertyName("options")]
        public GodboltOptions Options { get; set; } = new GodboltOptions();
    }

    public class GodboltOptions
    {
        [JsonPropertyName("userArguments")]
        public string? UserArguments { get; set; }

        [JsonPropertyName("compilerOptions")]
        public CompilerOptions CompilerOptions { get; set; } = new CompilerOptions();

        [JsonPropertyName("filters")]
        public Filters Filters { get; set; } = new Filters();
    }

    public class CompilerOptions
    {
        [JsonPropertyName("skipAsm")]
        public bool SkipAsm { get; set; }

        [JsonPropertyName("executorRequest")]
        public bool ExecutorRequest { get; set; }
    }

    public class Filters
    {
        [JsonPropertyName("binary")]
        public bool Binary { get; set; }

        [JsonPropertyName("binaryObject")]
        public bool BinaryObject { get; set; }

        [JsonPropertyName("commentOnly")]
        public bool CommentOnly { get; set; }

        [JsonPropertyName("demangle")]
        public bool Demangle { get; set; }

        [JsonPropertyName("directives")]
        public bool Directives { get; set; }

        [JsonPropertyName("execute")]
        public bool Execute { get; set; }

        [JsonPropertyName("intel")]
        public bool Intel { get; set; }

        [JsonPropertyName("labels")]
        public bool Labels { get; set; }

        [JsonPropertyName("libraryCode")]
        public bool LibraryCode { get; set; }

        [JsonPropertyName("trim")]
        public bool Trim { get; set; }

        [JsonPropertyName("debugCalls")]
        public bool DebugCalls { get; set; }
    }

    namespace Shortener
    {
        public class Compiler
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }
            [JsonPropertyName("options")]
            public string? Options { get; set; }
        }

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

        public class Root
        {
            [JsonPropertyName("sessions")]
            public List<Session>? Sessions { get; set; }
        }

        public class ShortenerResult
        {
            [JsonPropertyName("url")]
            public string? Url { get; set; }
        }

    } // namespace Shortener


} // namespace CodeSnip.Services
