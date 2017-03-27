using RAspect.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Contracts
{
    /// <summary>
    /// Attribute that throws <see cref="ArgumentException"/> for target it is applied to when value is not a valid URL starting with http/https/ftp. Null strings are accepted and do not throw exception
    /// </summary>
    public sealed class UrlAttribute : ContractAspect
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
            if(value == null || Uri.IsWellFormedUriString(value as string, UriKind.Absolute))
            {
                return null;
            }

            return new ArgumentException(name);
        }
    }
}
