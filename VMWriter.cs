using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JackCompiler
{
    public enum Segment { CONSTANT, ARGUMENT, LOCAL, STATIC, THIS, THAT, POINTER, TEMP }
    public enum ArithmeticOp { ADD, SUB, NEG, EQ, GT, LT, AND, OR, NOT }

    internal static class VMWriter
    {
        public static string WritePush(Segment segment, int index)
        {
            switch (segment)
            {
                case Segment.CONSTANT:
                    return $"push constant {index}";
                case Segment.ARGUMENT:
                    return $"push argument {index}";
                case Segment.LOCAL:
                    return $"push local {index}";
                case Segment.STATIC:
                    return $"push static {index}";
                case Segment.THIS:
                    return $"push this {index}";
                case Segment.THAT:
                    return $"push that {index}";
                case Segment.POINTER:
                    return $"push pointer {index}";
                case Segment.TEMP:
                    return $"push temp {index}";
            }

            return string.Empty;
        }

        public static string WritePop(Segment segment, int index)
        {
            switch (segment)
            {
                case Segment.ARGUMENT:
                    return $"pop argument {index}";
                case Segment.LOCAL:
                    return $"pop local {index}";
                case Segment.STATIC:
                    return $"pop static {index}";
                case Segment.THIS:
                    return $"pop this {index}";
                case Segment.THAT:
                    return $"pop that {index}";
                case Segment.POINTER:
                    return $"pop pointer {index}";
                case Segment.TEMP:
                    return $"pop temp {index}";
            }

            return string.Empty;
        }

        public static string WriteArithmetic(ArithmeticOp op)
        {
            switch (op)
            {
                case ArithmeticOp.ADD:
                    return "add";
                case ArithmeticOp.SUB:
                    return "sub";
                case ArithmeticOp.NEG:
                    return "neg";
                case ArithmeticOp.EQ:
                    return "eq";
                case ArithmeticOp.GT:
                    return "gt";
                case ArithmeticOp.LT:
                    return "lt";
                case ArithmeticOp.AND:
                    return "and";
                case ArithmeticOp.OR:
                    return "or";
                case ArithmeticOp.NOT:
                    return "not";
            }

            return string.Empty;
        }

        public static string WriteLabel(string label) => $"label {label}";

        public static string WriteGoto(string label) => $"goto {label}";

        public static string WriteIf(string label) => $"if-goto {label}";

        public static string WriteCall(string label, int argCount) => $"call {label} {argCount}";

        public static string WriteFunction(string label, int argCount) => $"function {label} {argCount}";

        public static string WriteReturn() => "return";
    }
}
