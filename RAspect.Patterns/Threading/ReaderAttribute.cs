using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Threading
{
    /// <summary>
    /// Attribute when applied on a method specified that method requires read access to the object
    /// </summary>
    public class ReaderAttribute : ReaderWriterBase
    {
        /// <summary>
        /// Enter ReadLock Method
        /// </summary>
        private readonly static MethodInfo EnterReadLockMethod = typeof(System.Threading.ReaderWriterLockSlim).GetMethod("EnterReadLock", ILWeaver.NonPublicBinding);

        /// <summary>
        /// Exit ReadLock Method
        /// </summary>
        private readonly static MethodInfo ExitReadLockMethod = typeof(System.Threading.ReaderWriterLockSlim).GetMethod("ExitReadLock", ILWeaver.NonPublicBinding);

        /// <summary>
        /// Gets Enter Method
        /// </summary>
        internal override MethodInfo EnterMethod
        {
            get
            {
                return EnterReadLockMethod;
            }
        }

        /// <summary>
        /// Gets Exit Method
        /// </summary>
        internal override MethodInfo ExitMethod
        {
            get
            {
                return ExitReadLockMethod;
            }
        }
    }
}
