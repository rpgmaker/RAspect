using RAspect.Patterns.Exception;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Threading
{
    /// <summary>
    /// Freezeable class for managing class freezeability
    /// </summary>
    public class Freezeable
    {
        /// <summary>
        /// Tracker collection for keeping track of frozen instance
        /// </summary>
        private readonly static ConcurrentDictionary<object, bool> Freezes =
            new ConcurrentDictionary<object, bool>();

        /// <summary>
        /// Freeze current instance
        /// </summary>
        /// <param name="value">Value</param>
        public static void Freeze(object value)
        {
            Freezes[value] = true;
        }

        /// <summary>
        /// UnFreeze current instance
        /// </summary>
        /// <param name="value">Value</param>
        public static void UnFreeze(object value)
        {
            Freezes[value] = false;
        }

        /// <summary>
        /// Return true if current instance is frozen
        /// </summary>
        /// <param name="value">Value</param>
        public static bool IsFrozen(object value)
        {
            var frozen = false;
            if (Freezes.TryGetValue(value, out frozen))
            {
                return frozen;
            }

            return frozen;
        }

        /// <summary>
        /// Throw exception if current instance is frozen
        /// </summary>
        /// <param name="value">Value</param>
        public static void ThrowIfFrozen(object value)
        {
            if (IsFrozen(value))
            {
                throw new ObjectReadOnlyException();
            }
        }
    }
}
