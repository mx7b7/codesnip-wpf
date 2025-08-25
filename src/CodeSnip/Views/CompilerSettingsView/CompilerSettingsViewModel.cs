using CodeSnip.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace CodeSnip.Views.CompilerSettingsView
{
    public partial class CompilerSettingsViewModel : ObservableObject
    {
        private readonly CompilerSettingsService _manager;

        [ObservableProperty]
        private ObservableCollection<LanguageInfo> _languages = [];

        [ObservableProperty]
        private LanguageInfo? _selectedLanguage;


        [ObservableProperty]
        private ObservableCollection<CompilerInfo> _compilers = [];

        [ObservableProperty]
        private CompilerInfo? _selectedCompiler;

        [ObservableProperty]
        private bool _isAddingLanguage;

        [ObservableProperty]
        private bool _canSetDefaultCompiler;

        [ObservableProperty]
        private string _compilersLink = string.Empty;

        [ObservableProperty]
        private string _linkText = string.Empty;

        // edit/insert
        [ObservableProperty]
        private string _compilerLocalId = string.Empty;
        [ObservableProperty]
        private string _compilerId = string.Empty;
        [ObservableProperty]
        private string _compilerName = string.Empty;
        [ObservableProperty]
        private string _compilerFlags = string.Empty;

        [ObservableProperty]
        private bool _canSaveCompiler;

        [ObservableProperty]
        private bool _isHelpOpen;

        [ObservableProperty]
        private string _helpText = "";

        public CompilerSettingsViewModel()
        {
            _manager = new CompilerSettingsService();
            LoadLanguages();
        }

        [RelayCommand]
        private void ShowHelp()
        {
            IsHelpOpen = !IsHelpOpen;
        }

        [RelayCommand]
        private void CopyLink()
        {
            if (!string.IsNullOrEmpty(CompilersLink))
            {
                Clipboard.SetText(CompilersLink);
            }
        }

        partial void OnSelectedLanguageChanged(LanguageInfo? value)
        {
            if (value != null)
            {
                Compilers = new ObservableCollection<CompilerInfo>(
                    _manager.GetCompilersForLanguage(value.LanguageId ?? "")
                );
                if (Compilers.Count > 0)
                {
                    SelectedCompiler = _manager.GetDefaultCompiler(value);//Compilers.First();
                    CanSaveCompiler = (Compilers.Count > 0 && SelectedCompiler != null);
                }
                else
                {
                    CanSaveCompiler = false;
                }
                CompilersLink = $"https://godbolt.org/api/compilers/{value.LanguageId}";
                LinkText = $"Get more compilers for {value.LanguageName}";
                HelpText = $"Enter a CompilerId matching a 'Compiler Name' from the list of Godbolt compilers for {value.LanguageName}";
                Debug.WriteLine(LinkText);
                Debug.WriteLine(CompilersLink);
            }
            else
            {
                Compilers.Clear();
                SelectedCompiler = null;
            }
        }

        partial void OnSelectedCompilerChanged(CompilerInfo? value)
        {
            if (value != null)
            {
                CompilerLocalId = value.LocalId ?? "";
                CompilerId = value.Id ?? "";
                CompilerName = value.Name ?? "";
                CompilerFlags = value.Flags ?? "";
                CanSetDefaultCompiler = SelectedLanguage != null &&
                               !string.Equals(value.Id, SelectedLanguage.DefaultCompilerId, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                ClearCompilerFields();
                CanSetDefaultCompiler = false;
            }
        }

        private void ClearCompilerFields()
        {
            CompilerLocalId = string.Empty;
            CompilerId = string.Empty;
            CompilerName = string.Empty;
            CompilerFlags = string.Empty;
        }

        private void LoadLanguages()
        {
            Languages = new ObservableCollection<LanguageInfo>(_manager.Settings!.Languages!);
            if (Languages.Any())
            {
                SelectedLanguage = Languages.FirstOrDefault();
            }
        }

        [RelayCommand]
        private void ToggleAddCompiler()
        {
            if (IsAddingLanguage)
            {
                // Cancel
                IsAddingLanguage = false;
                if (Compilers.Count > 0)
                {
                    SelectedCompiler = null; // otherwise it will not trigger OnSelectedCompilerChanged if there is one compiler
                    SelectedCompiler = Compilers.First();
                }
                else
                {
                    SelectedCompiler = null;
                    CanSaveCompiler = false;
                }
            }
            else
            {
                // Enter Add Mode
                IsAddingLanguage = true;
                ClearCompilerFields();
                CanSaveCompiler = true;
            }
        }

        [RelayCommand]
        private void SaveCompiler()
        {
            if (SelectedLanguage == null) return;

            var compiler = new CompilerInfo
            {
                LocalId = CompilerLocalId,
                Id = CompilerId,
                Name = CompilerName,
                Flags = CompilerFlags
            };

            if (string.IsNullOrWhiteSpace(CompilerId) || string.IsNullOrWhiteSpace(CompilerName)) return;

            _ = _manager.UpsertCompiler(SelectedLanguage.LanguageId!, compiler);

            
            if (IsAddingLanguage)
            {
                if (!Compilers.Any(c => c.Id == compiler.Id))
                    Compilers.Add(compiler);
                SelectedCompiler = compiler;
                IsAddingLanguage = false;
            }
            else
            {
                var idx = Compilers.IndexOf(SelectedCompiler!);
                if (idx >= 0)
                {
                    Compilers[idx] = compiler;
                    SelectedCompiler = compiler;
                }
            }
        }

        [RelayCommand]
        private void DeleteCompiler()
        {
            if (SelectedLanguage == null || SelectedCompiler == null) return;

            if (_manager.RemoveCompiler(SelectedLanguage.LanguageId ?? "", SelectedCompiler.Id ?? ""))
            {
                Compilers.Remove(SelectedCompiler);
                if (Compilers.Count > 0)
                {
                    SelectedCompiler = null; // otherwise it will not trigger OnSelectedCompilerChanged if there is one compiler
                    SelectedCompiler = Compilers.First();
                }
                else
                {
                    SelectedCompiler = null;
                    CanSaveCompiler = false;
                }
            }
            else
            {
                // Message notification
            }
        }

        [RelayCommand]
        private void SetDefault()
        {
            if (SelectedLanguage != null && SelectedCompiler != null)
            {
                _manager.SetDefaultCompiler(SelectedLanguage, SelectedCompiler.Id);
                CanSetDefaultCompiler = false;
            }
        }

    }
}
