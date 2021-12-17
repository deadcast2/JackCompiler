using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JackAnalyzer
{
    internal class CompilationEngine
    {
        public CompilationEngine(string[] filePaths)
        {
            foreach (string path in filePaths)
            {
                var xml = CompileClass(new TokenIterator(new Tokenizer(path).Process()));
                File.WriteAllLines(Path.ChangeExtension(path, "xml"), xml);
            }
        }

        private IEnumerable<string> CompileClass(TokenIterator it)
        {
            var xml = new List<string> { "<class>" };

            if (!it.HasMore())
                throw new Exception("Class definition missing");

            if (!it.Next().Is("keyword", "class"))
                throw new Exception("'Class' keyword expected.");

            xml.Add(it.CurrentAsString());

            if (!it.HasMore())
                throw new Exception("Class name expected.");

            if (!it.Next().Is("identifier"))
                throw new Exception("Class name identifier expected.");

            xml.Add(it.CurrentAsString());

            if (!it.HasMore())
                throw new Exception("Opening bracket for class expected.");

            if (!it.Next().Is("symbol", "{"))
                throw new Exception("'{' symbol expected for class.");

            xml.Add(it.CurrentAsString());

            xml.AddRange(CompileClassVarDec(it));

            xml.AddRange(CompileSubroutine(it));

            if (!it.HasMore())
                throw new Exception("Closing bracket for class expected.");

            if (!it.Next().Is("symbol", "}"))
                throw new Exception("'}' symbol expected for class.");

            xml.Add(it.CurrentAsString());

            xml.Add("</class>");

            return xml;
        }

        private IEnumerable<string> CompileClassVarDec(TokenIterator it)
        {
            return CompileVarDec(it, "classVarDec", "field", "static");
        }

        private IEnumerable<string> CompileVarDec(TokenIterator it,
            string tagName, params string[] allowedValues)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (!it.Peek().Is("keyword", allowedValues))
                return xml;

            xml.Add($"<{tagName}>");

            xml.Add(it.Next().ToString());

            if (!it.HasMore())
                throw new Exception($"Type expected for '{it.Current()}'.");

            if (!it.Next().Is("keyword", "int", "char", "boolean") &&
                !it.Current().Is("identifier", v => true /* validate class name */))
                throw new Exception($"Invalid type defined for field/static: '{it.Current()}'.");

            xml.Add(it.CurrentAsString());

            xml.AddRange(WriteVarName(it));

            xml.Add($"</{tagName}>");

            xml.AddRange(CompileVarDec(it, tagName, allowedValues));

            return xml;
        }

        private IEnumerable<string> CompileSubroutine(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.Next().Is("keyword", "constructor", "function", "method"))
                return xml;

            xml.Add("<subroutineDec>");
            xml.Add(it.CurrentAsString());

            if (!it.Next().Is("keyword", "void", "int", "char", "boolean") &&
                !it.Current().Is("identifier", v => true /* validate class name */))
                throw new Exception($"Invalid type defined for subroutine: '{it.Current()}'.");

            xml.Add(it.CurrentAsString());

            if (!it.Next().Is("identifier"))
                throw new Exception("Expected name for subroutine.");

            xml.Add(it.CurrentAsString());

            if (!it.Next().Is("symbol", "("))
                throw new Exception("Expected opening paranthesis for subroutine.");

            xml.Add(it.CurrentAsString());

            xml.AddRange(CompileParameterList(it));

            if (!it.Next().Is("symbol", ")"))
                throw new Exception("Expected closing paranthesis for subroutine.");

            xml.Add(it.CurrentAsString());

            xml.AddRange(CompileSubroutineBody(it));

            xml.Add("</subroutineDec>");

            xml.AddRange(CompileSubroutine(it));

            return xml;
        }

        private IEnumerable<string> CompileParameterList(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            xml.Add("<parameterList>");

            if (it.Peek().Is("keyword"))
                xml.AddRange(WriteParamName(it));

            xml.Add("</parameterList>");

            return xml;
        }

        private IEnumerable<string> WriteParamName(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.Next().Is("keyword", "int", "char", "boolean") &&
               !it.Current().Is("identifier", v => true /* validate class name */))
                throw new Exception($"Invalid type defined for parameter list: '{it.Current()}'.");

            xml.Add(it.CurrentAsString());

            if (!it.HasMore())
                throw new Exception("Identifier for parameter expected.");

            if (!it.Next().Is("identifier"))
                throw new Exception("Invalid indentifier for parameter.");

            xml.Add(it.CurrentAsString());

            if (it.Peek().Is("symbol", ","))
            {
                xml.Add(it.Next().ToString());
                xml.AddRange(WriteParamName(it));
            }

            return xml;
        }

        private IEnumerable<string> WriteVarName(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                throw new Exception("Var name expected.");

            if (!it.Next().Is("identifier"))
                throw new Exception("Identifier expected for var name.");

            xml.Add(it.CurrentAsString());

            if (it.Next().Is("symbol", ","))
            {
                xml.Add(it.CurrentAsString());
                xml.AddRange(WriteVarName(it));
            }
            else if (it.Current().Is("symbol", ";"))
            {
                xml.Add(it.CurrentAsString());
            }
            else
            {
                throw new Exception("Line ending expected for field/static.");
            }

            return xml;
        }

        private IEnumerable<string> CompileSubroutineBody(TokenIterator it)
        {
            var xml = new List<string> { "<subroutineBody>" };

            if (!it.HasMore())
                throw new Exception("Expected subroutine body.");

            if (!it.Next().Is("symbol", "{"))
                throw new Exception("'{' missing for subroutine body.");

            xml.Add(it.CurrentAsString());

            xml.AddRange(CompileVarDec(it, "varDec", "var"));

            xml.AddRange(CompileStatements(it));

            if (!it.HasMore())
                throw new Exception("Expected subroutine body ending.");

            if (!it.Next().Is("symbol", "}"))
                throw new Exception("'}' missing for subroutine body.");

            xml.Add(it.CurrentAsString());

            xml.Add("</subroutineBody>");

            return xml;
        }

        private IEnumerable<string> CompileStatements(TokenIterator it)
        {
            var xml = new List<string> { "<statements>" };

            while (it.Peek().Is("keyword", "let", "do", "return", "if", "while"))
            {
                xml.AddRange(CompileLet(it));

                xml.AddRange(CompileDo(it));

                xml.AddRange(CompileReturn(it));

                xml.AddRange(CompileIf(it));

                xml.AddRange(CompileWhile(it));
            }

            xml.Add("</statements>");

            return xml;
        }

        private IEnumerable<string> CompileLet(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (!it.Peek().Is("keyword", "let"))
                return xml;

            xml.Add("<letStatement>");

            xml.Add(it.Next().ToString());

            xml.AddRange(GetVarName(it));

            if (!it.HasMore())
                throw new Exception("Equals expected for let statement.");

            if (!it.Next().Is("symbol", "="))
                throw new Exception("Defined equals expected for let statement.");

            xml.Add(it.CurrentAsString());

            xml.AddRange(CompileExpression(it));

            if (!it.HasMore())
                throw new Exception("Ending expected for let statement.");

            if (!it.Next().Is("symbol", ";"))
                throw new Exception("Defined ending expected for let statement.");

            xml.Add(it.CurrentAsString());

            xml.Add("</letStatement>");

            return xml;
        }

        private IEnumerable<string> CompileDo(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (!it.Peek().Is("keyword", "do"))
                return xml;

            xml.Add("<doStatement>");

            xml.Add(it.Next().ToString());

            xml.AddRange(CompileSubroutineCall(it));

            if (!it.HasMore())
                throw new Exception("Ending expected for do statement.");

            if (!it.Next().Is("symbol", ";"))
                throw new Exception("Defined ending expected for do statement.");

            xml.Add(it.CurrentAsString());

            xml.Add("</doStatement>");

            return xml;
        }

        private IEnumerable<string> CompileReturn(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (!it.Peek().Is("keyword", "return"))
                return xml;

            xml.Add("<returnStatement>");

            xml.Add(it.Next().ToString());

            xml.AddRange(CompileExpression(it));

            if (!it.HasMore())
                throw new Exception("Ending expected for return statement.");

            if (!it.Next().Is("symbol", ";"))
                throw new Exception("Defined ending expected for return statement.");

            xml.Add(it.CurrentAsString());

            xml.Add("</returnStatement>");

            return xml;
        }

        private IEnumerable<string> CompileIf(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (!it.Peek().Is("keyword", "if"))
                return xml;

            xml.Add("<ifStatement>");

            xml.Add(it.Next().ToString());

            if (!it.HasMore())
                throw new Exception("Opening paranthesis expected for if statement.");

            if (!it.Next().Is("symbol", "("))
                throw new Exception("Defined opening paranthesis expected for if statement.");

            xml.Add(it.CurrentAsString());

            xml.AddRange(CompileExpression(it));

            if (!it.HasMore())
                throw new Exception("Closing paranthesis expected for if statement.");

            if (!it.Next().Is("symbol", ")"))
                throw new Exception("Defined closing paranthesis expected for if statement.");

            xml.Add(it.CurrentAsString());

            if (!it.HasMore())
                throw new Exception("Opening brace expected for if statement.");

            if (!it.Next().Is("symbol", "{"))
                throw new Exception("Defined opening brace expected for if statement.");

            xml.Add(it.CurrentAsString());

            xml.AddRange(CompileStatements(it));

            if (!it.HasMore())
                throw new Exception("Closing brace expected for if statement.");

            if (!it.Next().Is("symbol", "}"))
                throw new Exception("Defined closing brace expected for if statement.");

            xml.Add(it.CurrentAsString());

            xml.AddRange(CompileElse(it));

            xml.Add("</ifStatement>");

            return xml;
        }

        private IEnumerable<string> CompileElse(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (!it.Peek().Is("keyword", "else"))
                return xml;

            xml.Add(it.Next().ToString());

            if (!it.HasMore())
                throw new Exception("Opening brace expected for if statement.");

            if (!it.Next().Is("symbol", "{"))
                throw new Exception("Defined opening brace expected for if statement.");

            xml.Add(it.CurrentAsString());

            xml.AddRange(CompileStatements(it));

            if (!it.HasMore())
                throw new Exception("Closing brace expected for else statement.");

            if (!it.Next().Is("symbol", "}"))
                throw new Exception("Defined closing brace expected for else statement.");

            xml.Add(it.CurrentAsString());

            return xml;
        }

        private IEnumerable<string> CompileWhile(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (!it.Peek().Is("keyword", "while"))
                return xml;

            xml.Add("<whileStatement>");

            xml.Add(it.Next().ToString());

            if (!it.HasMore())
                throw new Exception("Opening paranthesis expected for while statement.");

            if (!it.Next().Is("symbol", "("))
                throw new Exception("Defined opening paranthesis expected for while statement.");

            xml.Add(it.CurrentAsString());

            xml.AddRange(CompileExpression(it));

            if (!it.HasMore())
                throw new Exception("Closing paranthesis expected for while statement.");

            if (!it.Next().Is("symbol", ")"))
                throw new Exception("Defined closing paranthesis expected for while statement.");

            xml.Add(it.CurrentAsString());

            if (!it.HasMore())
                throw new Exception("Opening brace expected for while statement.");

            if (!it.Next().Is("symbol", "{"))
                throw new Exception("Defined opening brace expected for while statement.");

            xml.Add(it.CurrentAsString());

            xml.AddRange(CompileStatements(it));

            if (!it.HasMore())
                throw new Exception("Closing brace expected for while statement.");

            if (!it.Next().Is("symbol", "}"))
                throw new Exception("Defined closing brace expected for while statement.");

            xml.Add(it.CurrentAsString());

            xml.Add("</whileStatement>");

            return xml;
        }

        private IEnumerable<string> CompileSubroutineCall(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                throw new Exception("Expected subroutine call.");

            if (!it.Next().Is("identifier"))
                throw new Exception("Expected identifier for subroutine call.");

            var identifier = it.CurrentAsString();

            if (!it.HasMore())
                throw new Exception("Expected expression list definition.");

            if (it.Next().Is("symbol", "("))
            {
                xml.Add(identifier);
                xml.Add(it.CurrentAsString());
                xml.AddRange(CompileExpressionList(it));

                if (!it.HasMore())
                    throw new Exception("Expected more tokens to finish subroutine call.");

                if (!it.Next().Is("symbol", ")"))
                    throw new Exception("Expected subroutine call closing paranthesis.");

                xml.Add(it.CurrentAsString());
            }
            else if (it.Current().Is("symbol", "."))
            {
                xml.Add(identifier);
                xml.Add(it.CurrentAsString());
                xml.AddRange(CompileSubroutineCall(it));
            }

            return xml;
        }

        private IEnumerable<string> CompileExpressionList(TokenIterator it)
        {
            var xml = new List<string> { "<expressionList>" };

            xml.AddRange(CompileExpression(it));

            xml.Add("</expressionList>");

            return xml;
        }

        private IEnumerable<string> CompileExpression(TokenIterator it)
        {
            var xml = new List<string>();

            var terms = CompileTerm(it);

            if (terms.Count() > 0)
            {
                xml.Add("<expression>");

                xml.AddRange(terms);

                if (it.HasMore() && it.Peek().Is("symbol", "+", "-", "*", "/", "&", "|", "<", ">", "="))
                {
                    xml.Add(it.Next().ToString());
                    xml.AddRange(CompileTerm(it));
                }

                xml.Add("</expression>");

                if (it.HasMore() && it.Peek().Is("symbol", ","))
                {
                    xml.Add(it.Next().ToString());
                    xml.AddRange(CompileExpression(it));
                }
            }

            return xml;
        }

        private IEnumerable<string> CompileTerm(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (it.Peek().Is("identifier") ||
                it.Peek().Is("integerConstant") ||
                it.Peek().Is("stringConstant") ||
                it.Peek().Is("symbol", "(", "-", "~") ||
                it.Peek().Is("keyword"))
            {
                xml.Add("<term>");

                if (it.Peek(2).Is("symbol", "."))
                {
                    xml.AddRange(CompileSubroutineCall(it));
                }
                else if (it.Peek(2).Is("symbol", "["))
                {
                    xml.AddRange(GetVarName(it));
                }
                else if (it.Peek().Is("symbol", "("))
                {
                    xml.Add(it.Next().ToString());
                    xml.AddRange(CompileExpression(it));
                    xml.Add(it.Next().ToString());
                }
                else if (it.Peek().Is("symbol", "-", "~")) // unary ops
                {
                    xml.Add(it.Next().ToString());
                    xml.AddRange(CompileTerm(it));
                }
                else
                {
                    xml.Add(it.Next().ToString());
                }

                xml.Add("</term>");
            }

            return xml;
        }

        private IEnumerable<string> GetVarName(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                throw new Exception("Identifier expected for var name.");

            if (!it.Next().Is("identifier"))
                throw new Exception("Defined indentifier expected for var name.");

            xml.Add(it.CurrentAsString());

            if (it.HasMore() && it.Peek().Is("symbol", "["))
            {
                xml.Add(it.Next().ToString());
                xml.AddRange(CompileExpression(it));

                if (!it.HasMore())
                    throw new Exception("Closing bracket expected for array access.");

                if (!it.Next().Is("symbol"))
                    throw new Exception("Defined closing bracket expected for array access.");

                xml.Add(it.CurrentAsString());
            }

            return xml;
        }
    }
}
