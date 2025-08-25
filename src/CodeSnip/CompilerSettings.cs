using System.Text.Json.Serialization;
namespace CodeSnip
{

    public class CompilerInfo
    {
        [JsonPropertyName("LocalId")]
        public string? LocalId { get; set; }

        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Flags")]
        public string? Flags { get; set; }
    }

    public class LanguageInfo
    {
        [JsonPropertyName("LanguageName")]
        public string? LanguageName { get; set; }

        [JsonPropertyName("LanguageId")]
        public string? LanguageId { get; set; }

        [JsonPropertyName("Extension")]
        public string? Extension { get; set; }

        [JsonPropertyName("DefaultCompilerId")]
        public string? DefaultCompilerId { get; set; }

        [JsonPropertyName("Compilers")]
        public List<CompilerInfo>? Compilers { get; set; }
    }

    public class CompilerSettingsRoot
    {
        [JsonPropertyName("Languages")]
        public List<LanguageInfo>? Languages { get; set; }
    }
}



