using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect
{
    /// <summary>
    /// Weaving Access Modifier
    /// </summary>
    [Flags]
    public enum WeaveAccessModifier
    {
        None = 0,
        Public = 2,
        NonPublic = 4,
        All = Public | NonPublic
    }
}
