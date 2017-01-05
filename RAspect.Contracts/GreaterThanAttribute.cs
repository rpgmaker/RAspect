using RAspect.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Contracts
{
    /// <summary>
    /// Attribute that throws <see cref="ArgumentOutOfRangeException"/> for target it is applied to when value is smaller than a given value
    /// </summary>
    public class GreaterThanAttribute : RangeAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GreaterThanAttribute"/> class.
        /// </summary>
        /// <param name="value">Value</param>
        public GreaterThanAttribute(double value) : base(value, double.MaxValue)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GreaterThanAttribute"/> class.
        /// </summary>
        /// <param name="value">Value</param>
        public GreaterThanAttribute(long value) : base(value, long.MaxValue)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GreaterThanAttribute"/> class.
        /// </summary>
        /// <param name="value">Value</param>
        public GreaterThanAttribute(ulong value) : base(value, ulong.MaxValue)
        {
        }
    }
}
