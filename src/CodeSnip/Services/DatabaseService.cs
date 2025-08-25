using CodeSnip.Views.LanguageCategoryView;
using CodeSnip.Views.SnippetView;
using Dapper;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace CodeSnip.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath = "snippets.sqlite";

        public IDbConnection CreateConnection()
        {
            return new SQLiteConnection($"Data Source={_dbPath};foreign keys=true;");
        }

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


        public IEnumerable<Language> GetSnippets()
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
    S.Code AS SnippetCode,
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
                // Pronađi ili kreiraj kategoriju
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
                            Code = (string)row.SnippetCode,
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

        public void UpdateSnippetCode(int id, string code)
        {
            using var conn = CreateConnection();
            conn.Execute(
                "UPDATE Snippets SET Code = @Code WHERE Id = @Id",
                new { Id = id, Code = code });
        }

        public void DeleteSnippet(int id)
        {
            using var conn = CreateConnection();
            conn.Execute("DELETE FROM Snippets WHERE Id = @Id", new { Id = id });
        }

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

        public void SaveLanguage(Language language)
        {
            using var conn = CreateConnection();
            if (language.Id == 0)
            {
                conn.Execute("INSERT INTO Languages (Code, Name) VALUES (@Code, @Name)", language);
            }
            else
            {
                conn.Execute("UPDATE Languages SET Code = @Code, Name = @Name WHERE ID = @Id", language);
            }
        }

        public void SaveCategory(Category category)
        {
            using var conn = CreateConnection();
            if (category.Id == 0)
            {
                conn.Execute("INSERT INTO Categories (LanguageId, Name) VALUES (@LanguageId, @Name)", category);
            }
            else
            {
                conn.Execute("UPDATE Categories SET Name = @Name WHERE ID = @Id AND LanguageId = @LanguageId", category);
            }
        }

        public void DeleteLanguage(int id)
        {
            using var conn = CreateConnection();
            conn.Execute("DELETE FROM Languages WHERE ID = @Id", new { Id = id });
        }

        public void DeleteCategory(int id)
        {
            using var conn = CreateConnection();
            conn.Execute("DELETE FROM Categories WHERE ID = @Id", new { Id = id });
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
						
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (1,'as','ActionScript3');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (2,'aspx','ASP/XHTML');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (3,'atg','Coco');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (4,'bat','BAT');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (5,'boo','Boo');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (6,'cpp','C++');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (7,'cs','C#');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (8,'css','CSS');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (9,'d','D');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (10,'fs','F#');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (11,'fx','HLSL');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (12,'html','HTML');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (13,'ini','INI');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (14,'java','Java');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (15,'js','JavaScript');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (16,'json','Json');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (17,'md','MarkDown');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (18,'nut','Squirrel');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (19,'pas','Pascal');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (20,'php','PHP');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (21,'plsql','PLSQL');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (22,'ps1','PowerShell');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (23,'py','Python');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (24,'rb','Ruby');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (25,'rs','Rust');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (26,'sql','SQL');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (27,'tex','TeX');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (28,'vb','VB');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (29,'vtl','VTL');
			INSERT INTO [Languages] ([ID],[Code],[Name]) VALUES (30,'xml','XML');
         
            INSERT INTO Categories (LanguageId, Name) VALUES (6, 'Basic Syntax');
            INSERT INTO Categories (LanguageId, Name) VALUES (6, 'STL (Standard Template Library)');
            INSERT INTO Categories (LanguageId, Name) VALUES (6, 'File I/O');
            INSERT INTO Categories (LanguageId, Name) VALUES (6, 'Exception Handling');
            INSERT INTO Categories (LanguageId, Name) VALUES (6, 'Pointers and Memory Management');
            INSERT INTO Categories (LanguageId, Name) VALUES (6, 'Classes and Objects');
            INSERT INTO Categories (LanguageId, Name) VALUES (6, 'Templates');
            INSERT INTO Categories (LanguageId, Name) VALUES (6, 'Multithreading');
            INSERT INTO Categories (LanguageId, Name) VALUES (6, 'Algorithms');
            INSERT INTO Categories (LanguageId, Name) VALUES (6, 'Preprocessor Directives');

            INSERT INTO Categories (LanguageId, Name) VALUES (7, 'Basic Syntax');
            INSERT INTO Categories (LanguageId, Name) VALUES (7, 'Collections');
            INSERT INTO Categories (LanguageId, Name) VALUES (7, 'Database');
            INSERT INTO Categories (LanguageId, Name) VALUES (7, 'LINQ Queries');
            INSERT INTO Categories (LanguageId, Name) VALUES (7, 'File I/O');
            INSERT INTO Categories (LanguageId, Name) VALUES (7, 'Exception Handling');
            INSERT INTO Categories (LanguageId, Name) VALUES (7, 'OOP Concepts');
            INSERT INTO Categories (LanguageId, Name) VALUES (7, 'Delegates and Events');
            INSERT INTO Categories (LanguageId, Name) VALUES (7, 'Async Programming');
            INSERT INTO Categories (LanguageId, Name) VALUES (7, 'Windows Forms/WPF');
            INSERT INTO Categories (LanguageId, Name) VALUES (7, 'Networking');
            
            INSERT INTO Categories (LanguageId, Name) VALUES (9, 'Basic Syntax');
            INSERT INTO Categories (LanguageId, Name) VALUES (9, 'Ranges and Algorithms');
            INSERT INTO Categories (LanguageId, Name) VALUES (9, 'File I/O');
            INSERT INTO Categories (LanguageId, Name) VALUES (9, 'Exception Handling');
            INSERT INTO Categories (LanguageId, Name) VALUES (9, 'Memory Management');
            INSERT INTO Categories (LanguageId, Name) VALUES (9, 'Classes and Structs');
            INSERT INTO Categories (LanguageId, Name) VALUES (9, 'Templates and Mixins');
            INSERT INTO Categories (LanguageId, Name) VALUES (9, 'Concurrency');
            INSERT INTO Categories (LanguageId, Name) VALUES (9, 'Modules and Imports');
            INSERT INTO Categories (LanguageId, Name) VALUES (9, 'Metaprogramming');
			
			INSERT INTO Categories (LanguageId, Name) VALUES (14, 'Basic Syntax');
			INSERT INTO Categories (LanguageId, Name) VALUES (14, 'OOP Concepts');
			INSERT INTO Categories (LanguageId, Name) VALUES (14, 'Collections Framework');
			INSERT INTO Categories (LanguageId, Name) VALUES (14, 'Generics');
			INSERT INTO Categories (LanguageId, Name) VALUES (14, 'Exceptions');
			INSERT INTO Categories (LanguageId, Name) VALUES (14, 'File I/O');
			INSERT INTO Categories (LanguageId, Name) VALUES (14, 'Multithreading and Concurrency');
			INSERT INTO Categories (LanguageId, Name) VALUES (14, 'Java Streams and Lambdas');
			INSERT INTO Categories (LanguageId, Name) VALUES (14, 'Networking');
			INSERT INTO Categories (LanguageId, Name) VALUES (14, 'JVM Internals');
			
			INSERT INTO Categories (LanguageId, Name) VALUES (15, 'Basic Syntax');
			INSERT INTO Categories (LanguageId, Name) VALUES (15, 'Functions and Closures');
			INSERT INTO Categories (LanguageId, Name) VALUES (15, 'DOM Manipulation');
			INSERT INTO Categories (LanguageId, Name) VALUES (15, 'Events');
			INSERT INTO Categories (LanguageId, Name) VALUES (15, 'Promises and Async/Await');
			INSERT INTO Categories (LanguageId, Name) VALUES (15, 'Modules');
			INSERT INTO Categories (LanguageId, Name) VALUES (15, 'ES6+ Features');
			INSERT INTO Categories (LanguageId, Name) VALUES (15, 'Error Handling');
			INSERT INTO Categories (LanguageId, Name) VALUES (15, 'Testing');
			INSERT INTO Categories (LanguageId, Name) VALUES (15, 'Node.js');
			
			INSERT INTO Categories (LanguageId, Name) VALUES (19, 'Basic Syntax');
			INSERT INTO Categories (LanguageId, Name) VALUES (19, 'Procedures and Functions');
			INSERT INTO Categories (LanguageId, Name) VALUES (19, 'Data Types and Variables');
			INSERT INTO Categories (LanguageId, Name) VALUES (19, 'Control Structures');
			INSERT INTO Categories (LanguageId, Name) VALUES (19, 'Records and Sets');
			INSERT INTO Categories (LanguageId, Name) VALUES (19, 'File Handling');
			INSERT INTO Categories (LanguageId, Name) VALUES (19, 'Object-Oriented Pascal');
			INSERT INTO Categories (LanguageId, Name) VALUES (19, 'Exception Handling');
			INSERT INTO Categories (LanguageId, Name) VALUES (19, 'Generics');
			INSERT INTO Categories (LanguageId, Name) VALUES (19, 'Multithreading');
			
			INSERT INTO Categories (LanguageId, Name) VALUES (22, 'Basic Syntax');
			INSERT INTO Categories (LanguageId, Name) VALUES (22, 'Cmdlets');
			INSERT INTO Categories (LanguageId, Name) VALUES (22, 'Functions and Scripts');
			INSERT INTO Categories (LanguageId, Name) VALUES (22, 'Modules');
			INSERT INTO Categories (LanguageId, Name) VALUES (22, 'Error Handling');
			INSERT INTO Categories (LanguageId, Name) VALUES (22, 'Remoting and Sessions');
			INSERT INTO Categories (LanguageId, Name) VALUES (22, 'Pipeline and Objects');
			INSERT INTO Categories (LanguageId, Name) VALUES (22, 'Security');
			INSERT INTO Categories (LanguageId, Name) VALUES (22, 'Event Handling');
			INSERT INTO Categories (LanguageId, Name) VALUES (22, 'Desired State Configuration (DSC)');

            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Basic Syntax');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Strings');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Lists and Tuples');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Dictionaries and Sets');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'File I/O');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Exception Handling');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Functions and Lambdas');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Classes and OOP');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Modules and Packages');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Iterators and Generators');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Comprehensions');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Decorators');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Context Managers');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Regular Expressions');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Data Serialization');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Networking');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Multithreading and Multiprocessing');
            INSERT INTO Categories (LanguageId, Name) VALUES (23, 'Virtual Environments and Packaging');
			
			INSERT INTO Categories (LanguageId, Name) VALUES (25, 'Basic Syntax');
			INSERT INTO Categories (LanguageId, Name) VALUES (25, 'Ownership and Borrowing');
			INSERT INTO Categories (LanguageId, Name) VALUES (25, 'Traits and Generics');
			INSERT INTO Categories (LanguageId, Name) VALUES (25, 'Error Handling');
			INSERT INTO Categories (LanguageId, Name) VALUES (25, 'Concurrency');
			INSERT INTO Categories (LanguageId, Name) VALUES (25, 'Modules and Crates');
			INSERT INTO Categories (LanguageId, Name) VALUES (25, 'Macros');
			INSERT INTO Categories (LanguageId, Name) VALUES (25, 'Patterns');
			INSERT INTO Categories (LanguageId, Name) VALUES (25, 'Unsafe Rust');
			INSERT INTO Categories (LanguageId, Name) VALUES (25, 'FFI (Foreign Function Interface)');

        ";

    }
}
