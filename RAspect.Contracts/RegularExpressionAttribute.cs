using RAspect.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RAspect.Contracts
{
    /// <summary>
    /// Attribute that throws <see cref="ArgumentException"/> for target it is applied to when value does not match a given regular expression pattern
    /// </summary>
    public class RegularExpressionAttribute : ContractAspect
    {
        /// <summary>
        /// Regex Instance
        /// </summary>
        internal readonly Regex Regex;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegularExpressionAttribute"/> class.
        /// </summary>
        public RegularExpressionAttribute() : this("(.*)")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RegularExpressionAttribute"/> class.
        /// </summary>
        /// <param name="pattern">Pattern</param>
        public RegularExpressionAttribute(string pattern) : this(pattern, RegexOptions.None)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RegularExpressionAttribute"/> class.
        /// </summary>
        /// <param name="pattern">Pattern</param>
        /// <param name="options">Options</param>
        public RegularExpressionAttribute(string pattern, RegexOptions options)
        {
            Regex = new Regex(pattern, options | RegexOptions.Compiled);
        }

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
            var regexAttr = attr as RegularExpressionAttribute;
            if (regexAttr.Regex.IsMatch(value as string))
                return null;
            
            return new ArgumentException(name);
        }
    }
}
