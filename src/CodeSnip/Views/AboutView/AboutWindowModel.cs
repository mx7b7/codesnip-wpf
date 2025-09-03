using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Reflection;


namespace CodeSnip.Views.AboutView
{
    public partial class AboutWindowModel : ObservableObject
    {
        [ObservableProperty]
        private string? title;

        [ObservableProperty]
        private string? description;

        [ObservableProperty]
        private string? company;

        [ObservableProperty]
        private string? version;

        [ObservableProperty]
        private string? copyright;

        public static string Url => "https://github.com/mx7b7/codesnip-wpf/";

        public ObservableCollection<LibraryInfo> Libraries { get; } =
        [
            new() { Name = "AvalonEdit", Url = new Uri("https://github.com/icsharpcode/AvalonEdit") },
            new() { Name = "CommunityToolkit.Mvvm", Url = new Uri("https://github.com/CommunityToolkit/dotnet") },
            new() { Name = "Dapper", Url = new Uri("https://github.com/DapperLib/Dapper") },
            new() { Name = "MahApps.Metro", Url = new Uri("https://github.com/MahApps/MahApps.Metro") },
            new() { Name = "System.Data.SQLite.Core", Url = new Uri("https://system.data.sqlite.org/") }
        ];

        public ObservableCollection<ServicesInfo> Services { get; } =
        [
            new() { Name = "Compiler Explorer", Url = new Uri("https://godbolt.org/") }
        ];

        public ObservableCollection<ToolsInfo> Tools { get; } =
        [
            new() { Name = "black", Url = new Uri("https://black.readthedocs.io/en/stable/") },
            new() { Name = "clang-format", Url = new Uri("https://clang.llvm.org/docs/ClangFormat.html") },
            new() { Name = "csharpier", Url = new Uri("https://csharpier.com/") },
            new() { Name = "dfmt", Url = new Uri("https://github.com/dlang-community/dfmt") },
            new() { Name = "rustfmt", Url = new Uri("https://github.com/rust-lang/rustfmt") },
            new() { Name = "ruff", Url = new Uri("https://github.com/astral-sh/ruff") }
        ];

        public AboutWindowModel()
        {
            var assembly = Assembly.GetExecutingAssembly();
            Title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "Unknown";
            Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;
            Company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;
            Version = assembly.GetName().Version?.ToString() ?? "1.0.0";
            Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
        }
    }

    public class LibraryInfo
    {
        public string Name { get; set; } = string.Empty;
        public Uri Url { get; set; } = new Uri("https://example.com");
    }

    public class ToolsInfo
    {
        public string Name { get; set; } = string.Empty;
        public Uri Url { get; set; } = new Uri("https://example.com");
    }

    public class ServicesInfo
    {
        public string Name { get; set; } = string.Empty;
        public Uri Url { get; set; } = new Uri("https://example.com");
    }
}
