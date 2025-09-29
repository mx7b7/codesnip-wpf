using CodeSnip.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
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

        public CodeRunnerViewModel(string languageExtension, string code, Func<string> getLatestCode)
        {
            Extension = languageExtension;// set before triggering OnSelectedCompilerChanged beacause it uses it
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

        
    }
}
