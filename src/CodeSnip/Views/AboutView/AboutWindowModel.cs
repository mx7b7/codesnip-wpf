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
        //
        public ObservableCollection<LibraryInfo> Libraries { get; } =
        [
            new() { Name = "AvalonEdit", Url = new Uri("https://github.com/icsharpcode/AvalonEdit"), LicenseName = "MIT License", LicenseUrl = new Uri("https://github.com/icsharpcode/AvalonEdit/blob/master/LICENSE") },
            new() { Name = "CommunityToolkit.Mvvm", Url = new Uri("https://github.com/CommunityToolkit/dotnet"),LicenseName = "MIT License", LicenseUrl = new Uri("https://github.com/CommunityToolkit/dotnet/blob/main/License.md") },
            new() { Name = "CSharpier.Core", Url = new Uri("https://csharpier.com/"), LicenseName = "MIT License", LicenseUrl = new Uri("https://github.com/belav/csharpier/blob/main/LICENSE") },
            new() { Name = "Dapper", Url = new Uri("https://github.com/DapperLib/Dapper"),LicenseName = "Apache 2.0 License", LicenseUrl = new Uri("https://github.com/DapperLib/Dapper/blob/main/License.txt") },
            new() { Name = "MahApps.Metro", Url = new Uri("https://github.com/MahApps/MahApps.Metro"), LicenseName = "MIT License", LicenseUrl = new Uri("https://github.com/MahApps/MahApps.Metro/blob/develop/LICENSE") },
            new() { Name = "System.Data.SQLite.Core", Url = new Uri("https://system.data.sqlite.org/"),LicenseName = "Public Domain License", LicenseUrl = new Uri("https://system.data.sqlite.org/home/doc/trunk/www/copyright.wiki") }
        ];

        public ObservableCollection<ServicesInfo> Services { get; } =
        [
            new() { Name = "Compiler Explorer", Url = new Uri("https://github.com/mattgodbolt/compiler-explorer"), LicenseName = "BSD 2-Clause", LicenseUrl = new Uri("https://github.com/compiler-explorer/compiler-explorer/blob/main/LICENSE") }
        ];

        public ObservableCollection<ToolsInfo> Tools { get; } =
        [
            new() { Name = "black", Url = new Uri("https://black.readthedocs.io/en/stable/"), LicenseName = "MIT License", LicenseUrl = new Uri("https://github.com/psf/black/blob/main/LICENSE") },
            new() { Name = "clang-format", Url = new Uri("https://clang.llvm.org/docs/ClangFormat.html"), LicenseName = "NCSA Open Source License", LicenseUrl = new Uri("https://llvm.org/docs/DeveloperPolicy.html#license") },
            new() { Name = "dfmt", Url = new Uri("https://github.com/dlang-community/dfmt"), LicenseName = "Boost Software License", LicenseUrl = new Uri("https://github.com/dlang-community/dfmt/blob/master/LICENSE.txt") },
            new() { Name = "rustfmt", Url = new Uri("https://github.com/rust-lang/rustfmt"), LicenseName = "MIT/Apache-2.0 License", LicenseUrl = new Uri("https://github.com/rust-lang/rustfmt/blob/master/LICENSE-MIT") },
            new() { Name = "ruff", Url = new Uri("https://github.com/astral-sh/ruff"), LicenseName = "MIT License", LicenseUrl = new Uri("https://github.com/astral-sh/ruff/blob/main/LICENSE") }
        ];

        public AboutWindowModel()
        {
            var assembly = Assembly.GetExecutingAssembly();
            Title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "Unknown";
            Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;
            Company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;
            var version = assembly.GetName().Version;
            Version = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
            Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
        }
    }

    public class LibraryInfo
    {
        public string? Name { get; set; } = string.Empty;
        public Uri? Url { get; set; } = new Uri("https://example.com");
        public string? LicenseName { get; set; } = string.Empty;
        public Uri? LicenseUrl { get; set; } = new Uri("https://opensource.org/licenses/MIT");
    }

    public class ToolsInfo
    {
        public string? Name { get; set; } = string.Empty;
        public Uri? Url { get; set; } = new Uri("https://example.com");
        public string? LicenseName { get; set; } = string.Empty;
        public Uri? LicenseUrl { get; set; } = new Uri("https://opensource.org/licenses/MIT");
    }

    public class ServicesInfo
    {
        public string? Name { get; set; } = string.Empty;
        public Uri? Url { get; set; } = new Uri("https://example.com");
        public string? LicenseName { get; set; } = string.Empty;
        public Uri? LicenseUrl { get; set; } = new Uri("https://opensource.org/licenses/MIT");
    }
}
