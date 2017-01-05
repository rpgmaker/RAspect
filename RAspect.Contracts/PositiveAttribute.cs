using RAspect.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Contracts
{
    /// <summary>
    /// Attribute that throws <see cref="ArgumentOutOfRangeException"/> for target it is applied to when value is smaller than  zero
    /// </summary>
    public sealed class PositiveAttribute : GreaterThanAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PositiveAttribute"/> class.
        /// </summary>
        public PositiveAttribute() : base(0)
        {

        }
    }
}
