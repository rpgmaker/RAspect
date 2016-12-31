using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Aspects
{
    /// <summary>
    /// Aspect for intercepting set/get for fields and properties
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Assembly)]
    public abstract class MemberInterceptionAspect : AspectBase
    {
        /// <summary>
        /// Member Context Constructor
        /// </summary>
        private readonly static ConstructorInfo MemberContextCtor = typeof(MemberContext).GetConstructor(new[] { typeof(object), typeof(string), typeof(object) });

        /// <summary>
        /// On Set Value Method
        /// </summary>
        private readonly static MethodInfo SetValueMethod = typeof(MemberInterceptionAspect).GetMethod("OnSetValue");

        /// <summary>
        /// On Set Value Method
        /// </summary>
        private readonly static MethodInfo GetValueMethod = typeof(MemberInterceptionAspect).GetMethod("OnGetValue");

        /// <summary>
        /// Member Context Value
        /// </summary>
        private readonly static MethodInfo MemberContextValueMethod = typeof(MemberContext).GetMethod("get_Value");

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberInterceptionAspect"/> class.
        /// </summary>
        public MemberInterceptionAspect() : base(WeaveTargetType.Properties | WeaveTargetType.Fields)
        {
            OnAspectBlockSetField = AspectBlockSetField;
            OnAspectBlockGetField = AspectBlockGetField;
        }

        /// <summary>
        /// Capture on entry of field/property
        /// </summary>
        /// <param name="context">MethodContext</param>
        internal override void OnEntry(MethodContext context)
        {
            var name = context.Method.Name;
            var isProperty = name.StartsWith("get_") || name.StartsWith("set_");

            if (!isProperty)
                return;

            var memberContext = new MemberContext
            {
                Instance = context.Instance,
                LocationName = name,
                Value = context.Returns,
                Continue = context.Continue,
                IsProperty = true
            };

            OnEnter(memberContext);

            context.Continue = memberContext.Continue;
        }

        /// <summary>
        /// Capture on success of field/property
        /// </summary>
        /// <param name="context">MethodContext</param>
        internal override void OnSuccess(MethodContext context)
        {
            var name = context.Method.Name;
            var isProperty = name.StartsWith("get_") || name.StartsWith("set_");

            if (!isProperty)
                return;

            var isGetter = isProperty && name.StartsWith("get_");

            var memberContext = new MemberContext
            {
                Instance = context.Instance,
                LocationName = name,
                Value = context.Returns,
                IsProperty = true
            };

            if (isGetter)
                OnGetValue(memberContext);
            else
            {
                memberContext.Value = context.Arguments.FirstOrDefault().Value;
                OnSetValue(memberContext);
            }

            context.Returns = memberContext.Value;
        }

        /// <summary>
        /// Capture on enter of field/property access
        /// </summary>
        /// <param name="context">MemberContext</param>
        public virtual void OnEnter(MemberContext context) { }

        /// <summary>
        /// Capture on setting value of field/property
        /// </summary>
        /// <param name="context">MemberContext</param>
        public virtual void OnSetValue(MemberContext context) { }

        /// <summary>
        /// Capture on getting value of field/property
        /// </summary>
        /// <param name="context">MemberContext</param>
        public virtual void OnGetValue(MemberContext context) { }

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

        /// <summary>
        /// Weave field into current aspect
        /// </summary>
        /// <param name="il">IL Generator</param>
        /// <param name="field">Field</param>
        /// <param name="methodContext">Method Context</param>
        private void AspectBlockSetField(ILGenerator il, FieldInfo field, LocalBuilder methodContext)
        {
            AspectBlockField(il, field, methodContext, SetValueMethod);
        }

        /// <summary>
        /// Weave field into current aspect
        /// </summary>
        /// <param name="il">IL Generator</param>
        /// <param name="field">Field</param>
        /// <param name="methodContext">Method Context</param>
        private void AspectBlockGetField(ILGenerator il, FieldInfo field, LocalBuilder methodContext)
        {
            AspectBlockField(il, field, methodContext, GetValueMethod);
        }

        /// <summary>
        /// Weave field into current aspect
        /// </summary>
        /// <param name="il">IL Generator</param>
        /// <param name="field">Field</param>
        /// <param name="methodContext">Method Context</param>
        /// <param name="fieldMethod">Field Method</param>
        private void AspectBlockField(ILGenerator il, FieldInfo field, LocalBuilder methodContext, MethodInfo fieldMethod)
        {
            var fieldName = field.Name;

            // Return if it is a backing field
            if (fieldName.IndexOf("k__BackingField") >= 0)
                return;

            var aspect = field.GetCustomAttribute<MemberInterceptionAspect>() ?? field.DeclaringType.GetCustomAttribute<MemberInterceptionAspect>()
                ?? field.DeclaringType.Assembly.GetCustomAttribute<MemberInterceptionAspect>();

            if (!ILWeaver.IsValidAspectFor(field, aspect))
                return;

            var fieldType = field.FieldType;
            var isStatic = field.IsStatic;
            var fieldLocal = il.DeclareLocal(fieldType);
            var memberLocal = il.DeclareLocal(typeof(MemberContext));
            var aspectField = ILWeaver.TypeAspects[field.DeclaringType.FullName][aspect.GetType().FullName];

            // Store current get field value
            il.Emit(OpCodes.Stloc, fieldLocal);

            // MemberContext(object instance, string locationName, object value)
            il.Emit(isStatic ? OpCodes.Ldnull : OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, fieldName);
            il.Emit(OpCodes.Ldloc, fieldLocal);

            if (fieldType.IsValueType)
                il.Emit(OpCodes.Box, fieldType);

            il.Emit(OpCodes.Newobj, MemberContextCtor);
            il.Emit(OpCodes.Stloc, memberLocal);

            il.Emit(OpCodes.Ldsfld, aspectField);
            il.Emit(OpCodes.Ldloc, memberLocal);
            il.Emit(OpCodes.Callvirt, fieldMethod);

            // Load value back to stack and reflect changes if any
            il.Emit(OpCodes.Ldloc, memberLocal);
            il.Emit(OpCodes.Callvirt, MemberContextValueMethod);

            // Convert to expected type
            il.Emit(fieldType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Isinst, fieldType);
        }
    }
}