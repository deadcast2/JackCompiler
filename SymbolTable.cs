using System.Collections.Generic;

namespace JackCompiler
{
    public enum SymbolKind { NONE, STATIC, FIELD, ARGUMENT, VAR }

    internal class SymbolTable
    {
        private int _ArgIndex, _VarIndex, _FieldIndex, _StaticIndex;
        private Dictionary<string, (string Type, SymbolKind Kind, int Index)> _Symbols;

        public SymbolTable()
        {
            Reset();
        }

        public void Reset()
        {
            _ArgIndex = 0;
            _VarIndex = 0;
            _FieldIndex = 0;
            _StaticIndex = 0;

            _Symbols = new Dictionary<string, (string Type, SymbolKind Kind, int Index)>();
        }

        internal static SymbolKind KindFor(string value)
        {
            switch(value)
            {
                case "static":
                    return SymbolKind.STATIC;
                case "field":
                    return SymbolKind.FIELD;
                case "var":
                    return SymbolKind.VAR;
                case "arg":
                    return SymbolKind.ARGUMENT;
            }

            return SymbolKind.NONE;
        }

        public (string Type, SymbolKind Kind, int Index) Define(string name, string type, SymbolKind kind)
        {
            var index = 0;

            switch (kind)
            {
                case SymbolKind.STATIC:
                    index = _StaticIndex++;
                    break;
                case SymbolKind.FIELD:
                    index = _FieldIndex++;
                    break;
                case SymbolKind.ARGUMENT:
                    index = _ArgIndex++;
                    break;
                case SymbolKind.VAR:
                    index = _VarIndex++;
                    break;
            }

            return _Symbols[name] = (type, kind, index);
        }

        public int VarCount(SymbolKind kind)
        {
            switch (kind)
            {
                case SymbolKind.STATIC:
                    return _StaticIndex++;
                case SymbolKind.FIELD:
                    return _FieldIndex++;
                case SymbolKind.ARGUMENT:
                    return _ArgIndex++;
                default:
                    return _VarIndex++;
            }
        }

        public SymbolKind KindOf(string name)
        {
            if (_Symbols.ContainsKey(name))
            {
                return _Symbols[name].Kind;
            }

            return SymbolKind.NONE;
        }

        public string TypeOf(string name)
        {
            if (_Symbols.ContainsKey(name))
            {
                return _Symbols[name].Type;
            }

            return string.Empty;
        }

        public int IndexOf(string name)
        {
            if (_Symbols.ContainsKey(name))
            {
                return _Symbols[name].Index;
            }

            return 0;
        }
    }
}
