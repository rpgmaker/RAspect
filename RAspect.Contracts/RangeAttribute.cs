using RAspect.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Contracts
{
    /// <summary>
    /// Attribute that throws <see cref="ArgumentOutOfRangeException"/> for target it is applied to when value is outside a given range
    /// </summary>
    public class RangeAttribute : ContractAspect
    {
        /// <summary>
        /// Min Value
        /// </summary>
        internal double Min;

        /// <summary>
        /// Max Value
        /// </summary>
        internal double Max;

        /// <summary>
        /// Initializes a new instance of the <see cref="RangeAttribute"/> class.
        /// </summary>
        public RangeAttribute() : this(0, double.MaxValue)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RangeAttribute"/> class.
        /// </summary>
        /// <param name="min">Min</param>
        /// <param name="max">Max</param>
        public RangeAttribute(double min, double max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RangeAttribute"/> class.
        /// </summary>
        /// <param name="min">Min</param>
        /// <param name="max">Max</param>
        public RangeAttribute(long min, long max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RangeAttribute"/> class.
        /// </summary>
        /// <param name="min">Min</param>
        /// <param name="max">Max</param>
        public RangeAttribute(ulong min, ulong max)
        {
            Min = min;
            Max = max;
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
            if (value == null)
            {
                return null;
            }

            var rangeAttr = attr as RangeAttribute;

            var num = (double)value;

            if(num >= rangeAttr.Min && num <= rangeAttr.Max)
            {
                return null;
            }

            return new ArgumentOutOfRangeException(name);
        }
    }
}
