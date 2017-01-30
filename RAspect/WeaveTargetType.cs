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
        Assembly = 2,
        Constructors = 4,
        Methods = 8,
        Properties = 16,
        Fields = 32,
        Events = 64,
        Parameters = 64 * 2,
        Class = Parameters * 2,
        All = Assembly | Constructors | Methods | Properties | Fields | Events | Parameters | Class
    }
}
