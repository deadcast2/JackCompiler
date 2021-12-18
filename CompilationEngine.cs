using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JackCompiler
{
    internal class CompilationEngine
    {
        private SymbolTable _ClassSymbolTable = new SymbolTable();
        private SymbolTable _SubroutineSymbolTable = new SymbolTable();

        public CompilationEngine(string[] filePaths)
        {
            foreach (string path in filePaths)
            {
                var vm = CompileClass(new TokenIterator(new Tokenizer(path).Process()));

                File.WriteAllLines(Path.ChangeExtension(path, "vm"), vm);
            }
        }

        private IEnumerable<string> CompileClass(TokenIterator it)
        {
            _ClassSymbolTable.Reset();

            var xml = new List<string>();

            if (!it.HasMore())
                throw new Exception("Class definition missing");

            if (!it.Next().Is("keyword", "class"))
                throw new Exception("'Class' keyword expected.");

            if (!it.HasMore())
                throw new Exception("Class name expected.");

            if (!it.Next().Is("identifier"))
                throw new Exception("Class name identifier expected.");

            var name = it.Current().Value;

            if (!it.HasMore())
                throw new Exception("Opening bracket for class expected.");

            if (!it.Next().Is("symbol", "{"))
                throw new Exception("'{' symbol expected for class.");

            xml.AddRange(CompileClassVarDec(it));

            xml.AddRange(CompileSubroutineDec(it, name));

            if (!it.HasMore())
                throw new Exception("Closing bracket for class expected.");

            if (!it.Next().Is("symbol", "}"))
                throw new Exception("'}' symbol expected for class.");

            return xml;
        }

        private IEnumerable<string> CompileClassVarDec(TokenIterator it)
        {
            return CompileVarDec(it, _ClassSymbolTable, "classVarDec", "field", "static");
        }

        private IEnumerable<string> CompileVarDec(TokenIterator it, SymbolTable symbolTable,
            string tagName, params string[] allowedValues)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (!it.Peek().Is("keyword", allowedValues))
                return xml;

            xml.Add($"<{tagName}>");

            var kind = SymbolTable.KindFor(it.Next().Value);

            xml.Add(it.Current().ToString());

            if (!it.HasMore())
                throw new Exception($"Type expected for '{it.Current()}'.");

            if (!it.Next().Is("keyword", "int", "char", "boolean") &&
                !it.Current().Is("identifier", v => true /* validate class name */))
                throw new Exception($"Invalid type defined for field/static: '{it.Current()}'.");

            var type = it.Current().Value;

            xml.Add(it.CurrentAsString());

            xml.AddRange(WriteVarName(symbolTable, type, kind, it));

            xml.Add($"</{tagName}>");

            xml.AddRange(CompileVarDec(it, symbolTable, tagName, allowedValues));

            return xml;
        }

        private IEnumerable<string> CompileSubroutineDec(TokenIterator it, string className)
        {
            _SubroutineSymbolTable.Reset();

            var xml = new List<string>();

            if (!it.Next().Is("keyword", "constructor", "function", "method"))
                return xml;

            if (!it.Next().Is("keyword", "void", "int", "char", "boolean") &&
                !it.Current().Is("identifier", v => true /* validate class name */))
                throw new Exception($"Invalid type defined for subroutine: '{it.Current()}'.");

            if (!it.Next().Is("identifier"))
                throw new Exception("Expected name for subroutine.");

            var name = $"{className}.{it.Current().Value}";

            if (!it.Next().Is("symbol", "("))
                throw new Exception("Expected opening paranthesis for subroutine.");

            xml.AddRange(CompileParameterList(it, _SubroutineSymbolTable));

            xml.Add(VMWriter.WriteFunction(name, _SubroutineSymbolTable.VarCount(SymbolKind.ARG)));

            if (!it.Next().Is("symbol", ")"))
                throw new Exception("Expected closing paranthesis for subroutine.");

            xml.AddRange(CompileSubroutineBody(it, _SubroutineSymbolTable));

            xml.AddRange(CompileSubroutineDec(it, className));

            return xml;
        }

        private IEnumerable<string> CompileParameterList(TokenIterator it, SymbolTable symbolTable)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (it.Peek().Is("keyword"))
                xml.AddRange(WriteParamName(it, symbolTable));

            return xml;
        }

        private IEnumerable<string> WriteParamName(TokenIterator it, SymbolTable symbolTable)
        {
            var xml = new List<string>();

            if (!it.Next().Is("keyword", "int", "char", "boolean") &&
               !it.Current().Is("identifier", v => true /* validate class name */))
                throw new Exception($"Invalid type defined for parameter list: '{it.Current()}'.");

            var type = it.Current().Value;

            if (!it.HasMore())
                throw new Exception("Identifier for parameter expected.");

            if (!it.Next().Is("identifier"))
                throw new Exception("Invalid indentifier for parameter.");

            symbolTable.Define(it.Current().Value, type, SymbolKind.ARG);

            if (it.Peek().Is("symbol", ","))
            {
                it.Next();

                xml.AddRange(WriteParamName(it, symbolTable));
            }

            return xml;
        }

        private IEnumerable<string> WriteVarName(SymbolTable symbolTable, string type, SymbolKind kind, TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                throw new Exception("Var name expected.");

            if (!it.Next().Is("identifier"))
                throw new Exception("Identifier expected for var name.");

            symbolTable.Define(it.Current().Value, type, kind);

            xml.Add(it.CurrentAsString());

            if (it.Next().Is("symbol", ","))
            {
                xml.Add(it.CurrentAsString());

                xml.AddRange(WriteVarName(symbolTable, type, kind, it));
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

        private IEnumerable<string> CompileSubroutineBody(TokenIterator it, SymbolTable symbolTable)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                throw new Exception("Expected subroutine body.");

            if (!it.Next().Is("symbol", "{"))
                throw new Exception("'{' missing for subroutine body.");

            xml.AddRange(CompileVarDec(it, symbolTable, "varDec", "var"));

            xml.AddRange(CompileStatements(it));

            if (!it.HasMore())
                throw new Exception("Expected subroutine body ending.");

            if (!it.Next().Is("symbol", "}"))
                throw new Exception("'}' missing for subroutine body.");

            return xml;
        }

        private IEnumerable<string> CompileStatements(TokenIterator it)
        {
            var xml = new List<string>();

            while (it.Peek().Is("keyword", "let", "do", "return", "if", "while"))
            {
                xml.AddRange(CompileLet(it));

                xml.AddRange(CompileDo(it));

                xml.AddRange(CompileReturn(it));

                xml.AddRange(CompileIf(it));

                xml.AddRange(CompileWhile(it));
            }

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

            xml.AddRange(CompileExpression(it).Item1);

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

            it.Next();

            xml.AddRange(CompileSubroutineCall(it));

            if (!it.HasMore())
                throw new Exception("Ending expected for do statement.");

            if (!it.Next().Is("symbol", ";"))
                throw new Exception("Defined ending expected for do statement.");

            return xml;
        }

        private IEnumerable<string> CompileReturn(TokenIterator it)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (!it.Peek().Is("keyword", "return"))
                return xml;

            it.Next();

            xml.AddRange(CompileExpression(it).Item1);

            xml.Add(VMWriter.WriteReturn());

            if (!it.HasMore())
                throw new Exception("Ending expected for return statement.");

            if (!it.Next().Is("symbol", ";"))
                throw new Exception("Defined ending expected for return statement.");

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

            xml.AddRange(CompileExpression(it).Item1);

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

            xml.AddRange(CompileExpression(it).Item1);

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

        private IEnumerable<string> CompileSubroutineCall(TokenIterator it, string prefix = "")
        {
            var xml = new List<string>();

            if (!it.HasMore())
                throw new Exception("Expected subroutine call.");

            if (!it.Next().Is("identifier"))
                throw new Exception("Expected identifier for subroutine call.");

            var identifier = it.Current().Value;

            if (!it.HasMore())
                throw new Exception("Expected expression list definition.");

            if (it.Next().Is("symbol", "("))
            {
                var expressionList = CompileExpressionList(it);

                xml.AddRange(expressionList.Item1.Reverse());

                xml.Add(VMWriter.WriteCall(prefix + identifier, expressionList.Item2));

                if (!it.HasMore())
                    throw new Exception("Expected more tokens to finish subroutine call.");

                if (!it.Next().Is("symbol", ")"))
                    throw new Exception("Expected subroutine call closing paranthesis.");
            }
            else if (it.Current().Is("symbol", "."))
            {
                xml.AddRange(CompileSubroutineCall(it, $"{identifier}."));
            }

            return xml;
        }

        private (IEnumerable<string>, int) CompileExpressionList(TokenIterator it)
        {
            var xml = new List<string>();

            var expressions = CompileExpression(it);

            xml.AddRange(expressions.Item1);

            return (xml, expressions.Item2);
        }

        private (IEnumerable<string>, int) CompileExpression(TokenIterator it, int argCount = 1)
        {
            var xml = new List<string>();

            var terms = CompileTerm(it);

            if (terms.Count() > 0)
            {
                if (it.HasMore() && it.Peek().Is("symbol", "+", "-", "*", "/", "&", "|", "<", ">", "="))
                {
                    var op = it.Next().Value;

                    switch (op)
                    {
                        case "+":
                            xml.Add(VMWriter.WriteArithmetic(ArithmeticOp.ADD));
                            break;
                        case "*":
                            xml.Add(VMWriter.WriteCall("Math.multiply", 2));
                            break;
                        default:
                            xml.Add(op);
                            break;
                    }

                    xml.AddRange(CompileTerm(it));
                }

                if (it.HasMore() && it.Peek().Is("symbol", ","))
                {
                    xml.Add(it.Next().ToString());
                    xml.AddRange(CompileExpression(it, ++argCount).Item1);
                }

                xml.AddRange(terms);
            }

            return (xml, argCount);
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
                    it.Next();

                    xml.AddRange(CompileExpression(it).Item1);

                    it.Next();
                }
                else if (it.Peek().Is("symbol", "-", "~")) // unary ops
                {
                    xml.Add(it.Next().ToString());
                    xml.AddRange(CompileTerm(it));
                }
                else
                {
                    if (it.Peek().Is("integerConstant"))
                    {
                        xml.Add(VMWriter.WritePush(Segment.CONSTANT, int.Parse(it.Next().Value)));
                    }
                    else
                    {
                        xml.Add(it.Next().ToString());
                    }
                }
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
                xml.AddRange(CompileExpression(it).Item1);

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
