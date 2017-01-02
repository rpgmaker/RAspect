using RAspect.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace RAspect.Contracts
{
    /// <summary>
    /// Attribute that throws <see cref="ArgumentException"/> for target it is applied to when is not a valid credit card
    /// </summary>
    public class CreditCardAttribute : ContractAspect
    {
        /// <summary>
        /// Regex pattern credit card
        /// </summary>
        private static Regex CreditCardRegex = new Regex(@"", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase); 
        
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
            if (CreditCardRegex.IsMatch(value as string))
            {
                return null;
            }

            return new ArgumentException(name);
        }
    }
}
