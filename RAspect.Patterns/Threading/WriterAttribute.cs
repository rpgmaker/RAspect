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
    /// Attribute when applied on a method, specified that the method requires write access to the object
    /// </summary>
    public class WriterAttribute : ReaderWriterBase
    {
        /// <summary>
        /// Enter WriteLock Method
        /// </summary>
        private readonly static MethodInfo EnterWriteLockMethod = typeof(System.Threading.ReaderWriterLockSlim).GetMethod("EnterWriteLock", ILWeaver.NonPublicBinding);

        /// <summary>
        /// Exit WriteLock Method
        /// </summary>
        private readonly static MethodInfo ExitWriteLockMethod = typeof(System.Threading.ReaderWriterLockSlim).GetMethod("ExitWriteLock", ILWeaver.NonPublicBinding);

        /// <summary>
        /// Gets Enter Method
        /// </summary>
        internal override MethodInfo EnterMethod
        {
            get
            {
                return EnterWriteLockMethod;
            }
        }

        /// <summary>
        /// Gets Exit Method
        /// </summary>
        internal override MethodInfo ExitMethod
        {
            get
            {
                return ExitWriteLockMethod;
            }
        }
    }
}
