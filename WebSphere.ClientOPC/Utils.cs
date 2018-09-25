using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Opc.Da;
using WebSphere.Domain.Abstract;
using WebSphere.Domain.Concrete;

namespace WebSphere.ClientOPC
{
    static class LoggerUtils
    {
        private static Type tp;
        private static TypeCode tc;
        public static bool IsAnalogType(this object o)
        {
            switch (Type.GetTypeCode(o.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsArrayType(this object o)
        {
            tp = o.GetType();
            tc = Type.GetTypeCode(tp);
            if (tp.Name == "Byte[]") return true;
            return false;
        }

        public static bool IsDiscreteType(this object o)
        {
            return Type.GetTypeCode(o.GetType()) == TypeCode.Boolean;
        }
        public static bool IsPropertyExist(dynamic settings, string name)
        {
            return settings.GetType().GetProperty(name) != null;
        }
    }
}
