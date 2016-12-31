using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using RuntimeHelpers = System.Runtime.CompilerServices.RuntimeHelpers;
using System.Runtime.InteropServices;
using System.Reflection.Emit;
using System.Linq;
using System.Collections.Concurrent;
using System.Diagnostics.SymbolStore;
using System.Text.RegularExpressions;
using Microsoft.Cci.Pdb;
using System.Runtime.CompilerServices;

namespace RAspect
{
    public unsafe static class ILWeaverUtil
    {
        /// <summary>
        /// Lock Object
        /// </summary>
        private static readonly object LockObject = new object();

        /// <summary>
        /// Fast Method Types
        /// </summary>
        private static ConcurrentDictionary<string, MethodInfo> FastMethodTypes = new ConcurrentDictionary<string, MethodInfo>();

        /// <summary>
        /// Fast Invokes
        /// </summary>
        public static ConcurrentDictionary<string, Delegate> FastInvokes = new ConcurrentDictionary<string, Delegate>();

        /// <summary>
        /// Compiler Generated Replacement Fields
        /// </summary>
        private static ConcurrentDictionary<string, FieldInfo> CompilerGeneratedFields = new ConcurrentDictionary<string, FieldInfo>();

        /// <summary>
        /// Generated Instance Fields
        /// </summary>
        private static ConcurrentDictionary<string, FieldInfo> GeneratedInstanceFields = new ConcurrentDictionary<string, FieldInfo>();

        /// <summary>
        /// Assembly PDB Information
        /// </summary>
        private static ConcurrentDictionary<string, Dictionary<uint, PdbFunction>> AssemblyPDBs = new ConcurrentDictionary<string, Dictionary<uint, PdbFunction>>();

        /// <summary>
        /// Symbol Writers for Debugging
        /// </summary>
        private static ConcurrentDictionary<string, ISymbolDocumentWriter> SymbolDocuments = new ConcurrentDictionary<string, ISymbolDocumentWriter>();

        /// <summary>
        /// Store Local OpCodes
        /// </summary>
        private static OpCode[] StoreLocalOpCodes = new OpCode[] { OpCodes.Stloc_0, OpCodes.Stloc_1, OpCodes.Stloc_2, OpCodes.Stloc_3 };

        /// <summary>
        /// Store Local OpCodes
        /// </summary>
        private static OpCode[] LoadLocalOpCodes = new OpCode[] { OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3 };

        /// <summary>
        /// Net Version 2 Above
        /// </summary>
        public static readonly Version Net20SP2 = new Version(2, 0, 50727, 3053);

        /// <summary>
        /// Get MSIL Label in preparation of MSIL Copying
        /// </summary>
        /// <param name="meth">Method</param>
        /// <param name="il">ILGenerator</param>
        /// <returns>Dictionary</returns>
        private static Dictionary<int, ILLabelInfo> GetLabels(MethodInfo meth, ILGenerator il)
        {
            var reader = new ILReader(meth);
            var labels = new Dictionary<int, ILLabelInfo>();
            while (reader.Read())
            {
                var current = reader.Current;

                if (current.Instruction.Name.Contains("leave"))
                {
                    continue;
                }

                if (current.Data != null)
                {
                    if (current.Instruction.FlowControl == FlowControl.Branch ||
                        current.Instruction.FlowControl == FlowControl.Cond_Branch)
                    {
                        var index = current.Index;
                        labels[index] = new ILLabelInfo { Label = il.DefineLabel(), Index = (int)current.Data  };
                    }
                }
            }

            return labels;
        }

        /// <summary>
        /// Copy MSIL from method into current ILGenerator
        /// </summary>
        /// <param name="type">TypeBuilder</param>
        /// <param name="meth">Method to copy MSIL from</param>
        /// <param name="il">ILGenerator</param>
        /// <param name="module">Module</param>
        /// <param name="aspects">Aspects</param>
        /// <param name="methodContext">Method Context</param>
        /// <param name="sil">Static ILGenerator</param>
        internal static void CopyIL(TypeBuilder type, MethodInfo meth, ILGenerator il, Module module, List<AspectBase> aspects, LocalBuilder methodContext, ILGenerator sil = null)
        {
            var methodName = meth.Name;
            var body = meth.GetMethodBody();

            var isEvent = (methodName.StartsWith("add_") || methodName.StartsWith("remove")) && meth.DeclaringType.GetEvent(methodName.Split('_')[0], ILWeaver.NonPublicBinding) != null;

            var labels = GetLabels(meth, il);
            var locals = new List<LocalBuilder>();
            var exceptions = body.ExceptionHandlingClauses;
            var methodPdbInfo = GetMethodPDBInfo(meth);
            var pdbSource = methodPdbInfo != null ? methodPdbInfo.Item1 : null;
            var pdbLines = methodPdbInfo != null ? methodPdbInfo.Item2 : null;
            var pdbLocals = methodPdbInfo != null ? methodPdbInfo.Item3 : null;

            var moduleBuilder = module as ModuleBuilder;
            ISymbolDocumentWriter writer = GetSymbolDocumentWriter(pdbSource, moduleBuilder);
            LocalBuilder adrLocal = null;
            FieldInfo adrField = null;
            FieldInfo eventField = null;
            var reader = new ILReader(meth);

            foreach (var local in body.LocalVariables)
            {
                var localVariable = il.DeclareLocal(local.LocalType, local.IsPinned);
#if DEBUG
                var localIndex = local.LocalIndex;
                var localName = string.Empty;
                if (pdbLocals == null || !pdbLocals.TryGetValue(localIndex, out localName))
                {
                    localName = string.Concat("local_", localIndex);
                }

                localVariable.SetLocalSymInfo(localName);
#endif
                locals.Add(localVariable);

            }

            while (reader.Read())
            {
                var current = reader.Current;
                var currentIndex = current.Index;
                var instruction = current.Instruction;
                var instructionValue = instruction.Value;
                var pdbLine = pdbLines != null ? pdbLines.FirstOrDefault(x => x.offset == currentIndex) : default(PdbLine);

                if (pdbLine.lineBegin > 0)
                {
                    AddDebugSequencePoint(il, writer, (int)pdbLine.lineBegin, pdbLine.colBegin, (int)pdbLine.lineEnd, pdbLine.colEnd);
                }

                if (instruction.Name.Contains("leave"))
                {
                    continue;
                }

                var offsetLabel = labels.FirstOrDefault(x => x.Value.Index == currentIndex);//labels.FirstOrDefault(x => x.Value.Index >= current.Index - 1 && x.Value.Index <= current.Index && !x.Value.Marked);

                var @try = exceptions.FirstOrDefault(x => x.TryOffset == currentIndex && (x.Flags == ExceptionHandlingClauseOptions.Clause || x.Flags == ExceptionHandlingClauseOptions.Finally));

                var @catch = exceptions.FirstOrDefault(x => x.HandlerOffset == currentIndex && x.Flags == ExceptionHandlingClauseOptions.Clause);

                var @finally = exceptions.FirstOrDefault(x => x.HandlerOffset == currentIndex && x.Flags == ExceptionHandlingClauseOptions.Finally);

                var endTry = exceptions.FirstOrDefault(x => x.Flags == ExceptionHandlingClauseOptions.Finally && (x.HandlerOffset + x.HandlerLength) == currentIndex) ??
                    exceptions.FirstOrDefault(x => x.Flags == ExceptionHandlingClauseOptions.Clause && (x.HandlerOffset + x.HandlerLength) == currentIndex);

                if (@try != null)
                {
                    il.BeginExceptionBlock();
                }

                if (@catch != null)
                {
                    il.BeginCatchBlock(@catch.CatchType);
                }

                if (@finally != null)
                {
                    il.BeginFinallyBlock();
                }

                if (endTry != null)
                {
                    il.EndExceptionBlock();
                }

                if (offsetLabel.Value != null)
                {
                    il.MarkLabel(offsetLabel.Value.Label);
                    offsetLabel.Value.Marked = true;
                }

                if (current.Data != null)
                {
                    ILLabelInfo labelInfo = null;
                    if (labels.TryGetValue(currentIndex, out labelInfo))
                    {
                        il.Emit(instruction, labelInfo.Label);
                        if (labelInfo.Index == currentIndex + 1)
                        {
                            il.MarkLabel(labelInfo.Label);
                            labelInfo.Marked = true;
                        }
                    }
                    else
                    {
                        //Resolve emit method based on data type
                        var data = current.Data;
                        var dataType = data != null ? data.GetType() : null;
                        var method = dataType != null ? typeof(ILGenerator).GetMethod("Emit", new Type[] { typeof(OpCode), dataType }) :
                            typeof(ILGenerator).GetMethod("Emit", new Type[] { typeof(OpCode), typeof(byte) });

                        if (method != null)
                        {
                            var dataMethod = data as MethodInfo;
                            var dataField = data as FieldInfo;
                            var getter = dataField != null && instruction.Name.StartsWith("ld", StringComparison.OrdinalIgnoreCase);

                            if(dataField != null && dataField.FieldType.IsSubclassOf(typeof(Delegate)))
                            {
                                eventField = dataField;
                            }

                            if (dataField != null && (!dataField.IsPublic || !dataField.DeclaringType.IsPublic))
                            {
                                var isFieldStatic = dataField.IsStatic;
                                var dataFieldType = dataField.FieldType;
                                
                                var isCompilerGenerated = dataField.DeclaringType.GetCustomAttribute<CompilerGeneratedAttribute>() != null;

                                if (isCompilerGenerated)
                                {
                                    FieldInfo rField = null;
                                    var key = string.Concat(type.FullName, dataField.Name);

                                    if (!CompilerGeneratedFields.TryGetValue(key, out rField))
                                    {
                                        var attr = dataField.Attributes;
                                        var sameAsCurrent = false;
                                        if ((attr & FieldAttributes.InitOnly) == FieldAttributes.InitOnly)
                                        {
                                            attr &= ~FieldAttributes.InitOnly;
                                            dataFieldType = type;
                                            sameAsCurrent = true;
                                        }
                                        CompilerGeneratedFields[key] = rField = type.DefineField(dataField.Name, dataFieldType, attr);

                                        if (sameAsCurrent)
                                        {
                                            if (!isFieldStatic)
                                            {
                                                il.Emit(OpCodes.Ldarg_0);
                                            }

                                            il.Emit(OpCodes.Ldarg_0);
                                            il.Emit(isFieldStatic ? OpCodes.Stsfld : OpCodes.Stfld, rField);

                                            var originalField = type.DefineField("<>a_c", typeof(object), attr);

                                            GeneratedInstanceFields[type.FullName] = originalField;

                                            if (!isFieldStatic)
                                            {
                                                il.Emit(OpCodes.Ldarg_0);
                                            }

                                            InvokeNonPublicMember(type, il, dataField, isFieldStatic, getter, sil);

                                            il.Emit(isFieldStatic ? OpCodes.Stsfld : OpCodes.Stfld, originalField);
                                        }
                                    }

                                    if (getter)
                                    {
                                        il.Emit(isFieldStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, rField);
                                    }
                                    else
                                    {
                                        il.Emit(isFieldStatic ? OpCodes.Stsfld : OpCodes.Stfld, rField);
                                    }
                                }
                                else
                                {
                                    if (getter)
                                    {
                                        var isAddressLoad = instructionValue == OpCodes.Ldflda.Value;
                                        adrLocal = isAddressLoad ? il.DeclareLocal(dataFieldType) : null;

                                        InvokeNonPublicMember(type, il, dataField, isFieldStatic, getter, sil);
                                        WeaveField(il, aspects, dataField, getter, methodContext);

                                        if (isAddressLoad)
                                        {
                                            adrField = dataField;
                                            il.Emit(OpCodes.Stloc, adrLocal);
                                            il.Emit(OpCodes.Ldloca, adrLocal);
                                        }
                                    }
                                    else
                                    {
                                        WeaveField(il, aspects, dataField, getter, methodContext);
                                        InvokeNonPublicMember(type, il, dataField, isFieldStatic, getter, sil);
                                    }
                                }
                            }
                            else if (dataMethod != null && (!dataMethod.IsPublic || !dataMethod.DeclaringType.IsPublic))
                            {
                                WeaveEventInvoke(il, aspects, methodContext, dataMethod, eventField);

                                if (instructionValue == OpCodes.Ldftn.Value)
                                {
                                    var hasCompilerGenerated = dataMethod.GetCustomAttribute<CompilerGeneratedAttribute>() != null;

                                    il.Emit(OpCodes.Ldftn, GenerateFunctionCallForDelegate(type, dataMethod, useUnderlyingType: !hasCompilerGenerated, sil: sil));
                                }
                                else
                                {
                                    InvokeNonPublicMethod(type, il, dataMethod, sil: sil);
                                }
                            }
                            else if (instructionValue == OpCodes.Newobj.Value)
                            {
                                var ctor = data as ConstructorInfo;
                                var nonPublic = !ctor.DeclaringType.IsPublic || !ctor.IsPublic;

                                if (nonPublic)
                                {
                                    InvokeNonPublicMethod(type, il, ctor);
                                }
                                else
                                {
                                    il.Emit(OpCodes.Newobj, ctor);
                                }
                            }
                            else
                            {
                                if (dataField != null)
                                {
                                    if (getter)
                                    {
                                        method.Invoke(il, new object[] { instruction, data });
                                        WeaveField(il, aspects, dataField, getter, methodContext);
                                    }
                                    else
                                    {
                                        WeaveField(il, aspects, dataField, getter, methodContext);
                                        method.Invoke(il, new object[] { instruction, data });
                                    }
                                }
                                else if(dataMethod != null)
                                {
                                    WeaveEventInvoke(il, aspects, methodContext, dataMethod, eventField);
                                    method.Invoke(il, new object[] { instruction, data });
                                }
                                else
                                    method.Invoke(il, new object[] { instruction, data });
                            }
                        }
                        else if (instructionValue == OpCodes.Ldc_I4_S.Value)
                        {
                            il.Emit(instruction, (int)current.Data);
                        }
                        else
                        {
                            il.Emit(instruction, (byte)current.Data);
                        }
                    }
                }
                else
                {
                    if (!meth.IsStatic && instructionValue == OpCodes.Ldarg_0.Value)
                    {
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Castclass, meth.DeclaringType);//Might Remove
                    }
                    else if (StoreLocalOpCodes.Contains(instruction))
                    {
                        int index = GetInstructionIndex(current);
                        il.Emit(OpCodes.Stloc, locals[index]);
                    }
                    else if (LoadLocalOpCodes.Contains(instruction))
                    {
                        int index = GetInstructionIndex(current);
                        il.Emit(OpCodes.Ldloc, locals[index]);
                    }
                    else
                    {
                        if(instructionValue == OpCodes.Ret.Value)
                        {
                            if (adrLocal != null && adrField != null)
                            {
                                var isFieldStatic = adrField.IsStatic;
                                if (!isFieldStatic)
                                {
                                    il.Emit(OpCodes.Ldarg_0);
                                    il.Emit(OpCodes.Castclass, meth.DeclaringType);//Might Remove
                                }

                                il.Emit(OpCodes.Ldloc, adrLocal);

                                InvokeNonPublicMember(type, il, adrField, isFieldStatic, false, sil);
                            }
                        }

                        il.Emit(instruction);
                    }
                }
            }
        }

        /// <summary>
        /// Weave event invoke
        /// </summary>
        /// <param name="il">IL Generator</param>
        /// <param name="aspects">Aspects</param>
        /// <param name="methodContext">Method Context</param>
        /// <param name="method">Method</param>
        /// <param name="field">Field</param>
        private static void WeaveEventInvoke(ILGenerator il, List<AspectBase> aspects, LocalBuilder methodContext, MethodInfo method, FieldInfo field)
        {
            var isEventInvoke = method.Name.Equals("Invoke") && method.DeclaringType.IsSubclassOf(typeof(Delegate));

            if (isEventInvoke)
            {
                foreach (var aspect in aspects)
                {
                    var del = aspect.OnAspectBlockInvokeEvent;
                    if (del != null)
                    {
                        del(il, method, field, methodContext);
                    }
                }
            }
        }

        /// <summary>
        /// Weave Field for aspect
        /// </summary>
        /// <param name="il">IL Generator</param>
        /// <param name="aspects">Aspects</param>
        /// <param name="field">Field</param>
        /// <param name="getter">Is Getter Operation</param>
        /// <param name="methodContext">Method Context</param>
        private static void WeaveField(ILGenerator il, List<AspectBase> aspects, FieldInfo field, bool getter, LocalBuilder methodContext)
        {
            if (field == null)
                return;

            foreach (var aspect in aspects)
            {
                var del = getter ? aspect.OnAspectBlockGetField : aspect.OnAspectBlockSetField;
                if (del != null)
                {
                    del(il, field, methodContext);
                }
            }
        }

        /// <summary>
        /// Get Symbol Document Writer for given source
        /// </summary>
        /// <param name="pdbSource">Source File</param>
        /// <param name="moduleBuilder">Module</param>
        /// <returns>ISymbolDocumentWriter</returns>
        internal static ISymbolDocumentWriter GetSymbolDocumentWriter(string pdbSource, ModuleBuilder moduleBuilder)
        {
            ISymbolDocumentWriter writer = null;

#if DEBUG
            if (moduleBuilder != null)
            {
                if(!string.IsNullOrWhiteSpace(pdbSource) && !SymbolDocuments.TryGetValue(pdbSource, out writer))
                {
                    writer = SymbolDocuments[pdbSource] = moduleBuilder.DefineDocument(pdbSource, SymLanguageType.CSharp, SymLanguageVendor.Microsoft, SymDocumentType.Text);
                }
            }
#endif
            return writer;
        }

        /// <summary>
        /// Convert current instance in stack to type if possible
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="il">ILGenerator</param>
        private static void ConvertInstanceToType(Type type, ILGenerator il)
        {
            if (type.IsPublic)
            {
                il.Emit(OpCodes.Isinst, type);
            }
        }

        /// <summary>
        /// Invoke non public member
        /// </summary>
        /// <param name="typeBuilder">Type Builder</param>
        /// <param name="il">ILGenerator</param>
        /// <param name="field">Field</param>
        /// <param name="isFieldStatic">IsStatic</param>
        /// <param name="getter">Is Getter</param>
        /// <param name="sil">Static ILGenerator</param>
        private static void InvokeNonPublicMember(TypeBuilder typeBuilder, ILGenerator il, FieldInfo field, bool isFieldStatic, bool getter, ILGenerator sil)
        {
            var key = string.Concat(field.Name, field.DeclaringType.FullName, getter ? "Get" : "Set").Replace(".", string.Empty);
            var nonPublicMember = FastMethodTypes.GetOrAdd(key, _ => {
                
                var isStatic = field.IsStatic;
                var declaringType = field.DeclaringType;
                var fieldType = field.FieldType;

                var parameterTypes = new List<Type>();

                if (getter)
                {
                    if (!isStatic)
                    {
                        parameterTypes.Add(declaringType);
                    }
                }
                else
                {
                    if (isStatic)
                    {
                        parameterTypes.Add(fieldType);
                    }
                    else
                    {
                        parameterTypes.AddRange(new[] { declaringType, fieldType });
                    }
                }

                var paramTypes = parameterTypes.ToArray();
                var delType = !isStatic ? (getter ? typeof(Func<,>) : typeof(Action<,>)) : (getter ? typeof(Func<>) : typeof(Action<>));

                var returnType = getter ? fieldType : typeof(void);
                var meth = new DynamicMethod(key, returnType, paramTypes, true);

                
                var mil = meth.GetILGenerator();

                if (getter)
                {
                    if (isStatic)
                    {
                        mil.Emit(OpCodes.Ldsfld, field);
                    }
                    else
                    {
                        mil.Emit(OpCodes.Ldarg_0);
                        mil.Emit(OpCodes.Ldfld, field);
                    }
                }
                else
                {
                    if (isStatic)
                    {
                        mil.Emit(OpCodes.Ldarg_0);
                        mil.Emit(OpCodes.Stsfld, field);
                    }
                    else
                    {
                        mil.Emit(OpCodes.Ldarg_0);
                        mil.Emit(OpCodes.Ldarg_1);
                        mil.Emit(OpCodes.Stfld, field);
                    }
                }

                mil.Emit(OpCodes.Ret);
                
                if (getter)
                {
                    parameterTypes.Add(fieldType);
                }

                delType = delType.MakeGenericType(parameterTypes.ToArray());

                var delFieldType = declaringType.IsPublic ? delType : typeof(Delegate);

                var isDelFieldPrivate = delFieldType == typeof(Delegate);

                var delField = typeBuilder.DefineField(key + "_<del>", delFieldType, FieldAttributes.Static | FieldAttributes.Private);
                FieldBuilder delFastField = null;

                FastInvokes[key] = meth.CreateDelegate(delType);

                sil.Emit(OpCodes.Ldsfld, typeof(ILWeaverUtil).GetField("FastInvokes"));
                sil.Emit(OpCodes.Ldstr, key);
                sil.Emit(OpCodes.Callvirt, FastInvokes.GetType().GetMethod("get_Item"));
                sil.Emit(OpCodes.Isinst, delField.FieldType);
                sil.Emit(OpCodes.Stsfld, delField);

                #region
                var mth = isDelFieldPrivate ? new DynamicMethod(key + "PrivateFastInvoke", returnType == typeof(void) ? returnType : typeof(object), new[] {typeof(object), typeof(object[]) }, true) : null;
                var fastKey = isDelFieldPrivate ? key + "Fast" : null;

                if (mth != null)
                {
                    var mthil = mth.GetILGenerator();

                    mthil.Emit(OpCodes.Ldarg_0);
                    mthil.Emit(OpCodes.Isinst, delType);

                    for (var i = 0; i < paramTypes.Length; i++)
                    {
                        var t = paramTypes[i];
                        mthil.Emit(OpCodes.Ldarg_1);
                        mthil.Emit(OpCodes.Ldc_I4, i);
                        mthil.Emit(OpCodes.Ldelem_Ref);

                        if (t.IsValueType)
                        {
                            mthil.Emit(OpCodes.Unbox_Any, t);
                        }
                        else
                        {
                            mthil.Emit(OpCodes.Isinst, t);
                        }
                    }

                    mthil.Emit(OpCodes.Callvirt, delType.GetMethod("Invoke"));
                    if (returnType.IsValueType)
                    {
                        mthil.Emit(OpCodes.Box, returnType);
                    }

                    mthil.Emit(OpCodes.Ret);
                    
                    var fastDelType = returnType == typeof(void) ? typeof(Action<object, object[]>) : typeof(Func<object, object[], object>);
                    delFastField = typeBuilder.DefineField(fastKey + "_<del>", fastDelType, FieldAttributes.Static | FieldAttributes.Private);

                    FastInvokes[fastKey] = mth.CreateDelegate(fastDelType);

                    sil.Emit(OpCodes.Ldsfld, typeof(ILWeaverUtil).GetField("FastInvokes"));
                    sil.Emit(OpCodes.Ldstr, fastKey);
                    sil.Emit(OpCodes.Callvirt, FastInvokes.GetType().GetMethod("get_Item"));
                    sil.Emit(OpCodes.Isinst, delFastField.FieldType);
                    sil.Emit(OpCodes.Stsfld, delFastField);
                }

                #endregion

                var invokeReturnType = mth != null ? typeof(object) : meth.ReturnType;
                var typeInvoke = typeBuilder.DefineMethod(key, MethodAttributes.Public | MethodAttributes.Static, invokeReturnType, paramTypes);

                var til = typeInvoke.GetILGenerator();
                var tilType = (delFastField ?? delField).FieldType;
                var argLocal = isDelFieldPrivate ? til.DeclareLocal(typeof(object[])) : null;

                if(argLocal != null)
                {
                    til.Emit(OpCodes.Ldc_I4, paramTypes.Length);
                    til.Emit(OpCodes.Newarr, typeof(object));
                    til.Emit(OpCodes.Stloc, argLocal);
                }

                til.Emit(OpCodes.Ldsfld, delFastField ?? delField);

                if (argLocal != null)
                {
                    til.Emit(OpCodes.Ldsfld, delField);
                }

                for (var i = 0; i < paramTypes.Length; i++)
                {
                    var paramType = paramTypes[i];
                    if (isDelFieldPrivate)
                    {
                        til.Emit(OpCodes.Ldloc, argLocal);
                        til.Emit(OpCodes.Ldc_I4, i);
                        til.Emit(OpCodes.Ldarg, i);
                        if (paramType.IsValueType)
                        {
                            til.Emit(OpCodes.Box, paramType);
                        }

                        til.Emit(OpCodes.Stelem_Ref, typeof(object));
                    }
                    else
                    {
                        til.Emit(OpCodes.Ldarg, i);
                    }
                }

                if (argLocal != null)
                {
                    til.Emit(OpCodes.Ldloc, argLocal);
                }

                til.Emit(OpCodes.Callvirt, tilType.GetMethod("Invoke"));

                if (getter && mth != null && invokeReturnType != typeof(object))
                {
                    if (fieldType.IsValueType)
                    {
                        til.Emit(OpCodes.Unbox_Any, fieldType);
                    }
                    else
                    {
                        til.Emit(OpCodes.Isinst, fieldType);
                    }
                }

                til.Emit(OpCodes.Ret);

                return typeInvoke;
            });

            il.Emit(OpCodes.Call, nonPublicMember);
        }

        /// <summary>
        /// Invoke non public method
        /// </summary>
        /// <param name="typeBuilder">Type Builder</param>
        /// <param name="il">ILGenerator</param>
        /// <param name="method">MethodInfo</param>
        /// <param name="type">Type</param>
        /// <param name="sil">Static ILGenerator</param>
        private static void InvokeNonPublicMethod(TypeBuilder typeBuilder, ILGenerator il, MethodBase method, Type type = null, ILGenerator sil = null)
        {
            var methodInfo = method as MethodInfo;
            var ctor = method as ConstructorInfo;
            var declaringType = method.DeclaringType;
            var methodType = methodInfo != null ? methodInfo.ReturnType : declaringType;
            var parameters = method.GetParameters().Select(x => x.ParameterType).ToArray();
            var isInstance = !method.IsStatic && !method.IsConstructor;
            var key = (method.Name + declaringType.FullName + "Invoke").Replace(".", string.Empty);
            

            var nonPublicMethod = FastMethodTypes.GetOrAdd(key, _ => {
                var isVoid = methodType == typeof(void);

                var @params = new List<Type>();

                if (isInstance)
                {
                    @params.Add(declaringType);
                }

                @params.AddRange(parameters);

                var paramArray = @params.ToArray();
                var delType = methodType == typeof(void) ? Type.GetType("System.Action`" + @params.Count) :
                Type.GetType("System.Func`" + (@params.Count + 1));


                if (!isVoid)
                {
                    @params.Add(methodType);
                }

                delType = delType.MakeGenericType(@params.ToArray());

                var delFieldType = declaringType.IsPublic ? delType : typeof(Delegate);

                var isDelFieldPrivate = delFieldType == typeof(Delegate);

                var delField = typeBuilder.DefineField(key + "_<del>", delFieldType, FieldAttributes.Static | FieldAttributes.Private);
                FieldBuilder delFastField = null;

                var meth = new DynamicMethod(key + "FastInvoke", methodType, paramArray, true);

                var mil = meth.GetILGenerator();

                for (var i = 0; i < paramArray.Length; i++)
                {
                    mil.Emit(OpCodes.Ldarg, i);
                }

                if (methodInfo != null)
                {
                    mil.Emit(isInstance ? OpCodes.Callvirt : OpCodes.Call, methodInfo);
                }
                else
                {
                    mil.Emit(OpCodes.Newobj, ctor);
                }

                mil.Emit(OpCodes.Ret);

                FastInvokes[key] = meth.CreateDelegate(delType);

                sil.Emit(OpCodes.Ldsfld, typeof(ILWeaverUtil).GetField("FastInvokes"));
                sil.Emit(OpCodes.Ldstr, key);
                sil.Emit(OpCodes.Callvirt, FastInvokes.GetType().GetMethod("get_Item"));
                sil.Emit(OpCodes.Isinst, delField.FieldType);
                sil.Emit(OpCodes.Stsfld, delField);


                #region
                var mth = isDelFieldPrivate ? new DynamicMethod(key + "PrivateFastInvoke", methodType == typeof(void) ? methodType : typeof(object), new[] {typeof(object), typeof(object[]) }, true) : null;
                var fastKey = isDelFieldPrivate ? key + "Fast" : null;

                if (mth != null)
                {
                    var mthil = mth.GetILGenerator();
                    
                    mthil.Emit(OpCodes.Ldarg_0);
                    mthil.Emit(OpCodes.Isinst, delType);

                    for (var i = 0; i < paramArray.Length; i++)
                    {
                        var t = paramArray[i];
                        mthil.Emit(OpCodes.Ldarg_1);
                        mthil.Emit(OpCodes.Ldc_I4, i);
                        mthil.Emit(OpCodes.Ldelem_Ref);

                        if (t.IsValueType)
                        {
                            mthil.Emit(OpCodes.Unbox_Any, t);
                        }
                        else
                        {
                            mthil.Emit(OpCodes.Isinst, t);
                        }
                    }

                    mthil.Emit(OpCodes.Callvirt, delType.GetMethod("Invoke"));
                    if (methodType.IsValueType)
                    {
                        mthil.Emit(OpCodes.Box, methodType);
                    }

                    mthil.Emit(OpCodes.Ret);

                    var fastDelType = methodType == typeof(void) ? typeof(Action<object, object[]>) : typeof(Func<object, object[], object>);
                    delFastField = typeBuilder.DefineField(fastKey + "_<del>", fastDelType, FieldAttributes.Static | FieldAttributes.Private);

                    FastInvokes[fastKey] = mth.CreateDelegate(fastDelType);

                    sil.Emit(OpCodes.Ldsfld, typeof(ILWeaverUtil).GetField("FastInvokes"));
                    sil.Emit(OpCodes.Ldstr, fastKey);
                    sil.Emit(OpCodes.Callvirt, FastInvokes.GetType().GetMethod("get_Item"));
                    sil.Emit(OpCodes.Isinst, delFastField.FieldType);
                    sil.Emit(OpCodes.Stsfld, delFastField);
                }

                #endregion

                var typeInvokeParameters = new List<Type>();

                for(var i = 0; i < paramArray.Length; i++)
                {
                    var paramType = paramArray[i];

                    if(i == 0 && isInstance)
                    {
                        paramType = typeof(object);
                    }

                    typeInvokeParameters.Add(paramType);
                }

                var typeInvoke = typeBuilder.DefineMethod(key, MethodAttributes.Public | MethodAttributes.Static, meth.ReturnType, typeInvokeParameters.ToArray());

                var til = typeInvoke.GetILGenerator();
                var tilType = (delFastField ?? delField).FieldType;
                var argLocal = isDelFieldPrivate ? til.DeclareLocal(typeof(object[])) : null;

                til.Emit(OpCodes.Ldsfld, delFastField ?? delField);

                if(argLocal != null)
                {
                    til.Emit(OpCodes.Ldsfld, delField);
                }

                if (argLocal != null)
                {
                    til.Emit(OpCodes.Ldc_I4, paramArray.Length);
                    til.Emit(OpCodes.Newarr, typeof(object));
                    til.Emit(OpCodes.Stloc, argLocal);
                }

                for (var i = 0; i < paramArray.Length; i++)
                {
                    var paramType = paramArray[i];
                    if (isDelFieldPrivate)
                    {
                        til.Emit(OpCodes.Ldloc, argLocal);
                        til.Emit(OpCodes.Ldc_I4, i);
                        til.Emit(OpCodes.Ldarg, i);
                        
                        if (paramType.IsValueType)
                        {
                            til.Emit(OpCodes.Box, paramType);
                        }

                        til.Emit(OpCodes.Stelem_Ref);
                    }
                    else
                    {
                        til.Emit(OpCodes.Ldarg, i);
                    }
                }
                
                if (argLocal != null)
                {
                    til.Emit(OpCodes.Ldloc, argLocal);
                }

                til.Emit(OpCodes.Callvirt, tilType.GetMethod("Invoke"));

                if (!isVoid)
                {
                    if (methodType.IsValueType)
                    {
                        til.Emit(OpCodes.Unbox_Any, methodType);
                    }
                    else
                    {
                        til.Emit(OpCodes.Isinst, methodType);
                    }
                }

                til.Emit(OpCodes.Ret);

                return typeInvoke;
            });

            il.Emit(OpCodes.Call, nonPublicMethod);
        }

        /// <summary>
        /// Emit retrieval of method handle for a given methodInfo
        /// </summary>
        /// <param name="il"></param>
        /// <param name="methodInfo"></param>
        private static void EmitGetMethodHandle(ILGenerator il, MethodInfo methodInfo)
        {
            var declaringType = methodInfo.DeclaringType;
            if (methodInfo.IsGenericMethodDefinition || methodInfo.IsGenericMethod || declaringType.IsGenericType)
            {
                il.Emit(OpCodes.Ldtoken, methodInfo);
                il.Emit(OpCodes.Ldtoken, declaringType);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", new Type[] { typeof(RuntimeTypeHandle) }));
                il.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle) }));
            }
            else
            {
                il.Emit(OpCodes.Ldtoken, methodInfo);
                il.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle) }));
            }
        }

        /// <summary>
        /// Invoke delegate method generated by compiler
        /// </summary>
        /// <param name="type">TypeBuilder</param>
        /// <param name="method">MethodInfo</param>
        /// <param name="useUnderlyingType">UseUnderlyingType</param>
        /// <param name="sil">Static ILGenerator</param>
        /// <returns></returns>
        private static MethodBuilder GenerateFunctionCallForDelegate(TypeBuilder type, MethodInfo method, bool useUnderlyingType = true, ILGenerator sil = null)
        {
            var parameters = method.GetParameters().Select(x => x.ParameterType).ToArray();
            var meth = type.DefineMethod(string.Concat(method.Name, "DelInvoke"), method.Attributes, method.CallingConvention, method.ReturnType,
                parameters);

            var il = meth.GetILGenerator();

            var declaringType = method.DeclaringType;
            var isStatic = method.IsStatic;
            var offset = isStatic ? 0 : 1;

            var field = default(FieldInfo);

            if (type != null)
            {
                GeneratedInstanceFields.TryGetValue(type.FullName, out field);
            }

            var overrideDeclaringType = field != null ? type : declaringType;

            if (!isStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                ConvertInstanceToType(declaringType, il);
                if (field != null)
                {
                    il.Emit(OpCodes.Isinst, overrideDeclaringType);
                    il.Emit(OpCodes.Ldfld, field);
                    //il.Emit(OpCodes.Isinst, declaringType);
                }
            }

            for (var i = 0; i < parameters.Length; i++)
            {
                var index = i + offset;
                il.Emit(OpCodes.Ldarg, index);
            }

            InvokeNonPublicMethod(type, il, method, useUnderlyingType ? type : null, sil: sil);   
            
            il.Emit(OpCodes.Ret);

            return meth;
        }

        /// <summary>
        /// Add Debug Sequence Point to enable debugging
        /// </summary>
        /// <param name="il">ILGenerator</param>
        /// <param name="document">SymbolDocumentWriter</param>
        /// <param name="startLine">StartLine</param>
        /// <param name="startColumn">StartColumn</param>
        /// <param name="endLine">EndLine</param>
        /// <param name="endColumn">EndColumn</param>
        private static void AddDebugSequencePoint(ILGenerator il, ISymbolDocumentWriter document, int startLine, int startColumn, int endLine, int endColumn)
        {
#if DEBUG
            if (document != null)
            {
                il.MarkSequencePoint(document, startLine, startColumn, endLine, endColumn);
            }
#endif
        }

        /// <summary>
        /// Get method pdb information
        /// </summary>
        /// <param name="method">Method</param>
        /// <returns>Tuple</returns>
        internal static Tuple<string, PdbLine[], Dictionary<int, string>> GetMethodPDBInfo(MethodInfo method)
        {
#if DEBUG
            var methodType = method.DeclaringType;
            var methodAsm = methodType.Assembly;
            var assembly = methodAsm.GetName();
            var assemblyName = assembly.Name;
            var methodToken = (uint)method.MetadataToken;
            var path = new FileInfo(assembly.CodeBase.Replace("file:///", string.Empty)).Directory.FullName;
            var pdbFile = string.Concat(Path.Combine(path, assemblyName), ".pdb");

            if (!File.Exists(pdbFile))
            {
                return null;
            }

            var pdbMapping = AssemblyPDBs.GetOrAdd(assemblyName, _ => {
                using (var fs = new FileStream(pdbFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var host = new Microsoft.Cci.PeReader.DefaultHost();
                    using (var pdbReader = new Microsoft.Cci.PdbReader(fs, host))
                    {
                        return pdbReader.PdbFunctionMap;
                    }
                }
            });

            PdbFunction function = null;

            if (pdbMapping.TryGetValue(methodToken, out function))
            {
                var lineInfo = function.Lines[0];
                var locals = new Dictionary<int, string>();

                ProcessScopes(function.Scopes, locals);

                return new Tuple<string, PdbLine[], Dictionary<int, string>>(lineInfo.File.name, lineInfo.Lines, locals);
            }

            return null;
#else
            return null;
#endif
        }

        /// <summary>
        /// Process Local Scopes
        /// </summary>
        /// <param name="scopes">Scope</param>
        /// <param name="locals">Current list of locals</param>
        private static void ProcessScopes(PdbScope[] scopes, Dictionary<int, string> locals)
        {
            scopes = scopes ?? new PdbScope[0];
            foreach (var scope in scopes)
            {
                var slots = scope.Slots;
                if (slots != null)
                {
                    foreach (var slot in slots)
                    {
                        locals[(int)slot.slot] = slot.name;
                    }
                }
                if (scope.Scopes != null)
                {
                    ProcessScopes(scope.Scopes, locals);
                }
            }
        }

        /// <summary>
        /// Get index value for given msil instruction
        /// </summary>
        /// <param name="current">MSIL Instruction</param>
        /// <returns>Int</returns>
        private static int GetInstructionIndex(ILInstruction current)
        {
            var name = current.Instruction.Name;
            var index = Convert.ToInt32(name.Split('.')[1]);
            return index;
        }

        /// <summary>
        /// Swap given method with new method
        /// </summary>
        /// <param name="meth"></param>
        /// <param name="newMethod"></param>
        public static void SwapWith(this MethodBase meth, MethodBase newMethod)
        {
            ReplaceMethod(newMethod, meth);
        }

        /// <summary>
        /// Swap given field with new field
        /// </summary>
        /// <param name="field"></param>
        /// <param name="newField"></param>
        public static void SwapWith(this FieldInfo field, FieldInfo newField)
        {
            var srcAdr = newField.FieldHandle.Value;
            var destAdr = GetMethodAddress(field.FieldHandle.Value);
            ReplaceDestAddress(srcAdr, destAdr);
        }

        /// <summary>
        /// Replaces the method.
        /// </summary>
        /// <param name="source">Source Method</param>
        /// <param name="dest">Destination Method</param>
        private static void ReplaceMethod(MethodBase source, MethodBase dest)
        {
            if (!MethodSignaturesEqual(source, dest))
            {
                throw new ArgumentException("The method signatures are not the same.", "source");
            }

            ReplaceMethod(GetMethodAddress(source), dest);
        }

        /// <summary>
        /// Replaces the method.
        /// </summary>
        /// <param name="srcAdr">Source method address</param>
        /// <param name="dest">Destination method</param>
        private static void ReplaceMethod(IntPtr srcAdr, MethodBase dest)
        {
            IntPtr destAdr = GetMethodAddressRef(dest);

            ReplaceDestAddress(srcAdr, destAdr);
        }


        /// <summary>
        /// Point destionation pointer to source pointer
        /// </summary>
        /// <param name="srcAdr">Source address</param>
        /// <param name="dest">Destination address</param>
        private static void ReplaceDestAddress(IntPtr srcAdr, IntPtr destAdr)
        {
            if (IntPtr.Size == 8)
            {
                ulong* d = (ulong*)destAdr.ToPointer();
                *d = (ulong)srcAdr.ToInt64();
            }
            else
            {
                uint* d = (uint*)destAdr.ToPointer();
                *d = (uint)srcAdr.ToInt32();
            }
        }


        /// <summary>
        /// Get address for given method
        /// </summary>
        /// <param name="srcMethod"></param>
        /// <returns></returns>
        private static IntPtr GetMethodAddressRef(MethodBase srcMethod)
        {
            if ((srcMethod is DynamicMethod))
            {
                return GetDynamicMethodAddress(srcMethod);
            }

            RuntimeHelpers.PrepareMethod(srcMethod.MethodHandle);

            IntPtr ptr = srcMethod.MethodHandle.GetFunctionPointer();

            // If 3.5 sp1 or greater than we have a different layout in memory.
            if (IsNet20Sp2OrGreater())
            {
                IntPtr addrRef = GetMethodAddress20SP2(srcMethod);

                if (IsAddressValueMatch(addrRef, ptr))
                {
                    return addrRef;
                }

                addrRef = IntPtr.Zero;
                
                {
                    UInt64* methodDesc = (UInt64*)(srcMethod.MethodHandle.Value.ToPointer());
                    int index = (int)(((*methodDesc) >> 32) & 0xFF);

                    if (IntPtr.Size == 8)
                    {
                        ulong* start = (ulong*)srcMethod.DeclaringType.TypeHandle.Value.ToPointer();
                        start += 8;
                        start = (ulong*)*start;
                        ulong* address = start + index;
                        addrRef = new IntPtr(address);
                    }
                    else
                    {
                        uint* start = (uint*)srcMethod.DeclaringType.TypeHandle.Value.ToPointer();
                        start += 10;
                        start = (uint*)*start;
                        uint* address = start + index;
                        addrRef = new IntPtr(address);
                    }

                    if (IsAddressValueMatch(addrRef, ptr))
                    {
                        return addrRef;
                    }
                }
            }

            {
                const int SKIP = 10;

                UInt64* location = (UInt64*)(srcMethod.MethodHandle.Value.ToPointer());
                int index = (int)(((*location) >> 32) & 0xFF);

                if (IntPtr.Size == 8)
                {
                    ulong* start = (ulong*)srcMethod.DeclaringType.TypeHandle.Value.ToPointer();
                    ulong* address = start + index + SKIP;
                    return new IntPtr(address);
                }
                else
                {
                    uint* start = (uint*)srcMethod.DeclaringType.TypeHandle.Value.ToPointer();
                    uint* address = start + index + SKIP;
                    return new IntPtr(address);
                }
            }
        }

        /// <summary>
        /// Determine if given address are equal
        /// </summary>
        /// <param name="address">Address</param>
        /// <param name="value">Value</param>
        /// <returns></returns>
        private static bool IsAddressValueMatch(IntPtr address, IntPtr value)
        {
            IntPtr realValue = *(IntPtr*)address;
            return realValue == value;
        }

        /// <summary>
        /// Gets the address of the method stub
        /// </summary>
        /// <param name="method">Method.</param>
        /// <returns></returns>
        private static IntPtr GetMethodAddress(MethodBase method)
        {
            if (method is DynamicMethod)
            {
                return GetDynamicMethodAddress(method);
            }

            RuntimeHelpers.PrepareMethod(method.MethodHandle);
            return method.MethodHandle.GetFunctionPointer();
        }

        /// <summary>
        /// Get the address of dynamic method
        /// </summary>
        /// <param name="method"></param>
        /// <returns>IntPtr</returns>
        private static IntPtr GetDynamicMethodAddress(MethodBase method)
        {
            RuntimeMethodHandle handle = GetDynamicMethodRuntimeHandle(method);
            byte* ptr = (byte*)handle.Value.ToPointer();

            if (IsNet20Sp2OrGreater())
            {
                RuntimeHelpers.PrepareMethod(handle);
                return handle.GetFunctionPointer();
            }
            else
            {
                if (IntPtr.Size == 8)
                {
                    ulong* address = (ulong*)ptr;
                    address += 6;
                    return new IntPtr(address);
                }
                else
                {
                    uint* address = (uint*)ptr;
                    address += 6;
                    return new IntPtr(address);
                }
            }
        }

        /// <summary>
        /// Get Dynamic Method RuntimeMethod Handle
        /// </summary>
        /// <param name="method"></param>
        /// <returns>RuntimeMethodHandle</returns>
        private static RuntimeMethodHandle GetDynamicMethodRuntimeHandle(MethodBase method)
        {
            RuntimeMethodHandle handle;

            if (Environment.Version.Major == 4)
            {
                MethodInfo getMethodDescriptorInfo = typeof(DynamicMethod).GetMethod("GetMethodDescriptor",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                handle = (RuntimeMethodHandle)getMethodDescriptorInfo.Invoke(method, null);
            }
            else
            {
                FieldInfo fieldInfo = typeof(DynamicMethod).GetField("m_method", BindingFlags.NonPublic | BindingFlags.Instance);
                handle = ((RuntimeMethodHandle)fieldInfo.GetValue(method));
            }
                
            return handle;
        }

        /// <summary>
        /// Get method address for .Net 2.0 SP2
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        private static IntPtr GetMethodAddress20SP2(MethodBase method)
        {
            return GetMethodAddress(method.MethodHandle.Value);
        }

        private static IntPtr GetMethodAddress(IntPtr handle)
        {
            return new IntPtr(((int*)handle.ToPointer() + 2));
        }

        /// <summary>
        /// Determine if given methods are equal
        /// </summary>
        /// <param name="method1">Method</param>
        /// <param name="method2">Method</param>
        /// <returns>Bool</returns>
        private static bool MethodSignaturesEqual(MethodBase method1, MethodBase method2)
        {
            if (method1.IsGenericMethod || method2.IsGenericMethod)
            {
                return true;
            }

            var return1 = GetMethodReturnType(method1);
            var return2 = GetMethodReturnType(method2);
            if (return1 != return2)
            {
                return false;
            }

            var params1 = method1.GetParameters();
            var params2 = method2.GetParameters();

            if (params1.Length != params2.Length)
            {
                return false;
            }

            for (int i = 0; i < params1.Length; i++)
            {
                if (params1[i].ParameterType != params2[i].ParameterType)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get return type for given method
        /// </summary>
        /// <param name="method">Method</param>
        /// <returns>Type</returns>
        private static Type GetMethodReturnType(MethodBase method)
        {
            MethodInfo methodInfo = method as MethodInfo;

            if (methodInfo == null)
            {
                throw new ArgumentException("Constructors is not supported");
            }

            return methodInfo.ReturnType;
        }

        /// <summary>
        /// Determine if .Net Framework is greater than .Net 2.0
        /// </summary>
        /// <returns>Bool</returns>
        private static bool IsNet20Sp2OrGreater()
        {
            if (Environment.Version.Major >= 4)
            {
                return true;
            }

            return Environment.Version.Major == Net20SP2.Major &&
                Environment.Version.MinorRevision >= Net20SP2.MinorRevision;
        }
    }
}
