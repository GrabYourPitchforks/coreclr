// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using Internal.Runtime.CompilerServices;

#if BIT64
using nuint = System.UInt64;
#else
using nuint = System.UInt32;
#endif

namespace System.Reflection
{
    public sealed class FieldAccessor<TObject, TField> where TObject : class
    {
        private readonly nuint _fieldOffset;

        public unsafe FieldAccessor(FieldInfo fieldInfo)
        {
            // There are four checks we perform:
            // - The field must be a regular RtFieldInfo (not a manufactured FieldInfo)
            // - The field must be an instance field
            // - The field must be on TObject or a superclass

            if (fieldInfo is null)
            {
                throw new ArgumentNullException(nameof(fieldInfo));
            }

            if (!(fieldInfo is RtFieldInfo rtFieldInfo))
            {
                throw new ArgumentException(SR.Argument_MustBeRuntimeFieldInfo, nameof(fieldInfo));
            }

            if (rtFieldInfo.IsStatic)
            {
                // TODO: Use a better resource string for this.
                throw new ArgumentException(SR.Format(SR.Argument_TypedReferenceInvalidField, rtFieldInfo.Name));
            }

            if (typeof(TObject) != rtFieldInfo.GetDeclaringTypeInternal() && !typeof(TObject).IsSubclassOf(rtFieldInfo.GetDeclaringTypeInternal()))
            {
                // TODO: Use a better resource string for this.
                throw new MissingMemberException(SR.MissingMemberTypeRef);
            }

            _fieldOffset = (nuint)(void*)rtFieldInfo.GetOffsetInBytes();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TField GetRef(TObject obj)
        {
            // Normally, the JIT would perform a null check on "this" before invoking the instance method.
            // By moving the this._fieldOffset dereference to the beginning of the method and storing it
            // as a local copy, the JIT is able to elide the earlier null check because the line immediately
            // following would cause the correct NullReferenceException if the caller called (null).GetRef(...).

            nuint fieldOffset = _fieldOffset;

            // We need to perform an additional null check on 'obj' so that we don't hand out a reference
            // to garbage memory. The easiest way to do this is through a dummy call to GetType(), which
            // the JIT converts into a simple dereference. The JIT will elide this check entirely if it
            // has other ways of proving that 'obj' cannot be null here.

            if (obj.GetType() == typeof(object))
            {
                // intentionally left empty - we only care about the null check
            }

            return ref Unsafe.As<byte, TField>(ref RuntimeHelpers.GetRefToObjectOffset(obj, fieldOffset));
        }
    }
}
