using CodeSnip.Views.LanguageCategoryView;
using CodeSnip.Views.SnippetView;
using Dapper;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace CodeSnip.Services
{
    /// <summary>
    /// Handles all interactions with the SQLite database, including creating the database,
    /// seeding initial data, and performing CRUD operations for snippets, languages, and categories.
    /// </summary>
    public class DatabaseService
    {
        private readonly string _dbPath = "snippets.sqlite";

        /// <summary>
        /// Creates and returns a new SQLite database connection.
        /// </summary>
        /// <returns>An open <see cref="IDbConnection"/> to the SQLite database.</returns>
        public IDbConnection CreateConnection()
        {
            return new SQLiteConnection($"Data Source={_dbPath};foreign keys=true;");
        }

        /// <summary>
        /// Initializes the database if the file does not exist. This method creates the database schema
        /// and seeds it with default languages, categories, and a few example snippets.
        /// </summary>
        /// <param name="dbSchema">The DDL and DML script to execute for database creation and seeding.</param>
        public void InitializeDatabaseIfNeeded(string dbSchema = ddl)
        {
            if (!File.Exists(_dbPath))
            {
                using var connection = CreateConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();

                // 1. Create tables + insert languages + categories
                connection.Execute(dbSchema, transaction: transaction);

                // 2. Insert initial snippets

                // C#
                var csCategoryId = connection.ExecuteScalar<int>(
                    "SELECT ID FROM Categories WHERE LanguageId = @langId AND Name = 'Basic Syntax'",
                    new { langId = 7 }, transaction);

                connection.Execute(@"
            INSERT INTO Snippets (CategoryId, Title, Code, Description, Tag) 
            VALUES (@CategoryId, @Title, @Code, @Description, @Tag)",
                    new
                    {
                        CategoryId = csCategoryId,
                        Title = "Hello World",
                        Code = @"using System;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(""Hello, World!"");
    }
}",
                        Description = "Hello World program in C#",
                        Tag = "hello,world"
                    }, transaction);

                // C++
                var cppCategoryId = connection.ExecuteScalar<int>(
                    "SELECT ID FROM Categories WHERE LanguageId = @langId AND Name = 'Basic Syntax'",
                    new { langId = 6 }, transaction);

                connection.Execute(@"
            INSERT INTO Snippets (CategoryId, Title, Code, Description, Tag) 
            VALUES (@CategoryId, @Title, @Code, @Description, @Tag)",
                    new
                    {
                        CategoryId = cppCategoryId,
                        Title = "Hello World",
                        Code = @"#include <iostream>

int main()
{
    std::cout << ""Hello, World!"" << std::endl;
    return 0;
}",
                        Description = "Hello World program in C++",
                        Tag = "hello,world"
                    }, transaction);

                // D
                var dCategoryId = connection.ExecuteScalar<int>(
                    "SELECT ID FROM Categories WHERE LanguageId = @langId AND Name = 'Basic Syntax'",
                    new { langId = 9 }, transaction);

                connection.Execute(@"
            INSERT INTO Snippets (CategoryId, Title, Code, Description, Tag) 
            VALUES (@CategoryId, @Title, @Code, @Description, @Tag)",
                    new
                    {
                        CategoryId = dCategoryId,
                        Title = "Hello World",
                        Code = @"import std.stdio;

void main()
{
    writeln(""Hello, World!"");
}",
                        Description = "Hello World program in D",
                        Tag = "hello,world"
                    }, transaction);

                // Python
                var pyCategoryId = connection.ExecuteScalar<int>(
                    "SELECT ID FROM Categories WHERE LanguageId = @langId AND Name = 'Basic Syntax'",
                    new { langId = 23 }, transaction);

                connection.Execute(@"
            INSERT INTO Snippets (CategoryId, Title, Code, Description, Tag) 
            VALUES (@CategoryId, @Title, @Code, @Description, @Tag)",
                    new
                    {
                        CategoryId = pyCategoryId,
                        Title = "Hello World",
                        Code = @"def main():
    print(""Hello, World!"")
    
if __name__ == '__main__':
    main()",
                        Description = "Hello World program in Python",
                        Tag = "hello,world"
                    }, transaction);

                transaction.Commit();
            }
        }

        /// <summary>
        /// Retrieves all languages, their categories, and associated snippets from the database.
        /// The snippet code is not loaded initially to support lazy loading.
        /// </summary>
        /// <returns>An enumerable collection of <see cref="Language"/> objects, structured hierarchically.</returns>
        public IEnumerable<Language> GetSnippets()
        {
            try
            {
                using var conn = CreateConnection();

                var sql = @"
 SELECT 
    L.ID AS LanguageID,
    L.Code AS LanguageCode,
    L.Name AS LanguageName,

    C.ID AS CategoryID,
    C.LanguageId AS CategoryLanguageId,
    C.Name AS CategoryName,

    S.ID AS SnippetID,
    S.CategoryId AS SnippetCategoryId,
    S.Title AS SnippetTitle,
    -- S.Code AS SnippetCode, -- Lazy load this
    S.Description AS SnippetDescription,
    S.Tag AS SnippetTag
FROM Languages L
LEFT JOIN Categories C ON C.LanguageId = L.ID
LEFT JOIN Snippets S ON S.CategoryId = C.ID
ORDER BY L.Name, C.Name, S.Title";

                var lookup = new Dictionary<int, Language>();
                //var result = conn.Query(sql).ToList();
                var result = conn.Query<dynamic>(sql).ToList();

                foreach (var row in result)
                {
                    // Find or create a language
                    int langId = (int)row.LanguageID;
                    if (!lookup.TryGetValue(langId, out var language))
                    {
                        language = new Language
                        {
                            Id = langId,
                            Code = (string)row.LanguageCode,
                            Name = (string)row.LanguageName,
                            Categories = new ObservableCollection<Category>() // Collection initialization
                        };
                        lookup.Add(langId, language);
                    }
                    // Find or create a category
                    if (row.CategoryID != null)
                    {
                        int catId = (int)row.CategoryID;
                        var category = language.Categories.FirstOrDefault(c => c.Id == catId);
                        if (category == null)
                        {
                            category = new Category
                            {
                                Id = catId,
                                LanguageId = (int)row.CategoryLanguageId,
                                Name = (string)row.CategoryName,
                                Language = language,
                                Snippets = new ObservableCollection<Snippet>() // Collection initialization
                            };
                            language.Categories.Add(category);
                        }
                        // Find or create a snippet
                        if (row.SnippetID != null)
                        {
                            var snippet = new Snippet
                            {
                                Id = (int)row.SnippetID,
                                CategoryId = (int)row.SnippetCategoryId,
                                Title = (string)row.SnippetTitle,
                                //Code = (string)row.SnippetCode,
                                Code = string.Empty, // Initially empty
                                Description = (string)row.SnippetDescription,
                                Tag = (string)row.SnippetTag,
                                Category = category
                            };
                            category.Snippets.Add(snippet);
                        }
                    }
                }
                return lookup.Values;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FATAL] Failed to load snippets from database: {ex}");
                return Enumerable.Empty<Language>(); // Return an empty list on failure
            }
        }

        /// <summary>
        /// Lazily loads the code for a specific snippet from the database.
        /// </summary>
        /// <param name="snippetId">The ID of the snippet to retrieve code for.</param>
        /// <returns>The code content of the snippet as a string.</returns>
        public string GetSnippetCode(int snippetId)
        {
            using var conn = CreateConnection();
            return conn.ExecuteScalar<string>(
                "SELECT Code FROM Snippets WHERE ID = @Id",
                new { Id = snippetId }) ?? string.Empty;
        }

        /// <summary>
        /// Saves a snippet to the database. Performs an INSERT for a new snippet (Id = 0)
        /// or an UPDATE for an existing one.
        /// </summary>
        /// <param name="snippet">The <see cref="Snippet"/> object to save.</param>
        /// <returns>The saved <see cref="Snippet"/> object, updated with its new ID if it was an insert.</returns>
        public Snippet SaveSnippet(Snippet snippet)
        {
            using var conn = CreateConnection();
            if (snippet.Id == 0)
            {
                // INSERT
                var id = conn.ExecuteScalar<int>(
                    "INSERT INTO Snippets (Title, Code, Description, Tag, CategoryId) VALUES (@Title, @Code, @Description, @Tag, @CategoryId); SELECT last_insert_rowid();",
                    snippet);

                snippet.Id = id;
            }
            else
            {
                // UPDATE
                conn.Execute(
                    "UPDATE Snippets SET Title = @Title, Code = @Code, Description = @Description, Tag = @Tag, CategoryId = @CategoryId WHERE Id = @Id",
                    snippet);
            }
            return snippet;
        }

        /// <summary>
        /// Updates only the code content of a specific snippet in the database.
        /// </summary>
        /// <param name="id">The ID of the snippet to update.</param>
        /// <param name="code">The new code content.</param>
        public void UpdateSnippetCode(int id, string code)
        {
            using var conn = CreateConnection();
            conn.Execute(
                "UPDATE Snippets SET Code = @Code WHERE Id = @Id",
                new { Id = id, Code = code });
        }

        /// <summary>
        /// Deletes a snippet from the database.
        /// </summary>
        /// <param name="id">The ID of the snippet to delete.</param>
        public void DeleteSnippet(int id)
        {
            using var conn = CreateConnection();
            conn.Execute("DELETE FROM Snippets WHERE Id = @Id", new { Id = id });
        }

        /// <summary>
        /// Retrieves all languages and their associated categories from the database, without loading snippets.
        /// </summary>
        /// <returns>An enumerable collection of <see cref="Language"/> objects, each containing its list of <see cref="Category"/> objects.</returns>
        public IEnumerable<Language> GetLanguagesWithCategories()
        {
            using var conn = CreateConnection();

            var sql = @"
        SELECT 
            L.ID AS LanguageID,
            L.Code AS LanguageCode,
            L.Name AS LanguageName,
            C.ID AS CategoryID,
            C.Name AS CategoryName,
            C.LanguageId AS CategoryLanguageId
        FROM Languages L
        LEFT JOIN Categories C ON C.LanguageId = L.ID
        ORDER BY L.Name, C.Name";

            var lookup = new Dictionary<int, Language>();

            var result = conn.Query<dynamic>(sql);

            foreach (var row in result)
            {
                int langId = (int)row.LanguageID;

                if (!lookup.TryGetValue(langId, out var language))
                {
                    language = new Language
                    {
                        Id = langId,
                        Code = (string)row.LanguageCode,
                        Name = (string)row.LanguageName,
                        Categories = new ObservableCollection<Category>()
                    };
                    lookup.Add(langId, language);
                }

                if (row.CategoryID != null)
                {
                    var category = new Category
                    {
                        Id = (int)row.CategoryID,
                        Name = (string)row.CategoryName,
                        LanguageId = (int)row.CategoryLanguageId,
                        Language = language
                    };
                    language.Categories.Add(category);
                }
            }

            return lookup.Values;
        }

        /// <summary>
        /// Saves a language to the database. Performs an INSERT for a new language (Id = 0)
        /// or an UPDATE for an existing one.
        /// </summary>
        /// <param name="language">The <see cref="Language"/> object to save.</param>
        /// <returns>The saved <see cref="Language"/> object, updated with its new ID if it was an insert.</returns>
        public Language SaveLanguage(Language language)
        {
            using var conn = CreateConnection();
            if (language.Id == 0)
            {
                var id = conn.ExecuteScalar<int>(
                    "INSERT INTO Languages (Code, Name) VALUES (@Code, @Name); SELECT last_insert_rowid();",
                    language);
                language.Id = id;
            }
            else
            {
                conn.Execute(
                    "UPDATE Languages SET Code = @Code, Name = @Name WHERE ID = @Id",
                    language);
            }
            return language;
        }

        /// <summary>
        /// Saves a category to the database. Performs an INSERT for a new category (Id = 0)
        /// or an UPDATE for an existing one.
        /// </summary>
        /// <param name="category">The <see cref="Category"/> object to save.</param>
        /// <returns>The saved <see cref="Category"/> object, updated with its new ID if it was an insert.</returns>
        public Category SaveCategory(Category category)
        {
            using var conn = CreateConnection();
            if (category.Id == 0)
            {
                var id = conn.ExecuteScalar<int>("INSERT INTO Categories (LanguageId, Name) VALUES (@LanguageId, @Name); SELECT last_insert_rowid();", category);
                category.Id = id;
            }
            else
            {
                conn.Execute("UPDATE Categories SET Name = @Name WHERE ID = @Id AND LanguageId = @LanguageId", category);
            }
            return category;
        }

        /// <summary>
        /// Deletes a language from the database. Note: This may fail if the language is referenced
        /// by categories, due to foreign key constraints.
        /// </summary>
        /// <param name="id">The ID of the language to delete.</param>
        public void DeleteLanguage(int id)
        {
            using var conn = CreateConnection();
            conn.Execute("DELETE FROM Languages WHERE ID = @Id", new { Id = id });
        }

        /// <summary>
        /// Deletes a category from the database. Note: This may fail if the category is referenced
        /// by snippets, due to foreign key constraints.
        /// </summary>
        /// <param name="id">The ID of the category to delete.</param>
        public void DeleteCategory(int id)
        {
            using var conn = CreateConnection();
            conn.Execute("DELETE FROM Categories WHERE ID = @Id", new { Id = id });
        }

        /// <summary>
        /// Counts the number of snippets within a specific category.
        /// </summary>
        /// <param name="categoryId">The ID of the category.</param>
        /// <returns>The total number of snippets in the category.</returns>
        public int CountSnippetsInCategory(int categoryId)
        {
            using var conn = CreateConnection();
            return conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Snippets WHERE CategoryId = @CategoryId", new { CategoryId = categoryId });
        }

        /// <summary>
        /// Counts the total number of snippets across all categories for a specific language.
        /// </summary>
        /// <param name="languageId">The ID of the language.</param>
        /// <returns>The total number of snippets in the language.</returns>
        public int CountSnippetsInLanguage(int languageId)
        {
            using var conn = CreateConnection();
            return conn.ExecuteScalar<int>(@"
                SELECT COUNT(*) 
                FROM Snippets s
                JOIN Categories c ON s.CategoryId = c.ID
                WHERE c.LanguageId = @LanguageId", new { LanguageId = languageId });
        }

        /// <summary>
        /// Deletes a category and all of its associated snippets from the database.
        /// This method bypasses the 'ON DELETE RESTRICT' foreign key constraint by first
        /// deleting the child snippets and then the parent category within a transaction.
        /// </summary>
        /// <param name="categoryId">The ID of the category to delete.</param>
        public void ForceDeleteCategory(int categoryId)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var transaction = conn.BeginTransaction();
            try
            {
                // Delete all snippete in category
                conn.Execute("DELETE FROM Snippets WHERE CategoryId = @CategoryId", new { CategoryId = categoryId }, transaction);
                // Delete the category itself
                conn.Execute("DELETE FROM Categories WHERE ID = @Id", new { Id = categoryId }, transaction);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Deletes a language, all of its categories, and all snippets contained within those categories.
        /// This method bypasses the 'ON DELETE RESTRICT' foreign key constraint by performing a
        /// hierarchical deletion (snippets -> categories -> language) within a transaction.
        /// </summary>
        /// <param name="languageId">The ID of the language to delete.</param>
        public void ForceDeleteLanguage(int languageId)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var transaction = conn.BeginTransaction();
            try
            {
                // Delete all snippets in categories of this language
                conn.Execute("DELETE FROM Snippets WHERE CategoryId IN (SELECT ID FROM Categories WHERE LanguageId = @LanguageId)", new { LanguageId = languageId }, transaction);
                //  Delete all categories of this language
                conn.Execute("DELETE FROM Categories WHERE LanguageId = @LanguageId", new { LanguageId = languageId }, transaction);
                // Delete the language itself
                conn.Execute("DELETE FROM Languages WHERE ID = @Id", new { Id = languageId }, transaction);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Runs a database integrity check.
        /// </summary>
        /// <returns>A task that resolves to <c>true</c> if the check passes, otherwise <c>false</c>.</returns>
        public async Task<bool> RunIntegrityCheckAsync()
        {
            try
            {
                using var conn = CreateConnection();
                var result = await conn.QueryAsync<string>("PRAGMA integrity_check;");
                return result.Count() == 1 && result.First() == "ok";
            }
            catch (Exception ex)
            {
                await DialogService.Instance.ShowMessageAsync("Integrity Check Failed", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Runs the VACUUM command to rebuild the database file, repacking it into a minimal amount of disk space.
        /// </summary>
        /// <returns>A task that resolves to <c>true</c> if the operation succeeds, otherwise <c>false</c>.</returns>
        public async Task<bool> RunVacuumAsync()
        {
            try
            {
                using var conn = CreateConnection();
                await conn.ExecuteAsync("VACUUM;");
                return true;
            }
            catch (Exception ex)
            {
                await DialogService.Instance.ShowMessageAsync("Vacuum Failed", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Runs the REINDEX command to rebuild all indices in the database.
        /// </summary>
        /// <returns>A task that resolves to <c>true</c> if the operation succeeds, otherwise <c>false</c>.</returns>
        public async Task<bool> RunReindexAsync()
        {
            try
            {
                using var conn = CreateConnection();
                await conn.ExecuteAsync("REINDEX;");
                return true;
            }
            catch (Exception ex)
            {
                await DialogService.Instance.ShowMessageAsync("Reindex Failed", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Checks the database for fragmentation to determine if a VACUUM operation is needed.
        /// </summary>
        /// <returns>A tuple containing a boolean indicating if vacuum is needed (fragmentation >= 25%)
        /// and the calculated fragmentation percentage.</returns>
        public async Task<(bool NeedVacuum, double FragmentationPercent)> IsVacuumNeeded()
        {
            try
            {
                using var conn = CreateConnection();

                var freelistCount = await conn.ExecuteScalarAsync<long>("PRAGMA freelist_count;");
                var pageCount = await conn.ExecuteScalarAsync<long>("PRAGMA page_count;");

                if (pageCount == 0)
                    return (false, 0);

                double fragmentationPercent = (double)freelistCount / pageCount;
                bool needVacuum = fragmentationPercent >= 0.25;

                return (needVacuum, fragmentationPercent);
            }
            catch (Exception ex)
            {
                await DialogService.Instance.ShowMessageAsync("Vacuum Check Failed", ex.Message);
                return (false, 0);
            }
        }

        private const string ddl = @"
CREATE TABLE IF NOT EXISTS Languages (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Code TEXT NOT NULL UNIQUE,
    Name TEXT
);
CREATE TABLE IF NOT EXISTS Categories (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    LanguageId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    FOREIGN KEY (LanguageId) REFERENCES Languages(ID) ON DELETE RESTRICT ON UPDATE CASCADE
);
CREATE TABLE IF NOT EXISTS Snippets (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    CategoryId INTEGER NOT NULL,
    Title TEXT NOT NULL,
    Code TEXT,
    Description TEXT,
    Tag TEXT,
    FOREIGN KEY (CategoryId) REFERENCES Categories(ID) ON DELETE RESTRICT ON UPDATE CASCADE
);
						
INSERT INTO Languages (ID, Code, Name) VALUES
(1, 'as', 'ActionScript3'),
(2, 'aspx', 'ASP/XHTML'),
(3, 'atg', 'Coco'),
(4, 'bat', 'BAT'),
(5, 'boo', 'Boo'),
(6, 'cpp', 'C++'),
(7, 'cs', 'C#'),
(8, 'css', 'CSS'),
(9, 'd', 'D'),
(10, 'fs', 'F#'),
(11, 'fx', 'HLSL'),
(12, 'html', 'HTML'),
(13, 'ini', 'INI'),
(14, 'java', 'Java'),
(15, 'js', 'JavaScript'),
(16, 'json', 'Json'),
(17, 'md', 'MarkDown'),
(18, 'nut', 'Squirrel'),
(19, 'pas', 'Pascal'),
(20, 'php', 'PHP'),
(21, 'plsql', 'PLSQL'),
(22, 'ps1', 'PowerShell'),
(23, 'py', 'Python'),
(24, 'rb', 'Ruby'),
(25, 'rs', 'Rust'),
(26, 'sql', 'SQL'),
(27, 'tex', 'TeX'),
(28, 'vb', 'VB'),
(29, 'vtl', 'VTL'),
(30, 'xml', 'XML'),
(31, 'lua', 'Lua'),
(32, 'asm', 'Asm'),
(33, 'il', 'IL'),
(34, 'go', 'Go');

INSERT INTO Categories (LanguageId, Name) VALUES
-- C++
(6, 'Basic Syntax'),
(6, 'STL (Standard Template Library)'),
(6, 'File I/O'),
(6, 'Exception Handling'),
(6, 'Pointers and Memory Management'),
(6, 'Classes and Objects'),
(6, 'Templates'),
(6, 'Multithreading'),
(6, 'Algorithms'),
(6, 'Preprocessor Directives'),
-- C#
(7, 'Basic Syntax'),
(7, 'Collections'),
(7, 'Database'),
(7, 'LINQ Queries'),
(7, 'File I/O'),
(7, 'Exception Handling'),
(7, 'OOP Concepts'),
(7, 'Delegates and Events'),
(7, 'Async Programming'),
(7, 'Windows Forms/WPF'),
(7, 'Networking'),
-- D
(9, 'Basic Syntax'),
(9, 'Ranges and Algorithms'),
(9, 'File I/O'),
(9, 'Exception Handling'),
(9, 'Memory Management'),
(9, 'Classes and Structs'),
(9, 'Templates and Mixins'),
(9, 'Concurrency'),
(9, 'Modules and Imports'),
(9, 'Metaprogramming'),
-- Java
(14, 'Basic Syntax'),
(14, 'OOP Concepts'),
(14, 'Collections Framework'),
(14, 'Generics'),
(14, 'Exceptions'),
(14, 'File I/O'),
(14, 'Multithreading and Concurrency'),
(14, 'Java Streams and Lambdas'),
(14, 'Networking'),
(14, 'JVM Internals'),
-- JavaScript
(15, 'Basic Syntax'),
(15, 'Functions and Closures'),
(15, 'DOM Manipulation'),
(15, 'Events'),
(15, 'Promises and Async/Await'),
(15, 'Modules'),
(15, 'ES6+ Features'),
(15, 'Error Handling'),
(15, 'Testing'),
(15, 'Node.js'),
-- Pascal
(19, 'Basic Syntax'),
(19, 'Procedures and Functions'),
(19, 'Data Types and Variables'),
(19, 'Control Structures'),
(19, 'Records and Sets'),
(19, 'File Handling'),
(19, 'Object-Oriented Pascal'),
(19, 'Exception Handling'),
(19, 'Generics'),
(19, 'Multithreading'),
-- PowerShell
(22, 'Basic Syntax'),
(22, 'Cmdlets'),
(22, 'Functions and Scripts'),
(22, 'Modules'),
(22, 'Error Handling'),
(22, 'Remoting and Sessions'),
(22, 'Pipeline and Objects'),
(22, 'Security'),
(22, 'Event Handling'),
(22, 'Desired State Configuration (DSC)'),
-- Python
(23, 'Basic Syntax'),
(23, 'Strings'),
(23, 'Lists and Tuples'),
(23, 'Dictionaries and Sets'),
(23, 'File I/O'),
(23, 'Exception Handling'),
(23, 'Functions and Lambdas'),
(23, 'Classes and OOP'),
(23, 'Modules and Packages'),
(23, 'Iterators and Generators'),
(23, 'Comprehensions'),
(23, 'Decorators'),
(23, 'Context Managers'),
(23, 'Regular Expressions'),
(23, 'Data Serialization'),
(23, 'Networking'),
(23, 'Multithreading and Multiprocessing'),
(23, 'Virtual Environments and Packaging'),
-- Rust
(25, 'Basic Syntax'),
(25, 'Ownership and Borrowing'),
(25, 'Traits and Generics'),
(25, 'Error Handling'),
(25, 'Concurrency'),
(25, 'Modules and Crates'),
(25, 'Macros'),
(25, 'Patterns'),
(25, 'Unsafe Rust'),
(25, 'FFI (Foreign Function Interface)');
";

    }
}
