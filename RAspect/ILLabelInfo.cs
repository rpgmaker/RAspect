namespace RAspect
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection.Emit;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Container for IL Label information
    /// </summary>
    public class ILLabelInfo
    {
        /// <summary>
        /// Gets or sets index of label
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets label
        /// </summary>
        public Label Label { get; set; }

        /// <summary>
        /// Gets or sets indication of marked label
        /// </summary>
        public bool Marked { get; set; }
    }
}
