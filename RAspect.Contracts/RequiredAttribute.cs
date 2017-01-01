using RAspect.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection.Emit;
using System.Reflection;

namespace RAspect.Contracts
{
    /// <summary>
    /// Attribute that throws <see cref="NullReferenceException"/> for target it is applied to when they are null, empty or whitespace
    /// </summary>
    public sealed class RequiredAttribute : ContractAspect
    {
        /// <summary>
        /// Validate value against contract implementation
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="name">Name</param>
        /// <param name="isParameter">Flag indicating if value is from a parameter</param>
        /// <param name="attrs">Attribute</param>
        /// <returns>Exception</returns>
        protected override Exception ValidateContract(object value, string name, bool isParameter, ContractAspect attr)
        {
            var str = value as string;
            if (str != null && string.IsNullOrWhiteSpace(str))
                return new NullReferenceException(name);

            return value != null ? null : new NullReferenceException(name);
        }
    }
}
