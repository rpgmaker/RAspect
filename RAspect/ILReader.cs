namespace RAspect
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

    
    /// <summary>
    /// MSIL Reader
    /// </summary>
    public class ILReader
    {
        /// <summary>
        /// Lock Object
        /// </summary>
        private static readonly object LockObject = new object();

        /// <summary>
        /// Multiple OpCodes
        /// </summary>
        private static OpCode[] multiOpCodes;

        /// <summary>
        /// Single OpCodes
        /// </summary>
        private static OpCode[] singleOpCodes;

        /// <summary>
        /// Current Instruction
        /// </summary>
        private ILInstruction current;

        /// <summary>
        /// Module
        /// </summary>
        private Module module;

        /// <summary>
        /// Method
        /// </summary>
        private MethodInfo method;

        /// <summary>
        /// CIL Byte Array
        /// </summary>
        private byte[] cilCodes;

        /// <summary>
        /// CIL Position
        /// </summary>
        private int position;

        /// <summary>
        /// Initializes static members of the <see cref="ILReader"/> class.
        /// </summary>
        static ILReader()
        {
            if (multiOpCodes == null)
            {
                lock (LockObject)
                {
                    const int SIZE = 0x100;
                    singleOpCodes = new OpCode[SIZE];
                    multiOpCodes = new OpCode[SIZE];

                    var codeType = typeof(OpCode);
                    FieldInfo[] opcodes = typeof(OpCodes).GetRuntimeFields().ToArray();

                    for (var i = 0; i < opcodes.Length; i++)
                    {
                        var opcode = opcodes[i];
                        if (opcode.FieldType == codeType)
                        {
                            var code = (OpCode)opcode.GetValue(null);
                            ushort codeValue = unchecked((ushort)code.Value);

                            if (codeValue < SIZE)
                            {
                                singleOpCodes[(int)codeValue] = code;
                            }
                            else
                            {
                                if ((codeValue & 0xff00) != 0xfe00)
                                {
                                    throw new Exception("Unsupported opcode");
                                }

                                multiOpCodes[codeValue & 0xff] = code;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ILReader"/> class.
        /// </summary>
        /// <param name="method">Method Info</param>
        public ILReader(MethodInfo method)
        {
            this.method = method ?? throw new ArgumentException("method");
            this.module = method.Module;
            this.position = 0;
            this.cilCodes = method is DynamicMethod ? GetILBytes(method as DynamicMethod) : method.GetMethodBody().GetILAsByteArray();
        }

        /// <summary>
        /// Gets current MSIL instruction
        /// </summary>
        public ILInstruction Current
        {
            get
            {
                return current;
            }
        }

        /// <summary>
        /// Stream MSIL instructions
        /// </summary>
        /// <returns>Bool</returns>
        public bool Read()
        {
            var il = this.cilCodes;

            if (this.position >= il.Length)
            {
                return false;
            }

            current = new ILInstruction();

            var code = OpCodes.Nop;
            ushort value = il[this.position++];

            current.Last = this.position == il.Length;

            if (value != 0xfe)
            {
                code = singleOpCodes[value];
            }
            else
            {
                value = il[this.position++];
                code = multiOpCodes[value];
                value = (ushort)(value | 0xfe00);
            }

            current.Instruction = code;
            current.Index = this.position - 1;
            current.Data = GetData(module, code, il);
            var size = GetSize(code.OperandType);
            current.RawData = il.Skip(this.position - size).Take(size).ToArray();

            return true;
        }

        /// <summary>
        /// Get IL for given dynamic method
        /// </summary>
        /// <param name="method">Dynamic Method</param>
        /// <returns>Byte[]</returns>
        private static byte[] GetILBytes(DynamicMethod method)
        {
            var il = method.GetILGenerator();
            const BindingFlags NonPublicBinding = BindingFlags.NonPublic | BindingFlags.Instance;
            var stream = typeof(ILGenerator).GetField("m_ILStream", NonPublicBinding);
            if (stream == null)
            {
                return new byte[0];
            }

            var buffer = stream.GetValue(il) as byte[];

            return buffer.Take(il.ILOffset).ToArray();
        }

        /// <summary>
        /// Get Size based on IL type
        /// </summary>
        /// <param name="cilOpType">Operand Type</param>
        /// <returns>int</returns>
        private static int GetSize(OperandType cilOpType)
        {
            switch (cilOpType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineSwitch:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Get Data for given Opcode
        /// </summary>
        /// <param name="module">Module</param>
        /// <param name="code">OpCode</param>
        /// <param name="il">CIL Buffer</param>
        /// <returns>Object</returns>
        private object GetData(Module module, OpCode code, byte[] il)
        {
            object data = null;
            int metadataToken = 0;
            switch (code.OperandType)
            {
                case OperandType.InlineField:
                    metadataToken = ReadInt32(il);
                    try
                    {
                        data = module.ResolveField(metadataToken);
                    }
                    catch
                    {
                        data = module.ResolveField(metadataToken,
                            method.DeclaringType.GetGenericArguments(),
                            method.GetGenericArguments());
                    }

                    break;
                case OperandType.InlineSwitch:
                    {
                        int count = ReadInt32(il);
                        int[] caseAddresses = new int[count];
                        for (int i = 0; i < count; i++)
                        {
                            caseAddresses[i] = ReadInt32(il);
                        }

                        data = caseAddresses;
                    }

                    break;
                case OperandType.InlineBrTarget:
                    metadataToken = ReadInt32(il);
                    metadataToken += position;

                    data = metadataToken;
                    break;
                case OperandType.InlineI:
                    data = ReadInt32(il);
                    break;
                case OperandType.InlineI8:
                    data = ReadInt64(il);
                    break;
                case OperandType.InlineMethod:
                    metadataToken = ReadInt32(il);
                    try
                    {
                        data = module.ResolveMethod(metadataToken);
                    }
                    catch
                    {
                        try
                        {
                            data = module.ResolveMember(metadataToken);
                        }
                        catch
                        {
                            try
                            {
                                data = module.ResolveMethod(metadataToken,
                                    method.DeclaringType.GetGenericArguments(),
                                    method.GetGenericArguments());
                            }
                            catch
                            {
                                data = module.ResolveMember(metadataToken,
                                    method.DeclaringType.GetGenericArguments(),
                                    method.GetGenericArguments());
                            }
                        }
                    }
                    
                    break;
                case OperandType.InlineR:
                    data = ReadDouble(il);
                    break;
                case OperandType.InlineSig:
                    data = module.ResolveSignature(ReadInt32(il));
                    break;
                case OperandType.InlineString:
                    data = module.ResolveString(ReadInt32(il));
                    break;
                case OperandType.InlineTok:
                    data = module.ResolveType(ReadInt32(il), method.DeclaringType.GetGenericArguments(), method.GetGenericArguments());
                    break;
                case OperandType.InlineType:
                    metadataToken = ReadInt32(il);
                    try
                    {
                        data = module.ResolveType(metadataToken);
                    }
                    catch
                    {
                        data = module.ResolveType(metadataToken,
                            method.DeclaringType.GetGenericArguments(),
                            method.GetGenericArguments());
                    }

                    break;
                case OperandType.InlineVar:
                    data = ReadUInt16(il);
                    break;
                case OperandType.ShortInlineVar:
                    data = ReadByte(il);
                    break;
                case OperandType.ShortInlineI:
                    data = ReadSByte(il);
                    break;
                case OperandType.ShortInlineBrTarget:
                    data = ReadSByte(il) + position;
                    break;
                case OperandType.ShortInlineR:
                    data = ReadSingle(il);
                    break;
            }

            return data;
        }

        /// <summary>
        /// Read IL to value
        /// </summary>
        /// <param name="il">CIL Buffer</param>
        /// <returns>ushort</returns>
        private ushort ReadUInt16(byte[] il)
        {
            return (ushort)(il[position++] | (il[position++] << 8));
        }

        /// <summary>
        /// Read IL to value
        /// </summary>
        /// <param name="il">CIL Buffer</param>
        /// <returns>int</returns>
        private int ReadInt32(byte[] il)
        {
            return ((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18);
        }

        /// <summary>
        /// Read IL to value
        /// </summary>
        /// <param name="il">CIL Buffer</param>
        /// <returns>ulong</returns>
        private ulong ReadInt64(byte[] il)
        {
            return (ulong)(((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18) | (il[position++] << 0x20) | (il[position++] << 0x28) | (il[position++] << 0x30) | (il[position++] << 0x38));
        }

        /// <summary>
        /// Read IL to value
        /// </summary>
        /// <param name="il">CIL Buffer</param>
        /// <returns>double</returns>
        private double ReadDouble(byte[] il)
        {
            return ((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18) | (il[position++] << 0x20) | (il[position++] << 0x28) | (il[position++] << 0x30) | (il[position++] << 0x38);
        }

        /// <summary>
        /// Read IL to value
        /// </summary>
        /// <param name="il">CIL Buffer</param>
        /// <returns>sbyte</returns>
        private sbyte ReadSByte(byte[] il)
        {
            return (sbyte)il[position++];
        }

        /// <summary>
        /// Read IL to value
        /// </summary>
        /// <param name="il">CIL Buffer</param>
        /// <returns>byte</returns>
        private byte ReadByte(byte[] il)
        {
            return (byte)il[position++];
        }

        /// <summary>
        /// Read IL to value
        /// </summary>
        /// <param name="il">CIL Buffer</param>
        /// <returns>Single</returns>
        private float ReadSingle(byte[] il)
        {
            return (float)(((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18));
        }
    }
}