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
}
