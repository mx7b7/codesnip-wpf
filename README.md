# CodeSnip

**CodeSnip** — a snippet manager & local code runner with multi-language interpreter support and Compiler Explorer integration.  

![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey)
![License: MIT](https://img.shields.io/badge/License-MIT-green)
![Status](https://img.shields.io/badge/Status-Active-success)

## ⬇️Download

[![GitHub Release](https://img.shields.io/github/v/release/mx7b7/codesnip-wpf?sort=semver&display_name=tag)](https://github.com/mx7b7/codesnip-wpf/releases/latest)

---

![Slideshow GIF](images/slideshow.gif)

---

## ✨ Features

- **Local storage using SQLite database** — all snippets are stored privately on your device.
- **Snippet organization**:
  - Hierarchy: *Language → Category → Snippet* (TreeView)
  - Filter by name or tags
  - Instant search
- **AvalonEdit integration**:
  - Syntax highlighting (light/dark mode)
  - Code folding
  - Toggle single-line and multi-line comments
- **Highlighting Editor**:
  - **Dual-Mode Editing**: A tabbed interface allows for both simple visual tweaks (colors, font styles) and advanced source code editing.
  - **Direct XSHD Source Editing**: Directly edit the raw `.xshd` XML for full control over rules, spans, and keywords.
  - **Preview on Demand**: Apply changes from the XSHD source to the main editor to see how they look before saving.
  - **Validation**: An integrated validation engine checks for XML errors and XSHD schema compliance.
- **Compiler Explorer (Godbolt) integration**:
  - Compile snippets without installing compilers locally
  - Support for 30+ languages
  - Add and edit available compilers
  - Select compiler and flags
  - View stdout/stderr output
  - **View assembly output** with syntax highlighting for supported languages
    >**Note:** Some compilers may generate a very large amount of assembly code even from a small number of source lines.
  - Generate shareable shortlinks to [Compiler Explorer](https://godbolt.org/)
 - **Local Code Execution**:
   - Run scripts for languages like F#, PowerShell, Python, PHP, Perl, Lua, Ruby, Node.js, and Java (via `JShell`) directly using local interpreters.
     >**Note:** If an interpreter is not in system's PATH, you can place its portable version (e.g., `lua.exe`, `node.exe`) directly into the `Tools/Interpreters` directory within the application's installation folder.
   - **C# Scripting**: CodeSnip can also execute C# code locally using a custom wrapper named `csrunner.exe`. This tool is not included with the application. To enable this feature, create a .NET console project using the example code from this [Gist](https://gist.github.com/mx7b7/90013b77c1d0bcfb6b9e77399f62e409), build it, and place the compiled `csrunner.exe` with its dependencies into the `Tools/Interpreters` folder.
- **UI/UX**:
  - Modern Metro interface
  - Flyout panels for additional windows (settings, editors, actions, etc.)
  - Automatic loading of theme and syntax definitions
- **Export & Sharing**:
  - **Copy As**: Copy selected code as Markdown, HTML, BBCode, Base64, a JSON string, or as a **Bitmap Image**.
  - **Export to File**: Save snippets as HTML, a **PNG Image**, or in their original language format.


---

## 📚 Libraries

- [AvalonEdit](https://github.com/icsharpcode/AvalonEdit)
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- [Dapper](https://github.com/DapperLib/Dapper)
- [MahApps.Metro](https://github.com/MahApps/MahApps.Metro)
- [System.Data.SQLite](https://system.data.sqlite.org/)

---

## 🧹 Code Formatters

CodeSnip integrates various code formatters, which are executed in different ways. Use the legend below to understand how each formatter is run.

### 🏷️ Legend
*   ![Local](https://img.shields.io/badge/Local-green) — Run executable from the `Tools` directory.
*   ![System](https://img.shields.io/badge/System-blue) — Run executable from the system PATH. Used if the formatter is not available in the Tools directory.
*   ![Built-in](https://img.shields.io/badge/Built--in-yellow) — The formatter is a built-in library and does not require any external installation.


### Supported Formatters

- [autopep8](https://pypi.org/project/autopep8) – Python code formatting. ![System](https://img.shields.io/badge/System-blue)
- [black](https://black.readthedocs.io/en/stable) – Python code formatting. ![System](https://img.shields.io/badge/System-blue)
- [clang-format](https://clang.llvm.org/docs/ClangFormat.html) – Formats C, C++, C#, Java, and more. ![Local](https://img.shields.io/badge/Local-green) ![System](https://img.shields.io/badge/System-blue)
- [csharpier](https://csharpier.com/) – C#, XML code formatting. ![Built-in](https://img.shields.io/badge/Built--in-yellow)
- [dfmt](https://github.com/dlang-community/dfmt) – D code formatting. ![Local](https://img.shields.io/badge/Local-green) ![System](https://img.shields.io/badge/System-blue)
- [gofmt](https://pkg.go.dev/cmd/gofmt) – Go code formatting. ![Local](https://img.shields.io/badge/Local-green) ![System](https://img.shields.io/badge/System-blue)
- [fantomas](https://github.com/fsprojects/fantomas) – F# code formatting. ![System](https://img.shields.io/badge/System-blue)
- [pasfmt](https://github.com/integrated-application-development/pasfmt) – Pascal/Delphi code formatting. ![Local](https://img.shields.io/badge/Local-green) ![System](https://img.shields.io/badge/System-blue)
- [prettier](https://prettier.io/) - Formats JavaScript, TypeScript, JSX, HTML, CSS, JSON, Markdown, and more. ![Local](https://img.shields.io/badge/Local-green) ![System](https://img.shields.io/badge/System-blue)
- [rustfmt](https://github.com/rust-lang/rustfmt) – Rust code formatting. ![Local](https://img.shields.io/badge/Local-green) ![System](https://img.shields.io/badge/System-blue)
- [ruff](https://github.com/astral-sh/ruff) – Python code formatting. ![Local](https://img.shields.io/badge/Local-green) ![System](https://img.shields.io/badge/System-blue)
- [stylua](https://github.com/JohnnyMorganz/StyLua) – Lua code formatting. ![Local](https://img.shields.io/badge/Local-green) ![System](https://img.shields.io/badge/System-blue)

---

## ⚙️ Build

The project source code is located in the `src` directory. To build and run CodeSnip, you'll need the **.NET 8 SDK** or later.

### For Development

1.  Clone the repository:
    ```bash
    git clone https://github.com/mx7b7/codesnip-wpf.git
    ```
2.  Navigate to the project's root directory:
    ```bash
    cd codesnip-wpf
    ```
3.  Build the project from the root directory:

    - For **Debug** build:
      ```bash
      dotnet build src/CodeSnip -c Debug
      ```

    - For **Release** build:
      ```bash
      dotnet build src/CodeSnip -c Release
      ```
4.  Run the application from your IDE (like Visual Studio or VS Code) or using the CLI:
    ```bash
    dotnet run --project src/CodeSnip
    ```
    On first launch, the application will automatically create the database in the executable's directory, load languages, and import initial categories/snippets.

### Creating a Self-Contained Release Package

A convenient batch script is included in the root directory to create a portable, self-contained, single-file executable for Windows (x64).

1.  Make sure you are in the root directory of the project.
2.  Run the `build.bat` script:
    ```cmd
    .\build.bat
    ```
3.  The script will build the project from the `src` folder and create a `release` folder in the root directory, containing the `CodeSnip.exe` file and all necessary components. You can copy this `release` folder anywhere.

---


## 📜 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

