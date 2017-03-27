using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil.Rocks;
using System.Text.RegularExpressions;

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

        private static readonly ConcurrentDictionary<string, IEnumerable<Attribute>> customAttributes =
            new ConcurrentDictionary<string, IEnumerable<Attribute>>();

        private static readonly ConcurrentDictionary<string, Type> typeReferences =
            new ConcurrentDictionary<string, Type>();

        private static readonly ConcurrentDictionary<string, Regex> regexs = new ConcurrentDictionary<string, Regex>();

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
                HandlerEnd = Instruction.Create(OpCodes.Leave, endHandler),//
                CatchType = module.Import(typeof(Exception))
            };

            il.Append(tryHandler.TryStart);
            il.Append(catchHandler.TryStart);

            return tryHandler.TryStart;
        }

        public static void BeginCatchBlock(this ILProcessor il, TypeReference catchType)
        {
            il.Append(Instruction.Create(OpCodes.Leave, catchHandler.HandlerEnd));
            catchHandler.TryEnd = Instruction.Create(OpCodes.Nop);
            catchHandler.HandlerStart = catchHandler.TryEnd;
            catchHandler.CatchType = catchType;

            il.Append(catchHandler.TryEnd);
        }

        public static void BeginCatchBlock(this ILProcessor il, Type catchType)
        {
            BeginCatchBlock(il, catchType.ToCecil());
        }

        public static void BeginFinallyBlock(this ILProcessor il)
        {
            var hasCatch = catchHandler.HandlerStart != null;
            if (catchHandler.TryEnd != null)
            {
                il.Emit(OpCodes.Leave, catchHandler.HandlerEnd);

                il.Append(catchHandler.HandlerEnd);
            }

            if (!hasCatch)
            {
                il.Append(Instruction.Create(OpCodes.Leave, endHandler));
            }
            
            tryHandler.TryEnd = Instruction.Create(OpCodes.Nop);
            tryHandler.HandlerStart = tryHandler.TryEnd;
            
            il.Append(tryHandler.TryEnd);
        }

        public static void EndExceptionBlock(this ILProcessor il)
        {
            var hasFinally = tryHandler.HandlerStart != null;
            var hasCatch = catchHandler.HandlerStart != null;

            if (!hasFinally)
            {
                il.Emit(OpCodes.Leave, catchHandler.HandlerEnd);

                il.Append(catchHandler.HandlerEnd);
            }

            if (hasFinally)
            {
                tryHandler.HandlerEnd = endHandler;//Instruction.Create(OpCodes.Nop);

                il.Emit(OpCodes.Endfinally);
                //il.Append(tryHandler.HandlerEnd);
            }

            il.Append(endHandler);

            if (hasCatch)
            {
                il.Body.ExceptionHandlers.Add(catchHandler);
            }

            if (hasFinally)
            {
                il.Body.ExceptionHandlers.Add(tryHandler);
            }
        }

        public static VariableDefinition DeclareLocal(this ILProcessor il, TypeReference type)
        {
            var variable = new VariableDefinition(type);
            il.Body.Variables.Add(variable);

            if (!il.Body.InitLocals)
            {
                il.Body.InitLocals = true;
            }

            return variable;
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

        public static FieldDefinition DefineField(this TypeDefinition typeDef, string name, Type type, FieldAttributes attrs)
        {
            var field = new FieldDefinition(name, attrs, module.Import(type));
            typeDef.Fields.Add(field);

            return field;
        }

        public static MethodDefinition GetMethod(this TypeReference type, string name, System.Reflection.BindingFlags flags)
        {
            return type.Resolve().GetMethods(flags).FirstOrDefault(x => x.Name == name);
        }

        public static PropertyDefinition GetProperty(this TypeDefinition type, string name, System.Reflection.BindingFlags flags)
        {
            return type.Properties.FirstOrDefault(x => x.Name == name);
        }

        public static MethodReference ToCecil(this System.Reflection.MethodBase meth)
        {
            return module.Import(meth);
        }

        public static TypeReference ToCecil(this System.Type type)
        {
            return module.Import(type);
        }

        public static bool IsPrimitive(this Type type)
        {
            if (type.IsGenericType &&
                        type.GetGenericTypeDefinition() == typeof(Nullable<>))
                type = type.GetGenericArguments()[0];

            return type == typeof(string) ||
                type.IsPrimitive || type == typeof(DateTime) ||
                type == typeof(DateTimeOffset) ||
                type == typeof(Decimal) || type == typeof(TimeSpan) ||
                type == typeof(Guid) || type == typeof(char) ||
                type.IsEnum;
        }

        public static MethodReference ToGenericMethod(this MethodReference method)
        {
            var type = method.DeclaringType;
            var isGenericType = type.HasGenericParameters;

            if (!isGenericType)
            {
                return method;
            }

            type = type.MakeGenericInstanceType(type.GenericParameters.ToArray());

            var parameters = method.Parameters;
            var genMethod = type.GetMethods()
                .Select(x => new { Method = x, Parameters = x.Parameters })
                .FirstOrDefault(x => x.Method.Name == method.Name && x.Parameters.Count == method.Parameters.Count &&
                (x.Method.HasGenericParameters || x.Method.ContainsGenericParameter) &&
                x.Parameters.All(mp => parameters.Any(p => p.Name == mp.Name && 
                p.ParameterType.FullName == mp.ParameterType.FullName)));

            if(genMethod != null)
            {
                var gen = genMethod.Method;
                var hasThis = gen.HasThis;
                var meth = new MethodReference(gen.Name, gen.ReturnType, type);
                meth.HasThis = hasThis;

                foreach (var param in gen.Parameters)
                {
                    meth.Parameters.Add(param);
                }

                var genRef = new GenericInstanceMethod(gen);
                
                foreach(var genParam in gen.GenericParameters)
                {
                    var elementType = genParam.GetElementType();
                    genRef.GenericArguments.Add(elementType);
                }

                return meth;
            }

            return method;
        }

        public static IEnumerable<FieldDefinition> GetFields(this TypeDefinition type, System.Reflection.BindingFlags flags)
        {
            return GetDefinitions(type, t => t.Fields, flags);
        }

        public static IEnumerable<MethodDefinition> GetMethods(this TypeDefinition type, System.Reflection.BindingFlags flags)
        {
            return GetDefinitions(type, t => t.Methods, flags).Where(x => !x.IsConstructor);
        }

        public static IEnumerable<MethodDefinition> GetMethods(this TypeReference type)
        {
            return GetMethods(type.Resolve(), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        }

        public static IEnumerable<T> GetDefinitions<T>(this TypeDefinition type, Func<TypeDefinition, IEnumerable<T>> func, System.Reflection.BindingFlags flags) where T : IMemberDefinition
        {
            var hash = new HashSet<string>();
            var definitions = func(type).ToList();
            var baseType = type.BaseType;

            foreach (IMemberDefinition def in definitions)
                hash.Add(def.FullName);

            while (baseType != null)
            {
                var baseTypeDef = baseType.Resolve();
                var baseTypeDefs = func(baseTypeDef);

                foreach (IMemberDefinition def in baseTypeDefs)
                {
                    var name = def.FullName;
                    if(!hash.Contains(name))
                    {
                        hash.Add(name);
                        definitions.Add((T)def);
                    }
                }

                baseType = baseTypeDef.BaseType;
            }

            return definitions;
        }

        public static IEnumerable<MethodDefinition> GetConstructors(this TypeDefinition type, System.Reflection.BindingFlags flags)
        {
            return type.Methods.Where(x => x.IsConstructor);
        }

        public static IEnumerable<MethodDefinition> GetConstructors(this TypeDefinition type)
        {
            return GetConstructors(type, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        }

        public static IEnumerable<MethodDefinition> GetConstructors(this TypeReference type)
        {
            return GetConstructors(type.Resolve());
        }

        public static MethodDefinition GetMethod(this TypeReference type, string name)
        {
            return GetMethod(type, name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        }

        public static EventDefinition GetEvent(this TypeDefinition type, string name)
        {
            return GetEvent(type, name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        }

        public static EventDefinition GetEvent(this TypeDefinition type, string name, System.Reflection.BindingFlags flags)
        {
            return GetDefinitions(type, t => t.Events, flags).FirstOrDefault(x => x.Name == name);
        }

        public static Type ReflectionType(this TypeReference type)
        {
            var typeName = type.GetTypeName();
            return typeReferences.GetOrAdd(typeName, _ =>
            {
                var typeRef = Type.GetType(_, false);
                if (typeRef == null)
                {
                    var typeRefName = typeName + ", " + type.Module.Assembly.FullName;
                    typeRef = Type.GetType(typeRefName, false);
                    if(typeRef == null)
                    {
                        typeRefName = typeName + ", " + type.Resolve().Module.Assembly.FullName;
                        typeRef = Type.GetType(typeRefName, false);
                    }
                }

                return typeRef;
            });
        }

        private static string GetTypeName(this TypeReference type)
        {
            if (type.IsGenericInstance)
            {
                var generic = type as GenericInstanceType;
                return string.Format("{0}.{1}[{2}]", generic.Namespace, type.Name, string.Join(",", generic.GenericArguments.Select(p => p.GetTypeName())));
            }

            if (type.IsNested)
            {
                var space = type.Namespace;
                var names = new List<string> { type.Name };
                var rootType = type.DeclaringType;

                while(rootType != null)
                {
                    names.Add(rootType.Name);
                    rootType = rootType.DeclaringType;
                }

                names.Reverse();

                return string.Concat(space, ".", string.Join(".", names));
            }

            return type.FullName;
        }

        public static IEnumerable<T> GetCustomAttributes<T>(this ICustomAttributeProvider provider)
        {
            var cType = typeof(T);
            return GetCustomAttributes(provider, cType).Cast<T>();
        }

        public static IEnumerable<Attribute> GetCustomAttributes(this ICustomAttributeProvider provider, Type cType)
        {
            var key = provider.ToString();

            var paramDef = provider as ParameterDefinition;
            var param = provider as ParameterReference;
            
            if(paramDef != null)
            {
                key = string.Concat(paramDef.Method.ToString(), paramDef.Name, paramDef.Index.ToString());
            }else if(param != null)
            {
                key = string.Concat(param.Resolve().Method.ToString(), param.Name, param.Index.ToString());
            }
            
            return customAttributes.GetOrAdd(key, _ =>
            {
                return GetCustomAttributesFor(provider, cType);
            });
        }

        public static Regex ToRegex(this string pattern)
        {
            return regexs.GetOrAdd(pattern, _ => new Regex(pattern, RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase));
        }

        public static Attribute GetCustomAttribute(this ICustomAttributeProvider provider, Type cType)
        {
            var fullName = cType.FullName;
            return GetCustomAttributes(provider, cType).FirstOrDefault(x => x.GetType().FullName == fullName);
        }

        public static T GetCustomAttribute<T>(this ICustomAttributeProvider provider) where T : Attribute
        {
            return (T)GetCustomAttribute(provider, typeof(T));
        }

        private static object Convert(this object value, TypeReference type)
        {
            var typeRef = type.ReflectionType();
            if (typeRef.IsEnum)
            {
                return Enum.Parse(typeRef, value.ToString());
            }

            if(value is TypeDefinition)
            {
                value = ((TypeDefinition)value).ReflectionType();
            }

            return value;
        }

        private static IEnumerable<Attribute> GetCustomAttributesFor(ICustomAttributeProvider provider, Type cType)
        {
            var attrs = provider.CustomAttributes;
            foreach (var attr in attrs)
            {
                var attrRefType = attr.AttributeType;

                if(attrRefType.Namespace.StartsWith("system", StringComparison.OrdinalIgnoreCase) ||
                    attrRefType.Scope.Name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var attrType = module.Import(attrRefType).ReflectionType();

                if (attrType == null)
                {
                    continue;
                }

                if (!(attrType.IsAssignableFrom(cType) || attrType.IsSubclassOf(cType)))
                {
                    continue;
                }

                var cAttr = (Attribute)Activator.CreateInstance(attrType, attr.ConstructorArguments.Select(x => x.Value.Convert(x.Type)).ToArray());

                foreach (var field in attr.Fields)
                {
                    var fieldInfo = attrType.GetField(field.Name);
                    fieldInfo.SetValue(cAttr, field.Argument.Value);
                }

                foreach (var prop in attr.Properties)
                {
                    var propInfo = attrType.GetProperty(prop.Name);
                    propInfo.SetValue(cAttr, prop.Argument.Value);
                }

                 yield return cAttr;
            }
        }

        public static void MarkLabel(this ILProcessor il, Instruction label)
        {
            il.Append(label);
        }

        public static void Emit(this ILProcessor il, OpCode opcode, Type cls)
        {
            il.Emit(opcode, module.Import(cls));
        }

        public static void Emit(this ILProcessor il, OpCode opcode, System.Reflection.MethodBase meth)
        {
            il.Emit(opcode, module.Import(meth));
        }

        public static void Emit(this ILProcessor il, OpCode opcode, System.Reflection.ConstructorInfo con)
        {
            il.Emit(opcode, module.Import(con));
        }

        public static void Emit(this ILProcessor il, OpCode opcode, System.Reflection.FieldInfo field)
        {
            il.Emit(opcode, module.Import(field));
        }
    }
}
