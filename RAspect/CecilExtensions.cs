using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect
{
    /// <summary>
    /// Extension to provide similar emit calls as ILGenerator
    /// </summary>
    public static class CecilExtensions
    {
        /// <summary>
        /// Cecil module
        /// </summary>
        internal static ModuleDefinition module;

        [ThreadStatic]
        internal static ExceptionHandler tryHandler;

        [ThreadStatic]
        internal static ExceptionHandler catchHandler;

        [ThreadStatic]
        internal static Instruction endHandler;

        public static Instruction BeginExceptionBlock(this ILProcessor il)
        {
            endHandler = Instruction.Create(OpCodes.Nop);
            tryHandler = new ExceptionHandler(ExceptionHandlerType.Finally)
            {
                TryStart = Instruction.Create(OpCodes.Nop)
            };
            catchHandler = new ExceptionHandler(ExceptionHandlerType.Catch)
            {
                TryStart = Instruction.Create(OpCodes.Nop),
                CatchType = module.Import(typeof(Exception))
            };

            il.Append(tryHandler.TryStart);
            il.Append(catchHandler.TryStart);

            return tryHandler.TryStart;
        }

        public static void BeginCatchBlock(this ILProcessor il)
        {
            catchHandler.TryEnd = Instruction.Create(OpCodes.Nop);
            catchHandler.HandlerStart = catchHandler.TryEnd;

            il.Append(catchHandler.TryEnd);
        }

        public static void BeginFinallyBlock(this ILProcessor il)
        {
            if(catchHandler.TryEnd != null)
            {
                catchHandler.HandlerEnd = Instruction.Create(OpCodes.Leave, endHandler);
                il.Emit(OpCodes.Leave, catchHandler.HandlerEnd);

                il.Append(catchHandler.HandlerEnd);
            }

            tryHandler.TryEnd = Instruction.Create(OpCodes.Nop);
            tryHandler.HandlerStart = tryHandler.TryEnd;
            
            il.Append(tryHandler.TryEnd);
        }

        public static void EndExceptionBlock(this ILProcessor il)
        {
            if (tryHandler.HandlerStart != null)
            {
                tryHandler.HandlerEnd = Instruction.Create(OpCodes.Nop);

                il.Emit(OpCodes.Endfinally);
                il.Append(tryHandler.HandlerEnd);
            }

            il.Append(endHandler);

            il.Body.ExceptionHandlers.Add(catchHandler);
            il.Body.ExceptionHandlers.Add(tryHandler);
        }

        public static VariableDefinition DeclareLocal(this ILProcessor il, Type type)
        {
            var variable = new VariableDefinition(module.Import(type));
            il.Body.Variables.Add(variable);

            if (!il.Body.InitLocals)
            {
                il.Body.InitLocals = true;
            }

            return variable;
        }

        public static Instruction DefineLabel(this ILProcessor il)
        {
            return Instruction.Create(OpCodes.Nop);
        }

        public static void MarkLabel(this ILProcessor il, Instruction label)
        {
            il.Append(label);
        }

        //
        // Summary:
        //     Puts the specified instruction onto the Microsoft intermediate language (MSIL)
        //     stream followed by the metadata token for the given type.
        //
        // Parameters:
        //   opcode:
        //     The MSIL instruction to be put onto the stream.
        //
        //   cls:
        //     A Type.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     cls is null.
        public static void Emit(this ILProcessor il, OpCode opcode, Type cls)
        {
            il.Emit(opcode, module.Import(cls));
        }

        //
        // Summary:
        //     Puts the specified instruction onto the Microsoft intermediate language (MSIL)
        //     stream followed by the metadata token for the given method.
        //
        // Parameters:
        //   opcode:
        //     The MSIL instruction to be emitted onto the stream.
        //
        //   meth:
        //     A MethodInfo representing a method.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     meth is null.
        //
        //   T:System.NotSupportedException:
        //     meth is a generic method for which the System.Reflection.MethodInfo.IsGenericMethodDefinition
        //     property is false.
        public static void Emit(this ILProcessor il, OpCode opcode, System.Reflection.MethodBase meth)
        {
            il.Emit(opcode, module.Import(meth));
        }

        //
        // Summary:
        //     Puts the specified instruction and metadata token for the specified constructor
        //     onto the Microsoft intermediate language (MSIL) stream of instructions.
        //
        // Parameters:
        //   opcode:
        //     The MSIL instruction to be emitted onto the stream.
        //
        //   con:
        //     A ConstructorInfo representing a constructor.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     con is null. This exception is new in the .NET Framework 4.
        public static void Emit(this ILProcessor il, OpCode opcode, System.Reflection.ConstructorInfo con)
        {
            il.Emit(opcode, module.Import(con));
        }

        //
        // Summary:
        //     Puts the specified instruction and metadata token for the specified field onto
        //     the Microsoft intermediate language (MSIL) stream of instructions.
        //
        // Parameters:
        //   opcode:
        //     The MSIL instruction to be emitted onto the stream.
        //
        //   field:
        //     A FieldInfo representing a field.
        public static void Emit(this ILProcessor il, OpCode opcode, System.Reflection.FieldInfo field)
        {
            il.Emit(opcode, module.Import(field));
        }
    }
}
