using System;
using System.Xml.Linq;
using System.Linq;

namespace JackCompiler
{
    internal static class XElementExtentions
    {
        public static bool Is(this XElement el, string type, params string[] values)
        {
            return el != null && el.Name == type && values.Contains(el.Value);
        }

        public static bool Is(this XElement el, string type, Func<string, bool> pred)
        {
            return el != null && el.Name == type && pred(el.Value);
        }

        public static bool Is(this XElement el, string type)
        {
            return el != null && el.Name == type;
        }
    }
}
