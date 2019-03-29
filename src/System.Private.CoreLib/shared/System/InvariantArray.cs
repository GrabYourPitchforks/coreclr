// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.CompilerServices;
using System.Runtime.CompilerServices;

namespace System
{
    internal readonly struct InvariantArray<T>
    {
        private readonly T[] _array;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InvariantArray(T[] array)
        {
            if (default(T) is null && (array != null && array.GetType() != typeof(T[])))
            {
                ThrowHelper.ThrowArrayTypeMismatchException();
            }

            _array = array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InvariantArray(int count)
        {
            _array = new T[count];
        }

        public bool IsNull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array is null;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array.Length;
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (default(T) is null)
                {
                    return ref Unsafe.As<IntPtr, T>(ref Unsafe.As<IntPtr[]>(_array)[index]);
                }
                else
                {
                    return ref _array[index];
                }
            }
        }
    }
}
