﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RAspect.Patterns.Exception;

namespace RAspect.Patterns.Threading
{
    /// <summary>
    /// Attribute when applied to a type, ensures that only one thread executes in methods of this type. When more than one thread accesses methods of this type, a <see cref="ConcurrentAccessException"/> exception is thrown
    /// </summary>
    public class ThreadUnSafeAttribute : AspectBase
    {
        /// <summary>
        /// Gets weave block type
        /// </summary>
        internal override WeaveBlockType BlockType
        {
            get
            {
                return WeaveBlockType.Wrapping;
            }
        }
    }
}
