using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect
{
    /// <summary>
    /// Enum for managing 
    /// </summary>
    [Flags]
    public enum WeaveTargetType
    {
        None = 0,
        Constructors = 2,
        Methods = 4,
        Properties = 8,
        Fields = 16,
        Events = 32,
        Parameters = 64,
        All = Constructors | Methods | Properties | Fields | Events | Parameters
    }
}
