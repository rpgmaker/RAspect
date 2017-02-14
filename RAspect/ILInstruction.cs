using System.Reflection.Emit;

namespace RAspect
{    
    /// <summary>
    /// IL instruction container
    /// </summary>
    public struct ILInstruction
    {
        /// <summary>
        /// Empty Instruction
        /// </summary>
        public static readonly ILInstruction Zero = new ILInstruction();

        /// <summary>
        /// Instruction
        /// </summary>
        public OpCode Instruction;

        /// <summary>
        /// Index
        /// </summary>
        public int Index;

        /// <summary>
        /// Data
        /// </summary>
        public object Data;

        /// <summary>
        /// Raw Data
        /// </summary>
        public byte[] RawData;

        public bool Last;
    }
}
