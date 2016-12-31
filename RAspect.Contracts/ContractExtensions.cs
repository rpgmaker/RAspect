using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Contracts
{
    public static class ContractExtensions
    {
        private static Dictionary<string, MethodInfo> _methods = new Dictionary<string, MethodInfo>();

        static ContractExtensions()
        {
            var methods = typeof(ContractExtensions).GetMethods();

            foreach(var method in methods)
            {
                _methods[method.Name] = method;
            }
        }

        public static MethodInfo GetMethod(string name)
        {
            return _methods[name];
        }

        /// <summary>
        /// Validate if value satisfy required attribute checks
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsValidateForRequired<T>(this T value)
        {
            var str = value as string;
            if (str != null && string.IsNullOrWhiteSpace(str))
                return false;

            return value != null;
        }
    }
}
