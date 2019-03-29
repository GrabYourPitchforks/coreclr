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

        public T[] Array
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array;
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

        public Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (IsNull)
                {
                    return default;
                }
                else
                {
                    return new Span<T>(ref Unsafe.As<byte, T>(ref _array.GetRawSzArrayData()), _array.Length);
                }
            }
        }

        public Span<T> SpanNotNull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return new Span<T>(ref Unsafe.As<byte, T>(ref _array.GetRawSzArrayData()), _array.Length);
            }
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array[index];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (default(T) is null)
                {
                    Unsafe.As<IntPtr, T>(ref Unsafe.As<IntPtr[]>(_array)[index]) = value;
                }
                else
                {
                    _array[index] = value;
                }
            }
        }
    }
}
