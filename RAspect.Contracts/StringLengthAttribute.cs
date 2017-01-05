using RAspect.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Contracts
{
    /// <summary>
    /// Attribute that throws <see cref="ArgumentException"/> for target it is applied to when value is assigned a string of invalid length. Null values do not throw an exception
    /// </summary>
    public sealed class StringLengthAttribute : ContractAspect
    {
        /// <summary>
        /// Max Length
        /// </summary>
        internal int MaxLength;

        /// <summary>
        /// Min Length
        /// </summary>
        internal int MinLength;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringLengthAttribute"/> class.
        /// </summary>
        /// <param name="maxLength">MaxLength</param>
        public StringLengthAttribute(int maxLength) : this(0, maxLength)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringLengthAttribute"/> class.
        /// </summary>
        /// <param name="minLength">MinLength</param>
        /// <param name="maxLength"></param>
        public StringLengthAttribute(int minLength, int maxLength)
        {
            MinLength = minLength;
            MaxLength = maxLength;
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
            var strLengthAttr = attr as StringLengthAttribute;
            var str = value as string;
            var strLength = 0;

            if(str == null || (strLength = str.Length) >=  strLengthAttr.MinLength && strLength <= strLengthAttr.MaxLength)
            {
                return null;
            }

            return new ArgumentException(name);
        }
    }
}
