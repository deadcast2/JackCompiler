using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace JackCompiler
{
    internal class Tokenizer
    {
        private readonly string _FilePath;

        public Tokenizer(string filePath)
        {
            _FilePath = filePath;
        }

        public string Process()
        {
            var cleaned = Utils.RemoveComments(File.ReadAllText(_FilePath));
            var xmlLines = new List<string> { "<tokens>" };

            foreach (var line in cleaned.SplitAndNormalize())
            {
                xmlLines.AddRange(GetTokens(line));
            }

            xmlLines.Add("</tokens>");

            return string.Concat(xmlLines);
        }

        private IEnumerable<string> GetTokens(string line)
        {
            foreach (var piece in Inflate(EncodeStringConstants(line)).Split(' '))
            {
                if (IsKeyword(piece, out var keyword))
                    yield return keyword;
                else if (IsSymbol(piece, out var symbol))
                    yield return symbol;
                else if (IsIntegerConstant(piece, out var integer))
                    yield return integer;
                else if (IsStringConstant(piece, out var _string))
                    yield return _string;
                else if (IsIdentifier(piece, out var identifier))
                    yield return identifier;
            }
        }

        private string EncodeStringConstants(string line)
        {
            return Regex.Replace(line, @"""[^""]*""", m =>
            {
                var encoded = Utils.ConvertToBase64(m.Value.Without("\""));

                return $"\"{encoded}\"";
            });
        }

        private string Inflate(string line)
        {
            return Regex.Replace(line, @"(?!"")(?!\s)(\W)", " $1 ");
        }

        private bool IsKeyword(string piece, out string token)
        {
            var keywords = new List<string>
            {
                "class", "constructor", "function", "method",
                "field", "static", "var", "int", "char", "boolean",
                "void", "true", "false", "null", "this", "let",
                "do", "if", "else", "while", "return"
            };

            token = $"<keyword>{piece}</keyword>";

            return keywords.Contains(piece);
        }

        private bool IsSymbol(string piece, out string token)
        {
            var symbols = new Dictionary<string, string>
            {
                { "{", "{" }, { "}", "}" }, { "(", "(" }, { ")", ")" }, { "[", "[" },
                { "]", "]" }, { ".", "." }, { ",", "," }, { ";", ";" }, { "+", "+" },
                { "-", "-" }, { "*", "*" }, { "/", "/" }, { "&", "&amp;" }, { "|", "|" },
                { "<", "&lt;" }, { ">", "&gt;" }, { "=", "=" }, { "~", "~" }
            };

            token = $"<symbol>{(symbols.ContainsKey(piece) ? symbols[piece] : "")}</symbol>";

            return symbols.ContainsKey(piece);
        }

        private bool IsIntegerConstant(string piece, out string token)
        {
            token = $"<integerConstant>{piece}</integerConstant>";

            return int.TryParse(piece, out var integer) && integer.InRange(0, Utils.MaxInt);
        }

        private bool IsStringConstant(string piece, out string token)
        {
            var match = Regex.Match(piece, @"^""([^""\s]+)""$");

            token = $"<stringConstant>{Utils.ConvertFromBase64(match.Value.Without("\""))}</stringConstant>";

            return match.Success;
        }

        private bool IsIdentifier(string piece, out string token)
        {
            var match = Regex.Match(piece, @"^[^\d][a-zA-Z\d_]*$");

            token = $"<identifier>{match.Value}</identifier>";

            return match.Success;
        }

        private string OutputPath(string outputName)
        {
            var path = Path.ChangeExtension(outputName, "xml");

            return path.Insert(path.LastIndexOf('.'), "T");
        }
    }
}
