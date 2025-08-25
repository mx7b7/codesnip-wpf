using CodeSnip.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Net.Http;
using System.Text.RegularExpressions;

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
        private CompilerInfo? _selectedCompiler;

        [ObservableProperty]
        private string _flags = "";

        [ObservableProperty]
        private string _stdout = "";

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

        public CodeRunnerViewModel(string languageExtension, string code, Func<string> getLatestCode)
        {
            Compilers = _compilersSettings.GetCompilersByExtension(languageExtension);
            var defaultCompilerId = _compilersSettings.GetDefaultCompilerIdByExtension(languageExtension);
            SelectedCompiler = Compilers.FirstOrDefault(c => c.Id == defaultCompilerId) ?? Compilers.FirstOrDefault();
            Code = code;
            _getLatestCode = getLatestCode;
            Extension = languageExtension;
            _godboltService = new GodboltService(_httpClient);
                       
        }
        partial void OnSelectedCompilerChanged(CompilerInfo? value)
        {
            Flags = value?.Flags ?? "";
        }

        [RelayCommand]
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

        private async Task CompileSnippetAsync()
        {
            string? langId = _compilersSettings.GetLanguageIdByExtension(Extension); // godbolt languageId (c++, csharp ...)
            var (stdout, stderr, error) = await _godboltService.CompileAndRunAsync(
                Code, SelectedCompiler?.Id ?? "", langId ?? "", Flags, true);

            Stdout = string.IsNullOrEmpty(stdout) ? "" : stdout;
            ErrorText = RemoveAnsiCodes(stderr);

            if (!string.IsNullOrEmpty(error))
            {
                ErrorText = error;
                Stdout = "";
            }
        }

        [RelayCommand]
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
            var (link, error) = await _godboltService.GetShortLinkAsync(langId ?? "", Code, SelectedCompiler!.Id!, Flags);
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
    }
}
