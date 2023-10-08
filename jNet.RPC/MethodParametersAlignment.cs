using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace jNet.RPC
{
    internal static class MethodParametersAlignment
    {
        public static void AlignType(ref object input, Type type)
        {
            if (type.IsEnum)
                input = Enum.Parse(type, input.ToString());
            else
            if (input is string && type == typeof(TimeSpan))
                input = TimeSpan.Parse((string)input, System.Globalization.CultureInfo.InvariantCulture);
            else
            if (input is string && type == typeof(TimeSpan?))
                input = TimeSpan.Parse((string)input, System.Globalization.CultureInfo.InvariantCulture);
            else
            if (input is string && type == typeof(Guid))
                input = Guid.Parse((string)input);
            else
            if (type.IsValueType && input != null && !type.IsGenericType)
                input = Convert.ChangeType(input, type);
        }
    }
}
