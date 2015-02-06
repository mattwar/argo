using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Utilities
{
    public static class TypeHelper
    {
        public static Type GetNonNullableType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }

        public static Type GetElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }
            else if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                return type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    .Select(i => i.GetGenericArguments()[0]).FirstOrDefault()
                    ?? typeof(object);
            }
            else
            {
                return null;
            }
        }

        public static bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
        {
            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType)
                {
                    var gtd = i.GetGenericTypeDefinition();
                    if (gtd == typeof(IReadOnlyDictionary<,>) || gtd == typeof(IDictionary<,>))
                    {
                        var args = i.GetGenericArguments();
                        keyType = args[0];
                        valueType = args[1];
                        return true;
                    }
                }
            }

            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                keyType = typeof(object);
                valueType = typeof(object);
                return true;
            }

            keyType = null;
            valueType = null;
            return false;
        }
    }
}