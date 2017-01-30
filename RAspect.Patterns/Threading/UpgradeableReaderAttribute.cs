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
    /// Attribute will require both read and exclusive write (No other threads can write) when applied to method or properties
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Assembly)]
    public class UpgradeableReaderAttribute : ReaderWriterBase
    {
        /// <summary>
        /// Enter UpgradeableReadLock Method
        /// </summary>
        private readonly static MethodInfo EnterUpgradeableReadLockMethod = typeof(System.Threading.ReaderWriterLockSlim).GetMethod("EnterUpgradeableReadLock", ILWeaver.NonPublicBinding);

        /// <summary>
        /// Exit UpgradeableReadLock Method
        /// </summary>
        private readonly static MethodInfo ExitUpgradeableReadLockMethod = typeof(System.Threading.ReaderWriterLockSlim).GetMethod("ExitUpgradeableReadLock", ILWeaver.NonPublicBinding);

        /// <summary>
        /// Gets Enter Method
        /// </summary>
        internal override MethodInfo EnterMethod
        {
            get
            {
                return EnterUpgradeableReadLockMethod;
            }
        }

        /// <summary>
        /// Gets Exit Method
        /// </summary>
        internal override MethodInfo ExitMethod
        {
            get
            {
                return ExitUpgradeableReadLockMethod;
            }
        }
    }
}
