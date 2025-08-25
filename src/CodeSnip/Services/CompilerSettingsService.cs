using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;

namespace CodeSnip.Services
{
    public class CompilerSettingsService
    {
        private readonly string _settingsFilePath = "compilers.json";
        public CompilerSettingsRoot? Settings { get; private set; }
        private readonly Uri compilersResource = new($"/CodeSnip;component/Resources/compilers.json", UriKind.Relative);
        private readonly JsonSerializerOptions _jsonOptions;

        public CompilerSettingsService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            LoadSettings();
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    Settings = JsonSerializer.Deserialize<CompilerSettingsRoot>(json, _jsonOptions) ?? new CompilerSettingsRoot();
                }
                // Create compilers.json from resource
                else
                {
                    var info = Application.GetResourceStream(compilersResource);
                    if (info != null)
                    {
                        using var stream = info.Stream;
                        using var reader = new StreamReader(stream);
                        string json = reader.ReadToEnd();
                        Settings = JsonSerializer.Deserialize<CompilerSettingsRoot>(json, _jsonOptions) ?? new CompilerSettingsRoot();
                    }
                    else
                    {
                        Settings = new CompilerSettingsRoot();
                    }
                    if (Settings.Languages == null)
                        Settings.Languages = new List<LanguageInfo>();

                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                Settings = new CompilerSettingsRoot { Languages = new List<LanguageInfo>() };
                MessageBox.Show($"Error loading settings:\n{ex.Message}");
            }
        }

        public void SaveSettings()
        {
            var json = JsonSerializer.Serialize(Settings, _jsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }

        // Retrieve a language by its Id
        public LanguageInfo? GetLanguageById(string? languageId)
        {
            if (string.IsNullOrWhiteSpace(languageId) || Settings?.Languages == null)
                return null;

            return Settings.Languages.FirstOrDefault(l => l.LanguageId != null && l.LanguageId.Equals(languageId, StringComparison.OrdinalIgnoreCase));
        }

        // Get default compiler
        public CompilerInfo? GetDefaultCompiler(LanguageInfo language)
        {
            if (language?.DefaultCompilerId == null || language.Compilers == null)
                return null;

            // Find the first compiler whose Id or LocalId matches the DefaultCompilerId
            var compiler = language.Compilers.FirstOrDefault(c =>
                string.Equals(c.Id, language.DefaultCompilerId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.LocalId, language.DefaultCompilerId, StringComparison.OrdinalIgnoreCase));

            return compiler;
        }

        public void SetDefaultCompiler(LanguageInfo language, string? compilerId)
        {
            if(language != null && compilerId != null)
            {
                language.DefaultCompilerId = compilerId;
                SaveSettings();
            }
        }

        public List<CompilerInfo> GetCompilersForLanguage(string languageId)
        {
            var language = GetLanguageById(languageId);
            return language?.Compilers ?? new List<CompilerInfo>();
        }
       
        public bool UpsertCompiler(string languageId, CompilerInfo compiler)
        {
            if (string.IsNullOrWhiteSpace(languageId) || compiler == null)
                return false;

            var language = GetLanguageById(languageId);
            if (language == null)
            {
                Debug.WriteLine($"Language '{languageId}' not found.");
                return false;
            }
            Debug.WriteLine($"Assigned new LocalId: {compiler.LocalId}");
            language.Compilers ??= new List<CompilerInfo>();
            Debug.WriteLine($"Assigned new LocalId: {compiler.LocalId}");
            // Check if there is already a LocalId
            if (string.IsNullOrWhiteSpace(compiler.LocalId))
            {
                compiler.LocalId = Guid.NewGuid().ToString();
                Debug.WriteLine($"Assigned new LocalId: {compiler.LocalId}");
            }

            // Try to find an existing compiler by LocalId
            var existing = language.Compilers.FirstOrDefault(c => c.LocalId == compiler.LocalId);

            if (existing == null)
            {
                // Check that there is not already a compiler with the same Godbolt Id (Id)
                var duplicateById = language.Compilers.Any(c => c.Id == compiler.Id);
                if (duplicateById)
                {
                    // Do not allow duplicates by Godbolt Id
                    Debug.WriteLine($"Duplicate compiler Id '{compiler.Id}' found. Not inserting.");
                    return false;
                }

                // Add new
                language.Compilers.Add(compiler);
                Debug.WriteLine($"Added new compiler '{compiler.Name}' with LocalId '{compiler.LocalId}'.");

            }
            else
            {
                // If old Id == DefaultCompilerId update DefaultCompilerId to new Id
                if (language.DefaultCompilerId == existing.Id)
                {
                    language.DefaultCompilerId = compiler.Id;
                }
                // Update existing
                existing.Id = compiler.Id;
                existing.Name = compiler.Name;
                existing.Flags = compiler.Flags;
                Debug.WriteLine($"Updated existing compiler '{existing.Name}' with LocalId '{existing.LocalId}'.");
            }

            SaveSettings();
            return true;
        }
        
        /// <summary>
        /// Deletes the compiler if it exists.
        /// </summary>
        public bool RemoveCompiler(string languageId, string compilerId)
        {
            if (string.IsNullOrWhiteSpace(languageId) || string.IsNullOrWhiteSpace(compilerId))
                return false;

            var language = GetLanguageById(languageId);
            if (language?.Compilers == null)
                return false;

            var compiler = language.Compilers.FirstOrDefault(c => c.Id == compilerId);
            if (compiler == null)
                return false;
            
            if(language.DefaultCompilerId == compilerId)
                language.DefaultCompilerId = string.Empty;

            language.Compilers.Remove(compiler);
            SaveSettings();
            return true;
        }

        /// <summary>
        /// Returns a list of compilers by extension (eg "cs").
        /// </summary>
        public List<CompilerInfo> GetCompilersByExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension) || Settings?.Languages == null)
                return [];
            // Direktno bez točke, bez ikakve obrade:
            var language = Settings.Languages
                .FirstOrDefault(l => l.Extension!.Equals(extension, StringComparison.OrdinalIgnoreCase));
            return language?.Compilers ?? [];
        }

        /// <summary>
        /// Returns the default CompilerId for the extension (eg "cs"), or null if none exists.
        /// </summary>
        public string? GetDefaultCompilerIdByExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension) || Settings?.Languages == null)
                return null;
            var language = Settings.Languages
                .FirstOrDefault(l => l.Extension!.Equals(extension, StringComparison.OrdinalIgnoreCase));
            return language?.DefaultCompilerId;
        }

        /// <summary>
        /// Returns languageID (godbolt language name)
        /// </summary>
        public string? GetLanguageIdByExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension) || Settings?.Languages == null)
                return null;
            var language = Settings.Languages
                .FirstOrDefault(l => l.Extension!.Equals(extension, StringComparison.OrdinalIgnoreCase));
            return language?.LanguageId;
        }



    }
}
