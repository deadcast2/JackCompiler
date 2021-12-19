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
        private int _NextLabelIndex;

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

            var kind = SymbolTable.KindFor(it.Next().Value);

            if (!it.HasMore())
                throw new Exception($"Type expected for '{it.Current()}'.");

            if (!it.Next().Is("keyword", "int", "char", "boolean") &&
                !it.Current().Is("identifier", v => true /* validate class name */))
                throw new Exception($"Invalid type defined for field/static: '{it.Current()}'.");

            var type = it.Current().Value;

            xml.AddRange(WriteVarName(symbolTable, type, kind, it));

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

            var returnType = it.Current().Value;

            if (!it.Next().Is("identifier"))
                throw new Exception("Expected name for subroutine.");

            var name = $"{className}.{it.Current().Value}";

            if (!it.Next().Is("symbol", "("))
                throw new Exception("Expected opening paranthesis for subroutine.");

            xml.AddRange(CompileParameterList(it, _SubroutineSymbolTable));

            if (!it.Next().Is("symbol", ")"))
                throw new Exception("Expected closing paranthesis for subroutine.");

            // Save the body to write after symbol table gets updated in case of any locals.
            var body = CompileSubroutineBody(it, _SubroutineSymbolTable, returnType);

            xml.Add(VMWriter.WriteFunction(name, _SubroutineSymbolTable.VarCount(SymbolKind.VAR)));

            xml.AddRange(body);

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

            symbolTable.Define(it.Current().Value, type, SymbolKind.ARGUMENT);

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

            if (it.Next().Is("symbol", ","))
            {
                xml.AddRange(WriteVarName(symbolTable, type, kind, it));
            }
            else if (!it.Current().Is("symbol", ";"))
            {
                throw new Exception("Line ending expected for field/static.");
            }

            return xml;
        }

        private IEnumerable<string> CompileSubroutineBody(TokenIterator it, SymbolTable symbolTable, string returnType)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                throw new Exception("Expected subroutine body.");

            if (!it.Next().Is("symbol", "{"))
                throw new Exception("'{' missing for subroutine body.");

            xml.AddRange(CompileVarDec(it, symbolTable, "varDec", "var"));

            xml.AddRange(CompileStatements(it, returnType));

            if (!it.HasMore())
                throw new Exception("Expected subroutine body ending.");

            if (!it.Next().Is("symbol", "}"))
                throw new Exception("'}' missing for subroutine body.");

            return xml;
        }

        private IEnumerable<string> CompileStatements(TokenIterator it, string returnType)
        {
            var xml = new List<string>();

            while (it.Peek().Is("keyword", "let", "do", "return", "if", "while"))
            {
                xml.AddRange(CompileLet(it));

                xml.AddRange(CompileDo(it));

                xml.AddRange(CompileReturn(it, returnType));

                xml.AddRange(CompileIf(it, returnType));

                xml.AddRange(CompileWhile(it, returnType));
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

            it.Next();

            var varStatement = GetVarName(it);

            if (!it.HasMore())
                throw new Exception("Equals expected for let statement.");

            if (!it.Next().Is("symbol", "="))
                throw new Exception("Defined equals expected for let statement.");

            xml.AddRange(CompileExpression(it).Item1);

            xml.AddRange(varStatement);

            if (!it.HasMore())
                throw new Exception("Ending expected for let statement.");

            if (!it.Next().Is("symbol", ";"))
                throw new Exception("Defined ending expected for let statement.");

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

            xml.Add(VMWriter.WritePop(Segment.TEMP, 0));

            if (!it.HasMore())
                throw new Exception("Ending expected for do statement.");

            if (!it.Next().Is("symbol", ";"))
                throw new Exception("Defined ending expected for do statement.");

            return xml;
        }

        private IEnumerable<string> CompileReturn(TokenIterator it, string returnType)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (!it.Peek().Is("keyword", "return"))
                return xml;

            it.Next();

            xml.AddRange(CompileExpression(it).Item1);

            if (returnType == "void")
                xml.Add(VMWriter.WritePush(Segment.CONSTANT, 0));

            xml.Add(VMWriter.WriteReturn());

            if (!it.HasMore())
                throw new Exception("Ending expected for return statement.");

            if (!it.Next().Is("symbol", ";"))
                throw new Exception("Defined ending expected for return statement.");

            return xml;
        }

        private IEnumerable<string> CompileIf(TokenIterator it, string returnType)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (!it.Peek().Is("keyword", "if"))
                return xml;

            it.Next();

            if (!it.HasMore())
                throw new Exception("Opening paranthesis expected for if statement.");

            if (!it.Next().Is("symbol", "("))
                throw new Exception("Defined opening paranthesis expected for if statement.");

            xml.AddRange(CompileExpression(it).Item1);

            xml.Add(VMWriter.WriteArithmetic(ArithmeticOp.NOT));

            var label1 = $"L{++_NextLabelIndex}";
            var label2 = $"L{++_NextLabelIndex}";

            xml.Add(VMWriter.WriteIf(label1));

            if (!it.HasMore())
                throw new Exception("Closing paranthesis expected for if statement.");

            if (!it.Next().Is("symbol", ")"))
                throw new Exception("Defined closing paranthesis expected for if statement.");

            if (!it.HasMore())
                throw new Exception("Opening brace expected for if statement.");

            if (!it.Next().Is("symbol", "{"))
                throw new Exception("Defined opening brace expected for if statement.");

            xml.AddRange(CompileStatements(it, returnType));

            if (!it.HasMore())
                throw new Exception("Closing brace expected for if statement.");

            if (!it.Next().Is("symbol", "}"))
                throw new Exception("Defined closing brace expected for if statement.");

            var @else = CompileElse(it, label2, returnType);

            if (@else.Count() > 0)
                xml.Add(VMWriter.WriteGoto(label2));

            xml.Add(VMWriter.WriteLabel(label1));

            xml.AddRange(@else);

            return xml;
        }

        private IEnumerable<string> CompileElse(TokenIterator it, string label, string returnType)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (!it.Peek().Is("keyword", "else"))
                return xml;

            it.Next();

            if (!it.HasMore())
                throw new Exception("Opening brace expected for if statement.");

            if (!it.Next().Is("symbol", "{"))
                throw new Exception("Defined opening brace expected for if statement.");

            xml.AddRange(CompileStatements(it, returnType));

            xml.Add(VMWriter.WriteLabel(label));

            if (!it.HasMore())
                throw new Exception("Closing brace expected for else statement.");

            if (!it.Next().Is("symbol", "}"))
                throw new Exception("Defined closing brace expected for else statement.");

            return xml;
        }

        private IEnumerable<string> CompileWhile(TokenIterator it, string returnType)
        {
            var xml = new List<string>();

            if (!it.HasMore())
                return xml;

            if (!it.Peek().Is("keyword", "while"))
                return xml;

            it.Next();

            var label1 = $"L{++_NextLabelIndex}";

            xml.Add(VMWriter.WriteLabel(label1));

            if (!it.HasMore())
                throw new Exception("Opening paranthesis expected for while statement.");

            if (!it.Next().Is("symbol", "("))
                throw new Exception("Defined opening paranthesis expected for while statement.");

            xml.AddRange(CompileExpression(it).Item1);

            xml.Add(VMWriter.WriteArithmetic(ArithmeticOp.NOT));

            var label2 = $"L{++_NextLabelIndex}";

            xml.Add(VMWriter.WriteIf(label2));

            if (!it.HasMore())
                throw new Exception("Closing paranthesis expected for while statement.");

            if (!it.Next().Is("symbol", ")"))
                throw new Exception("Defined closing paranthesis expected for while statement.");

            if (!it.HasMore())
                throw new Exception("Opening brace expected for while statement.");

            if (!it.Next().Is("symbol", "{"))
                throw new Exception("Defined opening brace expected for while statement.");

            xml.AddRange(CompileStatements(it, returnType));

            xml.Add(VMWriter.WriteGoto(label1));

            xml.Add(VMWriter.WriteLabel(label2));

            if (!it.HasMore())
                throw new Exception("Closing brace expected for while statement.");

            if (!it.Next().Is("symbol", "}"))
                throw new Exception("Defined closing brace expected for while statement.");

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

                xml.AddRange(expressionList.Item1);

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

        private (IEnumerable<string>, int) CompileExpression(TokenIterator it, int argCount = 0)
        {
            var xml = new List<string>();

            var terms = CompileTerm(it);

            if (terms.Count() > 0)
            {
                argCount++;

                xml.AddRange(terms);

                if (it.HasMore() && it.Peek().Is("symbol", "+", "-", "*", "/", "&", "|", "<", ">", "="))
                {
                    var op = it.Next().Value;

                    xml.AddRange(CompileTerm(it));

                    switch (op)
                    {
                        case "+":
                            xml.Add(VMWriter.WriteArithmetic(ArithmeticOp.ADD));
                            break;
                        case "-":
                            xml.Add(VMWriter.WriteArithmetic(ArithmeticOp.SUB));
                            break;
                        case "*":
                            xml.Add(VMWriter.WriteCall("Math.multiply", 2));
                            break;
                        case "<":
                            xml.Add(VMWriter.WriteArithmetic(ArithmeticOp.LT));
                            break;
                        case ">":
                            xml.Add(VMWriter.WriteArithmetic(ArithmeticOp.GT));
                            break;
                        case "=":
                            xml.Add(VMWriter.WriteArithmetic(ArithmeticOp.EQ));
                            break;
                        case "&":
                            xml.Add(VMWriter.WriteArithmetic(ArithmeticOp.AND));
                            break;
                        default:
                            xml.Add(op);
                            break;
                    }
                }

                if (it.HasMore() && it.Peek().Is("symbol", ","))
                {
                    it.Next();

                    var expression = CompileExpression(it, argCount);

                    // Important to gather the recursive arg count;
                    argCount = expression.Item2;

                    xml.AddRange(expression.Item1);
                }
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
                    var op = it.Next().Value;

                    xml.AddRange(CompileTerm(it));

                    if (op == "-")
                        xml.Add(VMWriter.WriteArithmetic(ArithmeticOp.NEG));
                    else
                        xml.Add(VMWriter.WriteArithmetic(ArithmeticOp.NOT));
                }
                else
                {
                    if (it.Peek().Is("integerConstant"))
                    {
                        xml.Add(VMWriter.WritePush(Segment.CONSTANT, int.Parse(it.Next().Value)));
                    }
                    else if (it.Peek().Is("keyword"))
                    {
                        var keyword = it.Next().Value;

                        if (keyword == "true")
                        {
                            xml.Add(VMWriter.WritePush(Segment.CONSTANT, 1));
                            xml.Add(VMWriter.WriteArithmetic(ArithmeticOp.NEG));
                        }
                        else if (keyword == "false")
                        {
                            xml.Add(VMWriter.WritePush(Segment.CONSTANT, 0));
                        }
                    }
                    else
                    {
                        // Refactor this!
                        var name = it.Next().Value;
                        var segment = Segment.UNKNOWN;
                        var kind = _SubroutineSymbolTable.KindOf(name);

                        if (kind == SymbolKind.ARGUMENT)
                            segment = Segment.ARGUMENT;
                        else if (kind == SymbolKind.VAR)
                            segment = Segment.LOCAL;
                        //*********************************

                        xml.Add(VMWriter.WritePush(segment, _SubroutineSymbolTable.IndexOf(name)));
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

            // Refactor this!
            var name = it.Current().Value;
            var segment = Segment.UNKNOWN;
            var kind = _SubroutineSymbolTable.KindOf(name);

            if (kind == SymbolKind.ARGUMENT)
                segment = Segment.ARGUMENT;
            else if (kind == SymbolKind.VAR)
                segment = Segment.LOCAL;
            //*********************************

            xml.Add(VMWriter.WritePop(segment, _SubroutineSymbolTable.IndexOf(name)));

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
