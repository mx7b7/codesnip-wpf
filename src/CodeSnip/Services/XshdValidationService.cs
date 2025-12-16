using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace CodeSnip.Services
{
    public class ValidationResult
    {
        public List<string> Errors { get; } = [];
        public bool IsValid => Errors.Count == 0;
    }

    public class XshdValidationService
    {

        public ValidationResult Validate(string xshdXml)
        {
            var result = new ValidationResult();

            // 1) XML well-formed
            XDocument doc;
            try
            {
                doc = XDocument.Parse(xshdXml);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"The XSHD source is not valid XML: {ex.Message}");
                return result;
            }

            // 2) XSHD schema sanity checks
            ValidateHighlightingStructure(doc, result);

            // 3) RuleSet span fatal patterns
            ValidateFatalSpanPatterns(doc, result);

            return result;
        }

        private static void ValidateHighlightingStructure(XDocument doc, ValidationResult result)
        {
            try
            {
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(doc.ToString()));
                using var reader = XmlReader.Create(ms);
                _ = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"HighlightingLoader failed: {ex.Message}");
            }
        }

        private static void ValidateFatalSpanPatterns(XDocument doc, ValidationResult result)
        {
            XNamespace ns = doc.Root?.Name.Namespace ?? "";

            foreach (var rs in doc.Descendants().Where(e => e.Name.LocalName == "RuleSet"))
            {
                foreach (var span in rs.Elements(ns + "Span"))
                {
                    var begin = (string?)span.Attribute("begin") ?? span.Element(ns + "Begin")?.Value ?? "";
                    var end = (string?)span.Attribute("end") ?? span.Element(ns + "End")?.Value ?? "";


                    if (IsFatalPattern(begin))
                        result.Errors.Add($"Fatal span begin pattern: '{begin}'");

                    if (end != "$" && IsFatalPattern(end))
                        result.Errors.Add($"Fatal span end pattern: '{end}'");
                }
            }
        }

        private static bool IsFatalPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) 
                return false;

            // Allow negative lookaheads, which are valid zero-width assertions often used for multiline span endings.
            if (pattern.Contains("?!"))
                return false;

            try
            {
                var regex = new Regex(pattern, RegexOptions.Singleline);
                var match = regex.Match("");
                return match.Success && match.Index == 0 && match.Length == 0;
            }
            catch
            {
                return true;
            }
        }
    }
}
