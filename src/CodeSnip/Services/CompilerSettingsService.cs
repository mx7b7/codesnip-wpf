using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;

namespace CodeSnip.Services
{
    /// <summary>
    /// Manages compiler settings for the Godbolt integration. This service is responsible for
    /// loading, saving, and providing access to the list of supported languages and their
    /// available compilers from a 'compilers.json' file.
    /// </summary>
    public class CompilerSettingsService
    {
        private readonly string _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "compilers.json");

        /// <summary>
        /// Gets the root object containing all compiler settings, including languages and their compilers.
        /// </summary>
        public CompilerSettingsRoot? Settings { get; private set; }

        private readonly Uri compilersResource = new($"/CodeSnip;component/Resources/compilers.json", UriKind.Relative);
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompilerSettingsService"/> class
        /// and loads the compiler settings.
        /// </summary>
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

        /// <summary>
        /// Loads compiler settings from 'compilers.json'. If the file doesn't exist or is invalid,
        /// it falls back to loading the default settings from embedded resources and creates the file.
        /// </summary>
        public void LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    Settings = JsonSerializer.Deserialize<CompilerSettingsRoot>(json, _jsonOptions);
                    if (Settings == null || Settings.Languages == null)
                    {
                        throw new JsonException("File is invalid or empty.");
                    }
                }
                catch (JsonException ex)
                {
                    MessageBox.Show($"Error parsing 'compilers.json': {ex.Message}\nLoading default compiler settings.", "Compiler Settings Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LoadSettingsFromResource(); // Load from embedded resource
                    SaveSettings();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An unexpected error occurred while loading 'compilers.json': {ex.Message}\nLoading default compiler settings.", "Compiler Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    LoadSettingsFromResource();
                    SaveSettings();
                }
            }
            else
            {
                // File doesn't exist, so create it from resource
                LoadSettingsFromResource();
                SaveSettings();
            }
        }

        /// <summary>
        /// Loads compiler settings from an embedded resource and deserializes them into the <see cref="Settings"/>
        /// property.
        /// </summary>
        /// <remarks>This method retrieves a JSON resource, deserializes it into a <see
        /// cref="CompilerSettingsRoot"/> object,  and assigns it to the <see cref="Settings"/> property. If the
        /// resource is empty or deserialization fails,  a default instance of <see cref="CompilerSettingsRoot"/> with
        /// an empty list of languages is assigned.</remarks>
        private void LoadSettingsFromResource()
        {
            var info = Application.GetResourceStream(compilersResource);
            using var stream = info!.Stream;
            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            Settings = JsonSerializer.Deserialize<CompilerSettingsRoot>(json, _jsonOptions) ?? new CompilerSettingsRoot { Languages = new List<LanguageInfo>() };
        }

        /// <summary>
        /// Saves the current compiler settings to the 'compilers.json' file.
        /// </summary>
        public void SaveSettings()
        {
            var json = JsonSerializer.Serialize(Settings, _jsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }

        /// <summary>
        /// Retrieves a language by its Godbolt language ID (e.g., "csharp", "cpp").
        /// </summary>
        /// <param name="languageId">The language ID to search for.</param>
        /// <returns>The <see cref="LanguageInfo"/> object if found; otherwise, null.</returns>
        public LanguageInfo? GetLanguageById(string? languageId)
        {
            if (string.IsNullOrWhiteSpace(languageId) || Settings?.Languages == null)
                return null;

            return Settings.Languages.FirstOrDefault(l => l.LanguageId != null && l.LanguageId.Equals(languageId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the default compiler for a given language.
        /// </summary>
        /// <param name="language">The language for which to find the default compiler.</param>
        /// <returns>The default <see cref="CompilerInfo"/> if set; otherwise, null.</returns>
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

        /// <summary>
        /// Sets the default compiler for a specific language.
        /// </summary>
        /// <param name="language">The language to update.</param>
        /// <param name="compilerId">The ID of the compiler to set as default.</param>
        public void SetDefaultCompiler(LanguageInfo language, string? compilerId)
        {
            if(language != null && compilerId != null)
            {
                language.DefaultCompilerId = compilerId;
                SaveSettings();
            }
        }

        /// <summary>
        /// Gets a list of all compilers available for a specific language ID.
        /// </summary>
        /// <param name="languageId">The Godbolt language ID.</param>
        /// <returns>A list of <see cref="CompilerInfo"/> objects, or an empty list if the language is not found.</returns>
        public List<CompilerInfo> GetCompilersForLanguage(string languageId)
        {
            var language = GetLanguageById(languageId);
            return language?.Compilers ?? new List<CompilerInfo>();
        }

        /// <summary>
        /// Adds a new compiler or updates an existing one for a specific language.
        /// </summary>
        /// <param name="languageId">The ID of the language to which the compiler belongs.</param>
        /// <param name="compiler">The <see cref="CompilerInfo"/> object to add or update.</param>
        /// <returns><c>true</c> if the operation was successful; otherwise, <c>false</c>.</returns>
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
        /// <param name="languageId">The ID of the language from which to remove the compiler.</param>
        /// <param name="compilerId">The ID of the compiler to remove.</param>
        /// <returns><c>true</c> if the compiler was successfully removed; otherwise, <c>false</c>.</returns>
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
        /// Gets a list of compilers for a given file extension.
        /// </summary>
        /// <param name="extension">The file extension (e.g., "cs", "cpp").</param>
        /// <returns>A list of <see cref="CompilerInfo"/> objects, or an empty list if not found.</returns>
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
        /// Gets the default compiler ID for a given file extension.
        /// </summary>
        /// <param name="extension">The file extension (e.g., "cs", "cpp").</param>
        /// <returns>The default compiler ID as a string, or null if not found.</returns>
        public string? GetDefaultCompilerIdByExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension) || Settings?.Languages == null)
                return null;
            var language = Settings.Languages
                .FirstOrDefault(l => l.Extension!.Equals(extension, StringComparison.OrdinalIgnoreCase));
            return language?.DefaultCompilerId;
        }

        /// <summary>
        /// Gets the Godbolt language ID (e.g., "csharp") for a given file extension.
        /// </summary>
        /// <param name="extension">The file extension (e.g., "cs", "cpp").</param>
        /// <returns>The Godbolt language ID as a string, or null if not found.</returns>
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
