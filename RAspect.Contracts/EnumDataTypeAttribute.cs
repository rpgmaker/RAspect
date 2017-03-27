using RAspect.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Contracts
{
    /// <summary>
    /// Attribute that throws <see cref="ArgumentException"/> for target it is applied to when value is not a valid member of an enumeration
    /// </summary>
    public sealed class EnumDataTypeAttribute : ContractAspect
    {
        /// <summary>
        /// Enum Type
        /// </summary>
        internal Type EnumType;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumDataTypeAttribute"/> class.
        /// </summary>
        public EnumDataTypeAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumDataTypeAttribute"/> class.
        /// </summary>
        /// <param name="enumType">Enum Type</param>
        public EnumDataTypeAttribute(Type enumType)
        {
            EnumType = enumType;
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
            if(value == null)
            {
                goto done;
            }

            var str = value as string;
            var enumAttr = attr as EnumDataTypeAttribute;
            var enumType = enumAttr.EnumType;
            var values = str != null ? Enum.GetNames(enumType) : 
                Enum.GetValues(enumType).Cast<object>().Select(x => x.ToString()).ToArray();

            str = value.ToString();

            if (values.Contains(str))
            {
                return null;
            }

            done:
            return new ArgumentException(name);
        }
    }
}
