// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if netcoreapp
using Internal.Runtime.CompilerServices;
#endif
using System.Diagnostics;
using System.Globalization;
using System.Numerics.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace System.Numerics
{
    /* Note: The following patterns are used throughout the code here and are described here
    *
    * PATTERN:
    *    if (typeof(T) == typeof(int)) { ... }
    *    else if (typeof(T) == typeof(float)) { ... }
    * EXPLANATION:
    *    At runtime, each instantiation of Vector<T> will be type-specific, and each of these typeof blocks will be eliminated,
    *    as typeof(T) is a (JIT) compile-time constant for each instantiation. This design was chosen to eliminate any overhead from
    *    delegates and other patterns.
    *
    * PATTERN:
    *    if (Vector.IsHardwareAccelerated) { ... }
    *    else { ... }
    * EXPLANATION
    *    This pattern solves two problems:
    *        1. Allows us to unroll loops when we know the size (when no hardware acceleration is present)
    *        2. Allows reflection to work:
    *            - If a method is called via reflection, it will not be "intrinsified", which would cause issues if we did
    *              not provide an implementation for that case (i.e. if it only included a case which assumed 16-byte registers)
    *    (NOTE: It is assumed that Vector.IsHardwareAccelerated will be a compile-time constant, eliminating these checks
    *        from the JIT'd code.)
    *
    * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

    /// <summary>
    /// A structure that represents a single Vector. The count of this Vector is fixed but CPU register dependent.
    /// This struct only supports numerical types. This type is intended to be used as a building block for vectorizing
    /// large algorithms. This type is immutable, individual elements cannot be modified.
    /// </summary>
    [Intrinsic]
    public struct Vector<T> : IEquatable<Vector<T>>, IFormattable where T : struct
    {
        #region Fields
        private Register register;
        #endregion Fields

        #region Static Members
        /// <summary>
        /// Returns the number of elements stored in the vector. This value is hardware dependent.
        /// </summary>
        public static int Count
        {
            [Intrinsic]
            get
            {
                ThrowIfUnsupportedType();
                return Unsafe.SizeOf<Vector<T>>() / Unsafe.SizeOf<T>(); // JIT-time constants
            }
        }

        /// <summary>
        /// Returns a vector containing all zeroes.
        /// </summary>
        public static Vector<T> Zero
        {
            [Intrinsic]
            get
            {
                ThrowIfUnsupportedType();
                return default;
            }
        }

        /// <summary>
        /// Returns a vector containing all ones.
        /// </summary>
        public static Vector<T> One
        {
            [Intrinsic]
            get
            {
                return s_one;
            }
        }
        private static readonly Vector<T> s_one = new Vector<T>(GetOneValue());

        internal static Vector<T> AllOnes
        {
            [Intrinsic]
            get
            {
                return s_allOnes;
            }
        }
        private static readonly Vector<T> s_allOnes = new Vector<T>(GetAllBitsSetValue());
        #endregion Static Members

        #region Constructors
        /// <summary>
        /// Constructs a vector whose components are all <code>value</code>
        /// </summary>
        [Intrinsic]
        public unsafe Vector(T value)
            : this()
        {
            ThrowIfUnsupportedType();

            if (Vector.IsHardwareAccelerated)
            {
                // This is an intrinsic operation and will not result in a recursive call.
                this = new Vector<T>(value);
            }
            else
            {
                if (typeof(T) == typeof(byte))
                {
                    register.byte_0 = (byte)(object)value;
                    register.byte_1 = (byte)(object)value;
                    register.byte_2 = (byte)(object)value;
                    register.byte_3 = (byte)(object)value;
                    register.byte_4 = (byte)(object)value;
                    register.byte_5 = (byte)(object)value;
                    register.byte_6 = (byte)(object)value;
                    register.byte_7 = (byte)(object)value;
                    register.byte_8 = (byte)(object)value;
                    register.byte_9 = (byte)(object)value;
                    register.byte_10 = (byte)(object)value;
                    register.byte_11 = (byte)(object)value;
                    register.byte_12 = (byte)(object)value;
                    register.byte_13 = (byte)(object)value;
                    register.byte_14 = (byte)(object)value;
                    register.byte_15 = (byte)(object)value;
                }
                else if (typeof(T) == typeof(sbyte))
                {
                    register.sbyte_0 = (sbyte)(object)value;
                    register.sbyte_1 = (sbyte)(object)value;
                    register.sbyte_2 = (sbyte)(object)value;
                    register.sbyte_3 = (sbyte)(object)value;
                    register.sbyte_4 = (sbyte)(object)value;
                    register.sbyte_5 = (sbyte)(object)value;
                    register.sbyte_6 = (sbyte)(object)value;
                    register.sbyte_7 = (sbyte)(object)value;
                    register.sbyte_8 = (sbyte)(object)value;
                    register.sbyte_9 = (sbyte)(object)value;
                    register.sbyte_10 = (sbyte)(object)value;
                    register.sbyte_11 = (sbyte)(object)value;
                    register.sbyte_12 = (sbyte)(object)value;
                    register.sbyte_13 = (sbyte)(object)value;
                    register.sbyte_14 = (sbyte)(object)value;
                    register.sbyte_15 = (sbyte)(object)value;
                }
                else if (typeof(T) == typeof(ushort))
                {
                    register.uint16_0 = (ushort)(object)value;
                    register.uint16_1 = (ushort)(object)value;
                    register.uint16_2 = (ushort)(object)value;
                    register.uint16_3 = (ushort)(object)value;
                    register.uint16_4 = (ushort)(object)value;
                    register.uint16_5 = (ushort)(object)value;
                    register.uint16_6 = (ushort)(object)value;
                    register.uint16_7 = (ushort)(object)value;
                }
                else if (typeof(T) == typeof(short))
                {
                    register.int16_0 = (short)(object)value;
                    register.int16_1 = (short)(object)value;
                    register.int16_2 = (short)(object)value;
                    register.int16_3 = (short)(object)value;
                    register.int16_4 = (short)(object)value;
                    register.int16_5 = (short)(object)value;
                    register.int16_6 = (short)(object)value;
                    register.int16_7 = (short)(object)value;
                }
                else if (typeof(T) == typeof(uint))
                {
                    register.uint32_0 = (uint)(object)value;
                    register.uint32_1 = (uint)(object)value;
                    register.uint32_2 = (uint)(object)value;
                    register.uint32_3 = (uint)(object)value;
                }
                else if (typeof(T) == typeof(int))
                {
                    register.int32_0 = (int)(object)value;
                    register.int32_1 = (int)(object)value;
                    register.int32_2 = (int)(object)value;
                    register.int32_3 = (int)(object)value;
                }
                else if (typeof(T) == typeof(ulong))
                {
                    register.uint64_0 = (ulong)(object)value;
                    register.uint64_1 = (ulong)(object)value;
                }
                else if (typeof(T) == typeof(long))
                {
                    register.int64_0 = (long)(object)value;
                    register.int64_1 = (long)(object)value;
                }
                else if (typeof(T) == typeof(float))
                {
                    register.single_0 = (float)(object)value;
                    register.single_1 = (float)(object)value;
                    register.single_2 = (float)(object)value;
                    register.single_3 = (float)(object)value;
                }
                else if (typeof(T) == typeof(double))
                {
                    register.double_0 = (double)(object)value;
                    register.double_1 = (double)(object)value;
                }
            }
        }

        /// <summary>
        /// Constructs a vector from the given array. The size of the given array must be at least Vector'T.Count.
        /// </summary>
        [Intrinsic]
        public unsafe Vector(T[] values) : this(values, 0) { }

        /// <summary>
        /// Constructs a vector from the given array, starting from the given index.
        /// The array must contain at least Vector'T.Count from the given index.
        /// </summary>
        [Intrinsic]
        public unsafe Vector(T[] values, int index)
            : this()
        {
            if (values == null)
            {
                // Match the JIT's exception type here. For perf, a NullReference is thrown instead of an ArgumentNull.
                throw new NullReferenceException(SR.Arg_NullArgumentNullRef);
            }
            if (index < 0 || (values.Length - index) < Count)
            {
                throw new IndexOutOfRangeException(SR.Format(SR.Arg_InsufficientNumberOfElements, Vector<T>.Count, nameof(values)));
            }

            if (Vector.IsHardwareAccelerated)
            {
                if (typeof(T) == typeof(byte))
                {
                    fixed (byte* basePtr = &this.register.byte_0)
                    {
                        for (int g = 0; g < Count; g++)
                        {
                            *(basePtr + g) = (byte)(object)values[g + index];
                        }
                    }
                }
                else if (typeof(T) == typeof(sbyte))
                {
                    fixed (sbyte* basePtr = &this.register.sbyte_0)
                    {
                        for (int g = 0; g < Count; g++)
                        {
                            *(basePtr + g) = (sbyte)(object)values[g + index];
                        }
                    }
                }
                else if (typeof(T) == typeof(ushort))
                {
                    fixed (ushort* basePtr = &this.register.uint16_0)
                    {
                        for (int g = 0; g < Count; g++)
                        {
                            *(basePtr + g) = (ushort)(object)values[g + index];
                        }
                    }
                }
                else if (typeof(T) == typeof(short))
                {
                    fixed (short* basePtr = &this.register.int16_0)
                    {
                        for (int g = 0; g < Count; g++)
                        {
                            *(basePtr + g) = (short)(object)values[g + index];
                        }
                    }
                }
                else if (typeof(T) == typeof(uint))
                {
                    fixed (uint* basePtr = &this.register.uint32_0)
                    {
                        for (int g = 0; g < Count; g++)
                        {
                            *(basePtr + g) = (uint)(object)values[g + index];
                        }
                    }
                }
                else if (typeof(T) == typeof(int))
                {
                    fixed (int* basePtr = &this.register.int32_0)
                    {
                        for (int g = 0; g < Count; g++)
                        {
                            *(basePtr + g) = (int)(object)values[g + index];
                        }
                    }
                }
                else if (typeof(T) == typeof(ulong))
                {
                    fixed (ulong* basePtr = &this.register.uint64_0)
                    {
                        for (int g = 0; g < Count; g++)
                        {
                            *(basePtr + g) = (ulong)(object)values[g + index];
                        }
                    }
                }
                else if (typeof(T) == typeof(long))
                {
                    fixed (long* basePtr = &this.register.int64_0)
                    {
                        for (int g = 0; g < Count; g++)
                        {
                            *(basePtr + g) = (long)(object)values[g + index];
                        }
                    }
                }
                else if (typeof(T) == typeof(float))
                {
                    fixed (float* basePtr = &this.register.single_0)
                    {
                        for (int g = 0; g < Count; g++)
                        {
                            *(basePtr + g) = (float)(object)values[g + index];
                        }
                    }
                }
                else if (typeof(T) == typeof(double))
                {
                    fixed (double* basePtr = &this.register.double_0)
                    {
                        for (int g = 0; g < Count; g++)
                        {
                            *(basePtr + g) = (double)(object)values[g + index];
                        }
                    }
                }
            }
            else
            {
                if (typeof(T) == typeof(byte))
                {
                    fixed (byte* basePtr = &this.register.byte_0)
                    {
                        *(basePtr + 0) = (byte)(object)values[0 + index];
                        *(basePtr + 1) = (byte)(object)values[1 + index];
                        *(basePtr + 2) = (byte)(object)values[2 + index];
                        *(basePtr + 3) = (byte)(object)values[3 + index];
                        *(basePtr + 4) = (byte)(object)values[4 + index];
                        *(basePtr + 5) = (byte)(object)values[5 + index];
                        *(basePtr + 6) = (byte)(object)values[6 + index];
                        *(basePtr + 7) = (byte)(object)values[7 + index];
                        *(basePtr + 8) = (byte)(object)values[8 + index];
                        *(basePtr + 9) = (byte)(object)values[9 + index];
                        *(basePtr + 10) = (byte)(object)values[10 + index];
                        *(basePtr + 11) = (byte)(object)values[11 + index];
                        *(basePtr + 12) = (byte)(object)values[12 + index];
                        *(basePtr + 13) = (byte)(object)values[13 + index];
                        *(basePtr + 14) = (byte)(object)values[14 + index];
                        *(basePtr + 15) = (byte)(object)values[15 + index];
                    }
                }
                else if (typeof(T) == typeof(sbyte))
                {
                    fixed (sbyte* basePtr = &this.register.sbyte_0)
                    {
                        *(basePtr + 0) = (sbyte)(object)values[0 + index];
                        *(basePtr + 1) = (sbyte)(object)values[1 + index];
                        *(basePtr + 2) = (sbyte)(object)values[2 + index];
                        *(basePtr + 3) = (sbyte)(object)values[3 + index];
                        *(basePtr + 4) = (sbyte)(object)values[4 + index];
                        *(basePtr + 5) = (sbyte)(object)values[5 + index];
                        *(basePtr + 6) = (sbyte)(object)values[6 + index];
                        *(basePtr + 7) = (sbyte)(object)values[7 + index];
                        *(basePtr + 8) = (sbyte)(object)values[8 + index];
                        *(basePtr + 9) = (sbyte)(object)values[9 + index];
                        *(basePtr + 10) = (sbyte)(object)values[10 + index];
                        *(basePtr + 11) = (sbyte)(object)values[11 + index];
                        *(basePtr + 12) = (sbyte)(object)values[12 + index];
                        *(basePtr + 13) = (sbyte)(object)values[13 + index];
                        *(basePtr + 14) = (sbyte)(object)values[14 + index];
                        *(basePtr + 15) = (sbyte)(object)values[15 + index];
                    }
                }
                else if (typeof(T) == typeof(ushort))
                {
                    fixed (ushort* basePtr = &this.register.uint16_0)
                    {
                        *(basePtr + 0) = (ushort)(object)values[0 + index];
                        *(basePtr + 1) = (ushort)(object)values[1 + index];
                        *(basePtr + 2) = (ushort)(object)values[2 + index];
                        *(basePtr + 3) = (ushort)(object)values[3 + index];
                        *(basePtr + 4) = (ushort)(object)values[4 + index];
                        *(basePtr + 5) = (ushort)(object)values[5 + index];
                        *(basePtr + 6) = (ushort)(object)values[6 + index];
                        *(basePtr + 7) = (ushort)(object)values[7 + index];
                    }
                }
                else if (typeof(T) == typeof(short))
                {
                    fixed (short* basePtr = &this.register.int16_0)
                    {
                        *(basePtr + 0) = (short)(object)values[0 + index];
                        *(basePtr + 1) = (short)(object)values[1 + index];
                        *(basePtr + 2) = (short)(object)values[2 + index];
                        *(basePtr + 3) = (short)(object)values[3 + index];
                        *(basePtr + 4) = (short)(object)values[4 + index];
                        *(basePtr + 5) = (short)(object)values[5 + index];
                        *(basePtr + 6) = (short)(object)values[6 + index];
                        *(basePtr + 7) = (short)(object)values[7 + index];
                    }
                }
                else if (typeof(T) == typeof(uint))
                {
                    fixed (uint* basePtr = &this.register.uint32_0)
                    {
                        *(basePtr + 0) = (uint)(object)values[0 + index];
                        *(basePtr + 1) = (uint)(object)values[1 + index];
                        *(basePtr + 2) = (uint)(object)values[2 + index];
                        *(basePtr + 3) = (uint)(object)values[3 + index];
                    }
                }
                else if (typeof(T) == typeof(int))
                {
                    fixed (int* basePtr = &this.register.int32_0)
                    {
                        *(basePtr + 0) = (int)(object)values[0 + index];
                        *(basePtr + 1) = (int)(object)values[1 + index];
                        *(basePtr + 2) = (int)(object)values[2 + index];
                        *(basePtr + 3) = (int)(object)values[3 + index];
                    }
                }
                else if (typeof(T) == typeof(ulong))
                {
                    fixed (ulong* basePtr = &this.register.uint64_0)
                    {
                        *(basePtr + 0) = (ulong)(object)values[0 + index];
                        *(basePtr + 1) = (ulong)(object)values[1 + index];
                    }
                }
                else if (typeof(T) == typeof(long))
                {
                    fixed (long* basePtr = &this.register.int64_0)
                    {
                        *(basePtr + 0) = (long)(object)values[0 + index];
                        *(basePtr + 1) = (long)(object)values[1 + index];
                    }
                }
                else if (typeof(T) == typeof(float))
                {
                    fixed (float* basePtr = &this.register.single_0)
                    {
                        *(basePtr + 0) = (float)(object)values[0 + index];
                        *(basePtr + 1) = (float)(object)values[1 + index];
                        *(basePtr + 2) = (float)(object)values[2 + index];
                        *(basePtr + 3) = (float)(object)values[3 + index];
                    }
                }
                else if (typeof(T) == typeof(double))
                {
                    fixed (double* basePtr = &this.register.double_0)
                    {
                        *(basePtr + 0) = (double)(object)values[0 + index];
                        *(basePtr + 1) = (double)(object)values[1 + index];
                    }
                }
            }
        }

#pragma warning disable 3001 // void* is not a CLS-Compliant argument type
        internal unsafe Vector(void* dataPointer) : this(dataPointer, 0) { }
#pragma warning restore 3001 // void* is not a CLS-Compliant argument type

#pragma warning disable 3001 // void* is not a CLS-Compliant argument type
        // Implemented with offset if this API ever becomes public; an offset of 0 is used internally.
        internal unsafe Vector(void* dataPointer, int offset)
            : this()
        {
            ThrowIfUnsupportedType();

            this = Unsafe.ReadUnaligned<Vector<T>>(Unsafe.Add<T>(dataPointer, offset));
        }
#pragma warning restore 3001 // void* is not a CLS-Compliant argument type

        private Vector(ref Register existingRegister)
        {
            this.register = existingRegister;
        }

#if netcoreapp
        /// <summary>
        /// Constructs a vector from the given span. The span must contain at least Vector'T.Count elements.
        /// </summary>
        public Vector(Span<T> values)
            : this()
        {
            ThrowIfUnsupportedType();

            if (values.Length < Count)
            {
                throw new IndexOutOfRangeException(SR.Format(SR.Arg_InsufficientNumberOfElements, Vector<T>.Count, nameof(values)));
            }

            this = Unsafe.ReadUnaligned<Vector<T>>(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(values)));
        }
#endif
        #endregion Constructors

        #region Public Instance Methods
        /// <summary>
        /// Copies the vector to the given destination array. The destination array must be at least size Vector'T.Count.
        /// </summary>
        /// <param name="destination">The destination array which the values are copied into</param>
        /// <exception cref="ArgumentNullException">If the destination array is null</exception>
        /// <exception cref="ArgumentException">If number of elements in source vector is greater than those available in destination array</exception>
        [Intrinsic]
        public unsafe void CopyTo(T[] destination)
        {
            CopyTo(destination, 0);
        }

        /// <summary>
        /// Copies the vector to the given destination array. The destination array must be at least size Vector'T.Count.
        /// </summary>
        /// <param name="destination">The destination array which the values are copied into</param>
        /// <param name="startIndex">The index to start copying to</param>
        /// <exception cref="ArgumentNullException">If the destination array is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">If index is greater than end of the array or index is less than zero</exception>
        /// <exception cref="ArgumentException">If number of elements in source vector is greater than those available in destination array</exception>
        [Intrinsic]
        public unsafe void CopyTo(T[] destination, int startIndex)
        {
            if (destination == null)
            {
                // Match the JIT's exception type here. For perf, a NullReference is thrown instead of an ArgumentNull.
                throw new NullReferenceException(SR.Arg_NullArgumentNullRef);
            }
            if ((uint)startIndex >= destination.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.Format(SR.Arg_ArgumentOutOfRangeException, startIndex));
            }
            if ((destination.Length - startIndex) < Count)
            {
                throw new ArgumentException(SR.Format(SR.Arg_ElementsInSourceIsGreaterThanDestination, startIndex));
            }

            ThrowIfUnsupportedType();

            Unsafe.WriteUnaligned<Vector<T>>(ref Unsafe.As<T, byte>(ref Unsafe.Add(ref Unsafe.As<byte, T>(ref JitHelpers.GetRawSzArrayData(destination)), startIndex)), this);
        }

        /// <summary>
        /// Returns the element at the given index.
        /// </summary>
        public unsafe T this[int index]
        {
            [Intrinsic]
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index);
                }

                ThrowIfUnsupportedType();

                return Unsafe.Add(ref Unsafe.As<Register, T>(ref register), index);
            }
        }

        /// <summary>
        /// Returns a boolean indicating whether the given Object is equal to this vector instance.
        /// </summary>
        /// <param name="obj">The Object to compare against.</param>
        /// <returns>True if the Object is equal to this vector; False otherwise.</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (!(obj is Vector<T>))
            {
                return false;
            }
            return Equals((Vector<T>)obj);
        }

        /// <summary>
        /// Returns a boolean indicating whether the given vector is equal to this vector instance.
        /// </summary>
        /// <param name="other">The vector to compare this instance to.</param>
        /// <returns>True if the other vector is equal to this instance; False otherwise.</returns>
        [Intrinsic]
        public bool Equals(Vector<T> other)
        {
            ThrowIfUnsupportedType();

            if (Vector.IsHardwareAccelerated)
            {
                // This is an intrinsic operation and will not result in a recursive call.
                return Equals(other);
            }

            if (IntPtr.Size == 4)
            {
                return this.register.int32_0 == other.register.int32_0
                    && this.register.int32_1 == other.register.int32_1
                    && this.register.int32_2 == other.register.int32_2
                    && this.register.int32_3 == other.register.int32_3;
            }
            else
            {
                return this.register.int64_0 == other.register.int64_0
                    && this.register.int64_1 == other.register.int64_1;
            }
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            int hash = HashHelpers.RandomSeed;

            if (Vector.IsHardwareAccelerated)
            {
                // We currently only support 128-bit and 256-bit vectors for hardware acceleration
                Debug.Assert(Vector<int>.Count == 4 || Vector<int>.Count == 8);

                hash = HashHelpers.Combine(hash, ((Vector<int>)this)[0]);
                hash = HashHelpers.Combine(hash, ((Vector<int>)this)[1]);
                hash = HashHelpers.Combine(hash, ((Vector<int>)this)[2]);
                hash = HashHelpers.Combine(hash, ((Vector<int>)this)[3]);

                // We have a 256-bit vector
                if (Vector<int>.Count == 8)
                {
                    hash = HashHelpers.Combine(hash, ((Vector<int>)this)[4]);
                    hash = HashHelpers.Combine(hash, ((Vector<int>)this)[5]);
                    hash = HashHelpers.Combine(hash, ((Vector<int>)this)[6]);
                    hash = HashHelpers.Combine(hash, ((Vector<int>)this)[7]);
                }
            }
            else
            {
                hash = HashHelpers.Combine(hash, this.register.int32_0);
                hash = HashHelpers.Combine(hash, this.register.int32_1);
                hash = HashHelpers.Combine(hash, this.register.int32_2);
                hash = HashHelpers.Combine(hash, this.register.int32_3);
            }

            return hash;
        }

        /// <summary>
        /// Returns a String representing this vector.
        /// </summary>
        /// <returns>The string representation.</returns>
        public override string ToString()
        {
            return ToString("G", CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Returns a String representing this vector, using the specified format string to format individual elements.
        /// </summary>
        /// <param name="format">The format of individual elements.</param>
        /// <returns>The string representation.</returns>
        public string ToString(string format)
        {
            return ToString(format, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Returns a String representing this vector, using the specified format string to format individual elements
        /// and the given IFormatProvider.
        /// </summary>
        /// <param name="format">The format of individual elements.</param>
        /// <param name="formatProvider">The format provider to use when formatting elements.</param>
        /// <returns>The string representation.</returns>
        public string ToString(string format, IFormatProvider formatProvider)
        {
            StringBuilder sb = new StringBuilder();
            string separator = NumberFormatInfo.GetInstance(formatProvider).NumberGroupSeparator;
            sb.Append('<');
            for (int g = 0; g < Count - 1; g++)
            {
                sb.Append(((IFormattable)this[g]).ToString(format, formatProvider));
                sb.Append(separator);
                sb.Append(' ');
            }
            // Append last element w/out separator
            sb.Append(((IFormattable)this[Count - 1]).ToString(format, formatProvider));
            sb.Append('>');
            return sb.ToString();
        }
        #endregion Public Instance Methods

        #region Arithmetic Operators
        /// <summary>
        /// Adds two vectors together.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The summed vector.</returns>
        [Intrinsic]
        public static unsafe Vector<T> operator +(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    // This is an intrinsic operation and will not result in a recursive call.
                    return left + right;
                }
                else
                {
                    Vector<T> sum = new Vector<T>();
                    if (typeof(T) == typeof(byte))
                    {
                        sum.register.byte_0 = (byte)(left.register.byte_0 + right.register.byte_0);
                        sum.register.byte_1 = (byte)(left.register.byte_1 + right.register.byte_1);
                        sum.register.byte_2 = (byte)(left.register.byte_2 + right.register.byte_2);
                        sum.register.byte_3 = (byte)(left.register.byte_3 + right.register.byte_3);
                        sum.register.byte_4 = (byte)(left.register.byte_4 + right.register.byte_4);
                        sum.register.byte_5 = (byte)(left.register.byte_5 + right.register.byte_5);
                        sum.register.byte_6 = (byte)(left.register.byte_6 + right.register.byte_6);
                        sum.register.byte_7 = (byte)(left.register.byte_7 + right.register.byte_7);
                        sum.register.byte_8 = (byte)(left.register.byte_8 + right.register.byte_8);
                        sum.register.byte_9 = (byte)(left.register.byte_9 + right.register.byte_9);
                        sum.register.byte_10 = (byte)(left.register.byte_10 + right.register.byte_10);
                        sum.register.byte_11 = (byte)(left.register.byte_11 + right.register.byte_11);
                        sum.register.byte_12 = (byte)(left.register.byte_12 + right.register.byte_12);
                        sum.register.byte_13 = (byte)(left.register.byte_13 + right.register.byte_13);
                        sum.register.byte_14 = (byte)(left.register.byte_14 + right.register.byte_14);
                        sum.register.byte_15 = (byte)(left.register.byte_15 + right.register.byte_15);
                    }
                    else if (typeof(T) == typeof(sbyte))
                    {
                        sum.register.sbyte_0 = (sbyte)(left.register.sbyte_0 + right.register.sbyte_0);
                        sum.register.sbyte_1 = (sbyte)(left.register.sbyte_1 + right.register.sbyte_1);
                        sum.register.sbyte_2 = (sbyte)(left.register.sbyte_2 + right.register.sbyte_2);
                        sum.register.sbyte_3 = (sbyte)(left.register.sbyte_3 + right.register.sbyte_3);
                        sum.register.sbyte_4 = (sbyte)(left.register.sbyte_4 + right.register.sbyte_4);
                        sum.register.sbyte_5 = (sbyte)(left.register.sbyte_5 + right.register.sbyte_5);
                        sum.register.sbyte_6 = (sbyte)(left.register.sbyte_6 + right.register.sbyte_6);
                        sum.register.sbyte_7 = (sbyte)(left.register.sbyte_7 + right.register.sbyte_7);
                        sum.register.sbyte_8 = (sbyte)(left.register.sbyte_8 + right.register.sbyte_8);
                        sum.register.sbyte_9 = (sbyte)(left.register.sbyte_9 + right.register.sbyte_9);
                        sum.register.sbyte_10 = (sbyte)(left.register.sbyte_10 + right.register.sbyte_10);
                        sum.register.sbyte_11 = (sbyte)(left.register.sbyte_11 + right.register.sbyte_11);
                        sum.register.sbyte_12 = (sbyte)(left.register.sbyte_12 + right.register.sbyte_12);
                        sum.register.sbyte_13 = (sbyte)(left.register.sbyte_13 + right.register.sbyte_13);
                        sum.register.sbyte_14 = (sbyte)(left.register.sbyte_14 + right.register.sbyte_14);
                        sum.register.sbyte_15 = (sbyte)(left.register.sbyte_15 + right.register.sbyte_15);
                    }
                    else if (typeof(T) == typeof(ushort))
                    {
                        sum.register.uint16_0 = (ushort)(left.register.uint16_0 + right.register.uint16_0);
                        sum.register.uint16_1 = (ushort)(left.register.uint16_1 + right.register.uint16_1);
                        sum.register.uint16_2 = (ushort)(left.register.uint16_2 + right.register.uint16_2);
                        sum.register.uint16_3 = (ushort)(left.register.uint16_3 + right.register.uint16_3);
                        sum.register.uint16_4 = (ushort)(left.register.uint16_4 + right.register.uint16_4);
                        sum.register.uint16_5 = (ushort)(left.register.uint16_5 + right.register.uint16_5);
                        sum.register.uint16_6 = (ushort)(left.register.uint16_6 + right.register.uint16_6);
                        sum.register.uint16_7 = (ushort)(left.register.uint16_7 + right.register.uint16_7);
                    }
                    else if (typeof(T) == typeof(short))
                    {
                        sum.register.int16_0 = (short)(left.register.int16_0 + right.register.int16_0);
                        sum.register.int16_1 = (short)(left.register.int16_1 + right.register.int16_1);
                        sum.register.int16_2 = (short)(left.register.int16_2 + right.register.int16_2);
                        sum.register.int16_3 = (short)(left.register.int16_3 + right.register.int16_3);
                        sum.register.int16_4 = (short)(left.register.int16_4 + right.register.int16_4);
                        sum.register.int16_5 = (short)(left.register.int16_5 + right.register.int16_5);
                        sum.register.int16_6 = (short)(left.register.int16_6 + right.register.int16_6);
                        sum.register.int16_7 = (short)(left.register.int16_7 + right.register.int16_7);
                    }
                    else if (typeof(T) == typeof(uint))
                    {
                        sum.register.uint32_0 = (uint)(left.register.uint32_0 + right.register.uint32_0);
                        sum.register.uint32_1 = (uint)(left.register.uint32_1 + right.register.uint32_1);
                        sum.register.uint32_2 = (uint)(left.register.uint32_2 + right.register.uint32_2);
                        sum.register.uint32_3 = (uint)(left.register.uint32_3 + right.register.uint32_3);
                    }
                    else if (typeof(T) == typeof(int))
                    {
                        sum.register.int32_0 = (int)(left.register.int32_0 + right.register.int32_0);
                        sum.register.int32_1 = (int)(left.register.int32_1 + right.register.int32_1);
                        sum.register.int32_2 = (int)(left.register.int32_2 + right.register.int32_2);
                        sum.register.int32_3 = (int)(left.register.int32_3 + right.register.int32_3);
                    }
                    else if (typeof(T) == typeof(ulong))
                    {
                        sum.register.uint64_0 = (ulong)(left.register.uint64_0 + right.register.uint64_0);
                        sum.register.uint64_1 = (ulong)(left.register.uint64_1 + right.register.uint64_1);
                    }
                    else if (typeof(T) == typeof(long))
                    {
                        sum.register.int64_0 = (long)(left.register.int64_0 + right.register.int64_0);
                        sum.register.int64_1 = (long)(left.register.int64_1 + right.register.int64_1);
                    }
                    else if (typeof(T) == typeof(float))
                    {
                        sum.register.single_0 = (float)(left.register.single_0 + right.register.single_0);
                        sum.register.single_1 = (float)(left.register.single_1 + right.register.single_1);
                        sum.register.single_2 = (float)(left.register.single_2 + right.register.single_2);
                        sum.register.single_3 = (float)(left.register.single_3 + right.register.single_3);
                    }
                    else if (typeof(T) == typeof(double))
                    {
                        sum.register.double_0 = (double)(left.register.double_0 + right.register.double_0);
                        sum.register.double_1 = (double)(left.register.double_1 + right.register.double_1);
                    }
                    return sum;
                }
            }
        }

        /// <summary>
        /// Subtracts the second vector from the first.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The difference vector.</returns>
        [Intrinsic]
        public static unsafe Vector<T> operator -(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    // This is an intrinsic operation and will not result in a recursive call.
                    return left - right;
                }
                else
                {
                    Vector<T> difference = new Vector<T>();
                    if (typeof(T) == typeof(byte))
                    {
                        difference.register.byte_0 = (byte)(left.register.byte_0 - right.register.byte_0);
                        difference.register.byte_1 = (byte)(left.register.byte_1 - right.register.byte_1);
                        difference.register.byte_2 = (byte)(left.register.byte_2 - right.register.byte_2);
                        difference.register.byte_3 = (byte)(left.register.byte_3 - right.register.byte_3);
                        difference.register.byte_4 = (byte)(left.register.byte_4 - right.register.byte_4);
                        difference.register.byte_5 = (byte)(left.register.byte_5 - right.register.byte_5);
                        difference.register.byte_6 = (byte)(left.register.byte_6 - right.register.byte_6);
                        difference.register.byte_7 = (byte)(left.register.byte_7 - right.register.byte_7);
                        difference.register.byte_8 = (byte)(left.register.byte_8 - right.register.byte_8);
                        difference.register.byte_9 = (byte)(left.register.byte_9 - right.register.byte_9);
                        difference.register.byte_10 = (byte)(left.register.byte_10 - right.register.byte_10);
                        difference.register.byte_11 = (byte)(left.register.byte_11 - right.register.byte_11);
                        difference.register.byte_12 = (byte)(left.register.byte_12 - right.register.byte_12);
                        difference.register.byte_13 = (byte)(left.register.byte_13 - right.register.byte_13);
                        difference.register.byte_14 = (byte)(left.register.byte_14 - right.register.byte_14);
                        difference.register.byte_15 = (byte)(left.register.byte_15 - right.register.byte_15);
                    }
                    else if (typeof(T) == typeof(sbyte))
                    {
                        difference.register.sbyte_0 = (sbyte)(left.register.sbyte_0 - right.register.sbyte_0);
                        difference.register.sbyte_1 = (sbyte)(left.register.sbyte_1 - right.register.sbyte_1);
                        difference.register.sbyte_2 = (sbyte)(left.register.sbyte_2 - right.register.sbyte_2);
                        difference.register.sbyte_3 = (sbyte)(left.register.sbyte_3 - right.register.sbyte_3);
                        difference.register.sbyte_4 = (sbyte)(left.register.sbyte_4 - right.register.sbyte_4);
                        difference.register.sbyte_5 = (sbyte)(left.register.sbyte_5 - right.register.sbyte_5);
                        difference.register.sbyte_6 = (sbyte)(left.register.sbyte_6 - right.register.sbyte_6);
                        difference.register.sbyte_7 = (sbyte)(left.register.sbyte_7 - right.register.sbyte_7);
                        difference.register.sbyte_8 = (sbyte)(left.register.sbyte_8 - right.register.sbyte_8);
                        difference.register.sbyte_9 = (sbyte)(left.register.sbyte_9 - right.register.sbyte_9);
                        difference.register.sbyte_10 = (sbyte)(left.register.sbyte_10 - right.register.sbyte_10);
                        difference.register.sbyte_11 = (sbyte)(left.register.sbyte_11 - right.register.sbyte_11);
                        difference.register.sbyte_12 = (sbyte)(left.register.sbyte_12 - right.register.sbyte_12);
                        difference.register.sbyte_13 = (sbyte)(left.register.sbyte_13 - right.register.sbyte_13);
                        difference.register.sbyte_14 = (sbyte)(left.register.sbyte_14 - right.register.sbyte_14);
                        difference.register.sbyte_15 = (sbyte)(left.register.sbyte_15 - right.register.sbyte_15);
                    }
                    else if (typeof(T) == typeof(ushort))
                    {
                        difference.register.uint16_0 = (ushort)(left.register.uint16_0 - right.register.uint16_0);
                        difference.register.uint16_1 = (ushort)(left.register.uint16_1 - right.register.uint16_1);
                        difference.register.uint16_2 = (ushort)(left.register.uint16_2 - right.register.uint16_2);
                        difference.register.uint16_3 = (ushort)(left.register.uint16_3 - right.register.uint16_3);
                        difference.register.uint16_4 = (ushort)(left.register.uint16_4 - right.register.uint16_4);
                        difference.register.uint16_5 = (ushort)(left.register.uint16_5 - right.register.uint16_5);
                        difference.register.uint16_6 = (ushort)(left.register.uint16_6 - right.register.uint16_6);
                        difference.register.uint16_7 = (ushort)(left.register.uint16_7 - right.register.uint16_7);
                    }
                    else if (typeof(T) == typeof(short))
                    {
                        difference.register.int16_0 = (short)(left.register.int16_0 - right.register.int16_0);
                        difference.register.int16_1 = (short)(left.register.int16_1 - right.register.int16_1);
                        difference.register.int16_2 = (short)(left.register.int16_2 - right.register.int16_2);
                        difference.register.int16_3 = (short)(left.register.int16_3 - right.register.int16_3);
                        difference.register.int16_4 = (short)(left.register.int16_4 - right.register.int16_4);
                        difference.register.int16_5 = (short)(left.register.int16_5 - right.register.int16_5);
                        difference.register.int16_6 = (short)(left.register.int16_6 - right.register.int16_6);
                        difference.register.int16_7 = (short)(left.register.int16_7 - right.register.int16_7);
                    }
                    else if (typeof(T) == typeof(uint))
                    {
                        difference.register.uint32_0 = (uint)(left.register.uint32_0 - right.register.uint32_0);
                        difference.register.uint32_1 = (uint)(left.register.uint32_1 - right.register.uint32_1);
                        difference.register.uint32_2 = (uint)(left.register.uint32_2 - right.register.uint32_2);
                        difference.register.uint32_3 = (uint)(left.register.uint32_3 - right.register.uint32_3);
                    }
                    else if (typeof(T) == typeof(int))
                    {
                        difference.register.int32_0 = (int)(left.register.int32_0 - right.register.int32_0);
                        difference.register.int32_1 = (int)(left.register.int32_1 - right.register.int32_1);
                        difference.register.int32_2 = (int)(left.register.int32_2 - right.register.int32_2);
                        difference.register.int32_3 = (int)(left.register.int32_3 - right.register.int32_3);
                    }
                    else if (typeof(T) == typeof(ulong))
                    {
                        difference.register.uint64_0 = (ulong)(left.register.uint64_0 - right.register.uint64_0);
                        difference.register.uint64_1 = (ulong)(left.register.uint64_1 - right.register.uint64_1);
                    }
                    else if (typeof(T) == typeof(long))
                    {
                        difference.register.int64_0 = (long)(left.register.int64_0 - right.register.int64_0);
                        difference.register.int64_1 = (long)(left.register.int64_1 - right.register.int64_1);
                    }
                    else if (typeof(T) == typeof(float))
                    {
                        difference.register.single_0 = (float)(left.register.single_0 - right.register.single_0);
                        difference.register.single_1 = (float)(left.register.single_1 - right.register.single_1);
                        difference.register.single_2 = (float)(left.register.single_2 - right.register.single_2);
                        difference.register.single_3 = (float)(left.register.single_3 - right.register.single_3);
                    }
                    else if (typeof(T) == typeof(double))
                    {
                        difference.register.double_0 = (double)(left.register.double_0 - right.register.double_0);
                        difference.register.double_1 = (double)(left.register.double_1 - right.register.double_1);
                    }
                    return difference;
                }
            }
        }

        // This method is intrinsic only for certain types. It cannot access fields directly unless we are sure the context is unaccelerated.
        /// <summary>
        /// Multiplies two vectors together.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The product vector.</returns>
        [Intrinsic]
        public static unsafe Vector<T> operator *(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    // This is an intrinsic operation and will not result in a recursive call.
                    return left * right;
                }
                else
                {
                    Vector<T> product = new Vector<T>();
                    if (typeof(T) == typeof(byte))
                    {
                        product.register.byte_0 = (byte)(left.register.byte_0 * right.register.byte_0);
                        product.register.byte_1 = (byte)(left.register.byte_1 * right.register.byte_1);
                        product.register.byte_2 = (byte)(left.register.byte_2 * right.register.byte_2);
                        product.register.byte_3 = (byte)(left.register.byte_3 * right.register.byte_3);
                        product.register.byte_4 = (byte)(left.register.byte_4 * right.register.byte_4);
                        product.register.byte_5 = (byte)(left.register.byte_5 * right.register.byte_5);
                        product.register.byte_6 = (byte)(left.register.byte_6 * right.register.byte_6);
                        product.register.byte_7 = (byte)(left.register.byte_7 * right.register.byte_7);
                        product.register.byte_8 = (byte)(left.register.byte_8 * right.register.byte_8);
                        product.register.byte_9 = (byte)(left.register.byte_9 * right.register.byte_9);
                        product.register.byte_10 = (byte)(left.register.byte_10 * right.register.byte_10);
                        product.register.byte_11 = (byte)(left.register.byte_11 * right.register.byte_11);
                        product.register.byte_12 = (byte)(left.register.byte_12 * right.register.byte_12);
                        product.register.byte_13 = (byte)(left.register.byte_13 * right.register.byte_13);
                        product.register.byte_14 = (byte)(left.register.byte_14 * right.register.byte_14);
                        product.register.byte_15 = (byte)(left.register.byte_15 * right.register.byte_15);
                    }
                    else if (typeof(T) == typeof(sbyte))
                    {
                        product.register.sbyte_0 = (sbyte)(left.register.sbyte_0 * right.register.sbyte_0);
                        product.register.sbyte_1 = (sbyte)(left.register.sbyte_1 * right.register.sbyte_1);
                        product.register.sbyte_2 = (sbyte)(left.register.sbyte_2 * right.register.sbyte_2);
                        product.register.sbyte_3 = (sbyte)(left.register.sbyte_3 * right.register.sbyte_3);
                        product.register.sbyte_4 = (sbyte)(left.register.sbyte_4 * right.register.sbyte_4);
                        product.register.sbyte_5 = (sbyte)(left.register.sbyte_5 * right.register.sbyte_5);
                        product.register.sbyte_6 = (sbyte)(left.register.sbyte_6 * right.register.sbyte_6);
                        product.register.sbyte_7 = (sbyte)(left.register.sbyte_7 * right.register.sbyte_7);
                        product.register.sbyte_8 = (sbyte)(left.register.sbyte_8 * right.register.sbyte_8);
                        product.register.sbyte_9 = (sbyte)(left.register.sbyte_9 * right.register.sbyte_9);
                        product.register.sbyte_10 = (sbyte)(left.register.sbyte_10 * right.register.sbyte_10);
                        product.register.sbyte_11 = (sbyte)(left.register.sbyte_11 * right.register.sbyte_11);
                        product.register.sbyte_12 = (sbyte)(left.register.sbyte_12 * right.register.sbyte_12);
                        product.register.sbyte_13 = (sbyte)(left.register.sbyte_13 * right.register.sbyte_13);
                        product.register.sbyte_14 = (sbyte)(left.register.sbyte_14 * right.register.sbyte_14);
                        product.register.sbyte_15 = (sbyte)(left.register.sbyte_15 * right.register.sbyte_15);
                    }
                    else if (typeof(T) == typeof(ushort))
                    {
                        product.register.uint16_0 = (ushort)(left.register.uint16_0 * right.register.uint16_0);
                        product.register.uint16_1 = (ushort)(left.register.uint16_1 * right.register.uint16_1);
                        product.register.uint16_2 = (ushort)(left.register.uint16_2 * right.register.uint16_2);
                        product.register.uint16_3 = (ushort)(left.register.uint16_3 * right.register.uint16_3);
                        product.register.uint16_4 = (ushort)(left.register.uint16_4 * right.register.uint16_4);
                        product.register.uint16_5 = (ushort)(left.register.uint16_5 * right.register.uint16_5);
                        product.register.uint16_6 = (ushort)(left.register.uint16_6 * right.register.uint16_6);
                        product.register.uint16_7 = (ushort)(left.register.uint16_7 * right.register.uint16_7);
                    }
                    else if (typeof(T) == typeof(short))
                    {
                        product.register.int16_0 = (short)(left.register.int16_0 * right.register.int16_0);
                        product.register.int16_1 = (short)(left.register.int16_1 * right.register.int16_1);
                        product.register.int16_2 = (short)(left.register.int16_2 * right.register.int16_2);
                        product.register.int16_3 = (short)(left.register.int16_3 * right.register.int16_3);
                        product.register.int16_4 = (short)(left.register.int16_4 * right.register.int16_4);
                        product.register.int16_5 = (short)(left.register.int16_5 * right.register.int16_5);
                        product.register.int16_6 = (short)(left.register.int16_6 * right.register.int16_6);
                        product.register.int16_7 = (short)(left.register.int16_7 * right.register.int16_7);
                    }
                    else if (typeof(T) == typeof(uint))
                    {
                        product.register.uint32_0 = (uint)(left.register.uint32_0 * right.register.uint32_0);
                        product.register.uint32_1 = (uint)(left.register.uint32_1 * right.register.uint32_1);
                        product.register.uint32_2 = (uint)(left.register.uint32_2 * right.register.uint32_2);
                        product.register.uint32_3 = (uint)(left.register.uint32_3 * right.register.uint32_3);
                    }
                    else if (typeof(T) == typeof(int))
                    {
                        product.register.int32_0 = (int)(left.register.int32_0 * right.register.int32_0);
                        product.register.int32_1 = (int)(left.register.int32_1 * right.register.int32_1);
                        product.register.int32_2 = (int)(left.register.int32_2 * right.register.int32_2);
                        product.register.int32_3 = (int)(left.register.int32_3 * right.register.int32_3);
                    }
                    else if (typeof(T) == typeof(ulong))
                    {
                        product.register.uint64_0 = (ulong)(left.register.uint64_0 * right.register.uint64_0);
                        product.register.uint64_1 = (ulong)(left.register.uint64_1 * right.register.uint64_1);
                    }
                    else if (typeof(T) == typeof(long))
                    {
                        product.register.int64_0 = (long)(left.register.int64_0 * right.register.int64_0);
                        product.register.int64_1 = (long)(left.register.int64_1 * right.register.int64_1);
                    }
                    else if (typeof(T) == typeof(float))
                    {
                        product.register.single_0 = (float)(left.register.single_0 * right.register.single_0);
                        product.register.single_1 = (float)(left.register.single_1 * right.register.single_1);
                        product.register.single_2 = (float)(left.register.single_2 * right.register.single_2);
                        product.register.single_3 = (float)(left.register.single_3 * right.register.single_3);
                    }
                    else if (typeof(T) == typeof(double))
                    {
                        product.register.double_0 = (double)(left.register.double_0 * right.register.double_0);
                        product.register.double_1 = (double)(left.register.double_1 * right.register.double_1);
                    }
                    return product;
                }
            }
        }

        /// <summary>
        /// Multiplies a vector by the given scalar.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <param name="factor">The scalar value.</param>
        /// <returns>The scaled vector.</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static Vector<T> operator *(Vector<T> value, T factor)
        {
            return new Vector<T>(factor) * value;
        }

        /// <summary>
        /// Multiplies a vector by the given scalar.
        /// </summary>
        /// <param name="factor">The scalar value.</param>
        /// <param name="value">The source vector.</param>
        /// <returns>The scaled vector.</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static Vector<T> operator *(T factor, Vector<T> value)
        {
            return new Vector<T>(factor) * value;
        }

        // This method is intrinsic only for certain types. It cannot access fields directly unless we are sure the context is unaccelerated.
        /// <summary>
        /// Divides the first vector by the second.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The vector resulting from the division.</returns>
        [Intrinsic]
        public static unsafe Vector<T> operator /(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    // This is an intrinsic operation and will not result in a recursive call.
                    return left / right;
                }
                else
                {
                    Vector<T> quotient = new Vector<T>();
                    if (typeof(T) == typeof(byte))
                    {
                        quotient.register.byte_0 = (byte)(left.register.byte_0 / right.register.byte_0);
                        quotient.register.byte_1 = (byte)(left.register.byte_1 / right.register.byte_1);
                        quotient.register.byte_2 = (byte)(left.register.byte_2 / right.register.byte_2);
                        quotient.register.byte_3 = (byte)(left.register.byte_3 / right.register.byte_3);
                        quotient.register.byte_4 = (byte)(left.register.byte_4 / right.register.byte_4);
                        quotient.register.byte_5 = (byte)(left.register.byte_5 / right.register.byte_5);
                        quotient.register.byte_6 = (byte)(left.register.byte_6 / right.register.byte_6);
                        quotient.register.byte_7 = (byte)(left.register.byte_7 / right.register.byte_7);
                        quotient.register.byte_8 = (byte)(left.register.byte_8 / right.register.byte_8);
                        quotient.register.byte_9 = (byte)(left.register.byte_9 / right.register.byte_9);
                        quotient.register.byte_10 = (byte)(left.register.byte_10 / right.register.byte_10);
                        quotient.register.byte_11 = (byte)(left.register.byte_11 / right.register.byte_11);
                        quotient.register.byte_12 = (byte)(left.register.byte_12 / right.register.byte_12);
                        quotient.register.byte_13 = (byte)(left.register.byte_13 / right.register.byte_13);
                        quotient.register.byte_14 = (byte)(left.register.byte_14 / right.register.byte_14);
                        quotient.register.byte_15 = (byte)(left.register.byte_15 / right.register.byte_15);
                    }
                    else if (typeof(T) == typeof(sbyte))
                    {
                        quotient.register.sbyte_0 = (sbyte)(left.register.sbyte_0 / right.register.sbyte_0);
                        quotient.register.sbyte_1 = (sbyte)(left.register.sbyte_1 / right.register.sbyte_1);
                        quotient.register.sbyte_2 = (sbyte)(left.register.sbyte_2 / right.register.sbyte_2);
                        quotient.register.sbyte_3 = (sbyte)(left.register.sbyte_3 / right.register.sbyte_3);
                        quotient.register.sbyte_4 = (sbyte)(left.register.sbyte_4 / right.register.sbyte_4);
                        quotient.register.sbyte_5 = (sbyte)(left.register.sbyte_5 / right.register.sbyte_5);
                        quotient.register.sbyte_6 = (sbyte)(left.register.sbyte_6 / right.register.sbyte_6);
                        quotient.register.sbyte_7 = (sbyte)(left.register.sbyte_7 / right.register.sbyte_7);
                        quotient.register.sbyte_8 = (sbyte)(left.register.sbyte_8 / right.register.sbyte_8);
                        quotient.register.sbyte_9 = (sbyte)(left.register.sbyte_9 / right.register.sbyte_9);
                        quotient.register.sbyte_10 = (sbyte)(left.register.sbyte_10 / right.register.sbyte_10);
                        quotient.register.sbyte_11 = (sbyte)(left.register.sbyte_11 / right.register.sbyte_11);
                        quotient.register.sbyte_12 = (sbyte)(left.register.sbyte_12 / right.register.sbyte_12);
                        quotient.register.sbyte_13 = (sbyte)(left.register.sbyte_13 / right.register.sbyte_13);
                        quotient.register.sbyte_14 = (sbyte)(left.register.sbyte_14 / right.register.sbyte_14);
                        quotient.register.sbyte_15 = (sbyte)(left.register.sbyte_15 / right.register.sbyte_15);
                    }
                    else if (typeof(T) == typeof(ushort))
                    {
                        quotient.register.uint16_0 = (ushort)(left.register.uint16_0 / right.register.uint16_0);
                        quotient.register.uint16_1 = (ushort)(left.register.uint16_1 / right.register.uint16_1);
                        quotient.register.uint16_2 = (ushort)(left.register.uint16_2 / right.register.uint16_2);
                        quotient.register.uint16_3 = (ushort)(left.register.uint16_3 / right.register.uint16_3);
                        quotient.register.uint16_4 = (ushort)(left.register.uint16_4 / right.register.uint16_4);
                        quotient.register.uint16_5 = (ushort)(left.register.uint16_5 / right.register.uint16_5);
                        quotient.register.uint16_6 = (ushort)(left.register.uint16_6 / right.register.uint16_6);
                        quotient.register.uint16_7 = (ushort)(left.register.uint16_7 / right.register.uint16_7);
                    }
                    else if (typeof(T) == typeof(short))
                    {
                        quotient.register.int16_0 = (short)(left.register.int16_0 / right.register.int16_0);
                        quotient.register.int16_1 = (short)(left.register.int16_1 / right.register.int16_1);
                        quotient.register.int16_2 = (short)(left.register.int16_2 / right.register.int16_2);
                        quotient.register.int16_3 = (short)(left.register.int16_3 / right.register.int16_3);
                        quotient.register.int16_4 = (short)(left.register.int16_4 / right.register.int16_4);
                        quotient.register.int16_5 = (short)(left.register.int16_5 / right.register.int16_5);
                        quotient.register.int16_6 = (short)(left.register.int16_6 / right.register.int16_6);
                        quotient.register.int16_7 = (short)(left.register.int16_7 / right.register.int16_7);
                    }
                    else if (typeof(T) == typeof(uint))
                    {
                        quotient.register.uint32_0 = (uint)(left.register.uint32_0 / right.register.uint32_0);
                        quotient.register.uint32_1 = (uint)(left.register.uint32_1 / right.register.uint32_1);
                        quotient.register.uint32_2 = (uint)(left.register.uint32_2 / right.register.uint32_2);
                        quotient.register.uint32_3 = (uint)(left.register.uint32_3 / right.register.uint32_3);
                    }
                    else if (typeof(T) == typeof(int))
                    {
                        quotient.register.int32_0 = (int)(left.register.int32_0 / right.register.int32_0);
                        quotient.register.int32_1 = (int)(left.register.int32_1 / right.register.int32_1);
                        quotient.register.int32_2 = (int)(left.register.int32_2 / right.register.int32_2);
                        quotient.register.int32_3 = (int)(left.register.int32_3 / right.register.int32_3);
                    }
                    else if (typeof(T) == typeof(ulong))
                    {
                        quotient.register.uint64_0 = (ulong)(left.register.uint64_0 / right.register.uint64_0);
                        quotient.register.uint64_1 = (ulong)(left.register.uint64_1 / right.register.uint64_1);
                    }
                    else if (typeof(T) == typeof(long))
                    {
                        quotient.register.int64_0 = (long)(left.register.int64_0 / right.register.int64_0);
                        quotient.register.int64_1 = (long)(left.register.int64_1 / right.register.int64_1);
                    }
                    else if (typeof(T) == typeof(float))
                    {
                        quotient.register.single_0 = (float)(left.register.single_0 / right.register.single_0);
                        quotient.register.single_1 = (float)(left.register.single_1 / right.register.single_1);
                        quotient.register.single_2 = (float)(left.register.single_2 / right.register.single_2);
                        quotient.register.single_3 = (float)(left.register.single_3 / right.register.single_3);
                    }
                    else if (typeof(T) == typeof(double))
                    {
                        quotient.register.double_0 = (double)(left.register.double_0 / right.register.double_0);
                        quotient.register.double_1 = (double)(left.register.double_1 / right.register.double_1);
                    }
                    return quotient;
                }
            }
        }

        /// <summary>
        /// Negates a given vector.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The negated vector.</returns>
        public static Vector<T> operator -(Vector<T> value)
        {
            return Zero - value;
        }
        #endregion Arithmetic Operators

        #region Bitwise Operators
        /// <summary>
        /// Returns a new vector by performing a bitwise-and operation on each of the elements in the given vectors.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The resultant vector.</returns>
        [Intrinsic]
        public static unsafe Vector<T> operator &(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    // This is an intrinsic operation and will not result in a recursive call.
                    return left & right;
                }
                else
                {
                    Vector<T> result = new Vector<T>();
                    result.register.int64_0 = left.register.int64_0 & right.register.int64_0;
                    result.register.int64_1 = left.register.int64_1 & right.register.int64_1;
                    return result;
                }
            }
        }

        /// <summary>
        /// Returns a new vector by performing a bitwise-or operation on each of the elements in the given vectors.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The resultant vector.</returns>
        [Intrinsic]
        public static unsafe Vector<T> operator |(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            Vector<T> result = new Vector<T>();
            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    // This is an intrinsic operation and will not result in a recursive call.
                    return left | right;
                }
                else
                {
                    result.register.int64_0 = left.register.int64_0 | right.register.int64_0;
                    result.register.int64_1 = left.register.int64_1 | right.register.int64_1;
                }
            }
            return result;
        }

        /// <summary>
        /// Returns a new vector by performing a bitwise-exclusive-or operation on each of the elements in the given vectors.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <returns>The resultant vector.</returns>
        [Intrinsic]
        public static unsafe Vector<T> operator ^(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            Vector<T> result = new Vector<T>();
            unchecked
            {
                if (Vector.IsHardwareAccelerated)
                {
                    // This is an intrinsic operation and will not result in a recursive call.
                    return left ^ right;
                }
                else
                {
                    result.register.int64_0 = left.register.int64_0 ^ right.register.int64_0;
                    result.register.int64_1 = left.register.int64_1 ^ right.register.int64_1;
                }
            }
            return result;
        }

        /// <summary>
        /// Returns a new vector whose elements are obtained by taking the one's complement of the given vector's elements.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The one's complement vector.</returns>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static Vector<T> operator ~(Vector<T> value)
        {
            return s_allOnes ^ value;
        }
        #endregion Bitwise Operators

        #region Logical Operators
        /// <summary>
        /// Returns a boolean indicating whether each pair of elements in the given vectors are equal.
        /// </summary>
        /// <param name="left">The first vector to compare.</param>
        /// <param name="right">The first vector to compare.</param>
        /// <returns>True if all elements are equal; False otherwise.</returns>
        [Intrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector<T> left, Vector<T> right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean indicating whether any single pair of elements in the given vectors are not equal.
        /// </summary>
        /// <param name="left">The first vector to compare.</param>
        /// <param name="right">The second vector to compare.</param>
        /// <returns>True if left and right are not equal; False otherwise.</returns>
        [Intrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector<T> left, Vector<T> right)
        {
            return !(left == right);
        }
        #endregion Logical Operators

        #region Conversions
        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [Intrinsic]
        public static explicit operator Vector<byte>(Vector<T> value)
        {
            return new Vector<byte>(ref value.register);
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [CLSCompliant(false)]
        [Intrinsic]
        public static explicit operator Vector<sbyte>(Vector<T> value)
        {
            return new Vector<sbyte>(ref value.register);
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [CLSCompliant(false)]
        [Intrinsic]
        public static explicit operator Vector<ushort>(Vector<T> value)
        {
            return new Vector<ushort>(ref value.register);
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [Intrinsic]
        public static explicit operator Vector<short>(Vector<T> value)
        {
            return new Vector<short>(ref value.register);
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [CLSCompliant(false)]
        [Intrinsic]
        public static explicit operator Vector<uint>(Vector<T> value)
        {
            return new Vector<uint>(ref value.register);
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [Intrinsic]
        public static explicit operator Vector<int>(Vector<T> value)
        {
            return new Vector<int>(ref value.register);
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [CLSCompliant(false)]
        [Intrinsic]
        public static explicit operator Vector<ulong>(Vector<T> value)
        {
            return new Vector<ulong>(ref value.register);
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [Intrinsic]
        public static explicit operator Vector<long>(Vector<T> value)
        {
            return new Vector<long>(ref value.register);
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [Intrinsic]
        public static explicit operator Vector<float>(Vector<T> value)
        {
            return new Vector<float>(ref value.register);
        }

        /// <summary>
        /// Reinterprets the bits of the given vector into those of another type.
        /// </summary>
        /// <param name="value">The source vector</param>
        /// <returns>The reinterpreted vector.</returns>
        [Intrinsic]
        public static explicit operator Vector<double>(Vector<T> value)
        {
            return new Vector<double>(ref value.register);
        }

        #endregion Conversions

        #region Internal Comparison Methods
        [Intrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        internal static unsafe Vector<T> Equals(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            if (Vector.IsHardwareAccelerated)
            {
                // This is an intrinsic operation and will not result in a recursive call.
                return Equals(left, right);
            }
            else
            {
                Register register = new Register();
                if (typeof(T) == typeof(byte))
                {
                    register.byte_0 = left.register.byte_0 == right.register.byte_0 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_1 = left.register.byte_1 == right.register.byte_1 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_2 = left.register.byte_2 == right.register.byte_2 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_3 = left.register.byte_3 == right.register.byte_3 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_4 = left.register.byte_4 == right.register.byte_4 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_5 = left.register.byte_5 == right.register.byte_5 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_6 = left.register.byte_6 == right.register.byte_6 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_7 = left.register.byte_7 == right.register.byte_7 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_8 = left.register.byte_8 == right.register.byte_8 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_9 = left.register.byte_9 == right.register.byte_9 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_10 = left.register.byte_10 == right.register.byte_10 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_11 = left.register.byte_11 == right.register.byte_11 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_12 = left.register.byte_12 == right.register.byte_12 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_13 = left.register.byte_13 == right.register.byte_13 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_14 = left.register.byte_14 == right.register.byte_14 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_15 = left.register.byte_15 == right.register.byte_15 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                }
                else if (typeof(T) == typeof(sbyte))
                {
                    register.sbyte_0 = left.register.sbyte_0 == right.register.sbyte_0 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_1 = left.register.sbyte_1 == right.register.sbyte_1 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_2 = left.register.sbyte_2 == right.register.sbyte_2 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_3 = left.register.sbyte_3 == right.register.sbyte_3 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_4 = left.register.sbyte_4 == right.register.sbyte_4 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_5 = left.register.sbyte_5 == right.register.sbyte_5 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_6 = left.register.sbyte_6 == right.register.sbyte_6 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_7 = left.register.sbyte_7 == right.register.sbyte_7 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_8 = left.register.sbyte_8 == right.register.sbyte_8 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_9 = left.register.sbyte_9 == right.register.sbyte_9 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_10 = left.register.sbyte_10 == right.register.sbyte_10 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_11 = left.register.sbyte_11 == right.register.sbyte_11 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_12 = left.register.sbyte_12 == right.register.sbyte_12 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_13 = left.register.sbyte_13 == right.register.sbyte_13 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_14 = left.register.sbyte_14 == right.register.sbyte_14 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_15 = left.register.sbyte_15 == right.register.sbyte_15 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                }
                else if (typeof(T) == typeof(ushort))
                {
                    register.uint16_0 = left.register.uint16_0 == right.register.uint16_0 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_1 = left.register.uint16_1 == right.register.uint16_1 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_2 = left.register.uint16_2 == right.register.uint16_2 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_3 = left.register.uint16_3 == right.register.uint16_3 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_4 = left.register.uint16_4 == right.register.uint16_4 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_5 = left.register.uint16_5 == right.register.uint16_5 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_6 = left.register.uint16_6 == right.register.uint16_6 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_7 = left.register.uint16_7 == right.register.uint16_7 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                }
                else if (typeof(T) == typeof(short))
                {
                    register.int16_0 = left.register.int16_0 == right.register.int16_0 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_1 = left.register.int16_1 == right.register.int16_1 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_2 = left.register.int16_2 == right.register.int16_2 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_3 = left.register.int16_3 == right.register.int16_3 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_4 = left.register.int16_4 == right.register.int16_4 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_5 = left.register.int16_5 == right.register.int16_5 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_6 = left.register.int16_6 == right.register.int16_6 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_7 = left.register.int16_7 == right.register.int16_7 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                }
                else if (typeof(T) == typeof(uint))
                {
                    register.uint32_0 = left.register.uint32_0 == right.register.uint32_0 ? ConstantHelper.GetUInt32WithAllBitsSet() : (uint)0;
                    register.uint32_1 = left.register.uint32_1 == right.register.uint32_1 ? ConstantHelper.GetUInt32WithAllBitsSet() : (uint)0;
                    register.uint32_2 = left.register.uint32_2 == right.register.uint32_2 ? ConstantHelper.GetUInt32WithAllBitsSet() : (uint)0;
                    register.uint32_3 = left.register.uint32_3 == right.register.uint32_3 ? ConstantHelper.GetUInt32WithAllBitsSet() : (uint)0;
                }
                else if (typeof(T) == typeof(int))
                {
                    register.int32_0 = left.register.int32_0 == right.register.int32_0 ? ConstantHelper.GetInt32WithAllBitsSet() : (int)0;
                    register.int32_1 = left.register.int32_1 == right.register.int32_1 ? ConstantHelper.GetInt32WithAllBitsSet() : (int)0;
                    register.int32_2 = left.register.int32_2 == right.register.int32_2 ? ConstantHelper.GetInt32WithAllBitsSet() : (int)0;
                    register.int32_3 = left.register.int32_3 == right.register.int32_3 ? ConstantHelper.GetInt32WithAllBitsSet() : (int)0;
                }
                else if (typeof(T) == typeof(ulong))
                {
                    register.uint64_0 = left.register.uint64_0 == right.register.uint64_0 ? ConstantHelper.GetUInt64WithAllBitsSet() : (ulong)0;
                    register.uint64_1 = left.register.uint64_1 == right.register.uint64_1 ? ConstantHelper.GetUInt64WithAllBitsSet() : (ulong)0;
                }
                else if (typeof(T) == typeof(long))
                {
                    register.int64_0 = left.register.int64_0 == right.register.int64_0 ? ConstantHelper.GetInt64WithAllBitsSet() : (long)0;
                    register.int64_1 = left.register.int64_1 == right.register.int64_1 ? ConstantHelper.GetInt64WithAllBitsSet() : (long)0;
                }
                else if (typeof(T) == typeof(float))
                {
                    register.single_0 = left.register.single_0 == right.register.single_0 ? ConstantHelper.GetSingleWithAllBitsSet() : (float)0;
                    register.single_1 = left.register.single_1 == right.register.single_1 ? ConstantHelper.GetSingleWithAllBitsSet() : (float)0;
                    register.single_2 = left.register.single_2 == right.register.single_2 ? ConstantHelper.GetSingleWithAllBitsSet() : (float)0;
                    register.single_3 = left.register.single_3 == right.register.single_3 ? ConstantHelper.GetSingleWithAllBitsSet() : (float)0;
                }
                else if (typeof(T) == typeof(double))
                {
                    register.double_0 = left.register.double_0 == right.register.double_0 ? ConstantHelper.GetDoubleWithAllBitsSet() : (double)0;
                    register.double_1 = left.register.double_1 == right.register.double_1 ? ConstantHelper.GetDoubleWithAllBitsSet() : (double)0;
                }
                return new Vector<T>(ref register);
            }
        }

        [Intrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        internal static unsafe Vector<T> LessThan(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            if (Vector.IsHardwareAccelerated)
            {
                // This is an intrinsic operation and will not result in a recursive call.
                return LessThan(left, right);
            }
            else
            {
                Register register = new Register();
                if (typeof(T) == typeof(byte))
                {
                    register.byte_0 = left.register.byte_0 < right.register.byte_0 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_1 = left.register.byte_1 < right.register.byte_1 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_2 = left.register.byte_2 < right.register.byte_2 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_3 = left.register.byte_3 < right.register.byte_3 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_4 = left.register.byte_4 < right.register.byte_4 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_5 = left.register.byte_5 < right.register.byte_5 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_6 = left.register.byte_6 < right.register.byte_6 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_7 = left.register.byte_7 < right.register.byte_7 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_8 = left.register.byte_8 < right.register.byte_8 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_9 = left.register.byte_9 < right.register.byte_9 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_10 = left.register.byte_10 < right.register.byte_10 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_11 = left.register.byte_11 < right.register.byte_11 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_12 = left.register.byte_12 < right.register.byte_12 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_13 = left.register.byte_13 < right.register.byte_13 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_14 = left.register.byte_14 < right.register.byte_14 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_15 = left.register.byte_15 < right.register.byte_15 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                }
                else if (typeof(T) == typeof(sbyte))
                {
                    register.sbyte_0 = left.register.sbyte_0 < right.register.sbyte_0 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_1 = left.register.sbyte_1 < right.register.sbyte_1 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_2 = left.register.sbyte_2 < right.register.sbyte_2 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_3 = left.register.sbyte_3 < right.register.sbyte_3 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_4 = left.register.sbyte_4 < right.register.sbyte_4 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_5 = left.register.sbyte_5 < right.register.sbyte_5 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_6 = left.register.sbyte_6 < right.register.sbyte_6 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_7 = left.register.sbyte_7 < right.register.sbyte_7 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_8 = left.register.sbyte_8 < right.register.sbyte_8 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_9 = left.register.sbyte_9 < right.register.sbyte_9 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_10 = left.register.sbyte_10 < right.register.sbyte_10 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_11 = left.register.sbyte_11 < right.register.sbyte_11 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_12 = left.register.sbyte_12 < right.register.sbyte_12 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_13 = left.register.sbyte_13 < right.register.sbyte_13 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_14 = left.register.sbyte_14 < right.register.sbyte_14 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_15 = left.register.sbyte_15 < right.register.sbyte_15 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                }
                else if (typeof(T) == typeof(ushort))
                {
                    register.uint16_0 = left.register.uint16_0 < right.register.uint16_0 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_1 = left.register.uint16_1 < right.register.uint16_1 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_2 = left.register.uint16_2 < right.register.uint16_2 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_3 = left.register.uint16_3 < right.register.uint16_3 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_4 = left.register.uint16_4 < right.register.uint16_4 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_5 = left.register.uint16_5 < right.register.uint16_5 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_6 = left.register.uint16_6 < right.register.uint16_6 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_7 = left.register.uint16_7 < right.register.uint16_7 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                }
                else if (typeof(T) == typeof(short))
                {
                    register.int16_0 = left.register.int16_0 < right.register.int16_0 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_1 = left.register.int16_1 < right.register.int16_1 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_2 = left.register.int16_2 < right.register.int16_2 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_3 = left.register.int16_3 < right.register.int16_3 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_4 = left.register.int16_4 < right.register.int16_4 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_5 = left.register.int16_5 < right.register.int16_5 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_6 = left.register.int16_6 < right.register.int16_6 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_7 = left.register.int16_7 < right.register.int16_7 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                }
                else if (typeof(T) == typeof(uint))
                {
                    register.uint32_0 = left.register.uint32_0 < right.register.uint32_0 ? ConstantHelper.GetUInt32WithAllBitsSet() : (uint)0;
                    register.uint32_1 = left.register.uint32_1 < right.register.uint32_1 ? ConstantHelper.GetUInt32WithAllBitsSet() : (uint)0;
                    register.uint32_2 = left.register.uint32_2 < right.register.uint32_2 ? ConstantHelper.GetUInt32WithAllBitsSet() : (uint)0;
                    register.uint32_3 = left.register.uint32_3 < right.register.uint32_3 ? ConstantHelper.GetUInt32WithAllBitsSet() : (uint)0;
                }
                else if (typeof(T) == typeof(int))
                {
                    register.int32_0 = left.register.int32_0 < right.register.int32_0 ? ConstantHelper.GetInt32WithAllBitsSet() : (int)0;
                    register.int32_1 = left.register.int32_1 < right.register.int32_1 ? ConstantHelper.GetInt32WithAllBitsSet() : (int)0;
                    register.int32_2 = left.register.int32_2 < right.register.int32_2 ? ConstantHelper.GetInt32WithAllBitsSet() : (int)0;
                    register.int32_3 = left.register.int32_3 < right.register.int32_3 ? ConstantHelper.GetInt32WithAllBitsSet() : (int)0;
                }
                else if (typeof(T) == typeof(ulong))
                {
                    register.uint64_0 = left.register.uint64_0 < right.register.uint64_0 ? ConstantHelper.GetUInt64WithAllBitsSet() : (ulong)0;
                    register.uint64_1 = left.register.uint64_1 < right.register.uint64_1 ? ConstantHelper.GetUInt64WithAllBitsSet() : (ulong)0;
                }
                else if (typeof(T) == typeof(long))
                {
                    register.int64_0 = left.register.int64_0 < right.register.int64_0 ? ConstantHelper.GetInt64WithAllBitsSet() : (long)0;
                    register.int64_1 = left.register.int64_1 < right.register.int64_1 ? ConstantHelper.GetInt64WithAllBitsSet() : (long)0;
                }
                else if (typeof(T) == typeof(float))
                {
                    register.single_0 = left.register.single_0 < right.register.single_0 ? ConstantHelper.GetSingleWithAllBitsSet() : (float)0;
                    register.single_1 = left.register.single_1 < right.register.single_1 ? ConstantHelper.GetSingleWithAllBitsSet() : (float)0;
                    register.single_2 = left.register.single_2 < right.register.single_2 ? ConstantHelper.GetSingleWithAllBitsSet() : (float)0;
                    register.single_3 = left.register.single_3 < right.register.single_3 ? ConstantHelper.GetSingleWithAllBitsSet() : (float)0;
                }
                else if (typeof(T) == typeof(double))
                {
                    register.double_0 = left.register.double_0 < right.register.double_0 ? ConstantHelper.GetDoubleWithAllBitsSet() : (double)0;
                    register.double_1 = left.register.double_1 < right.register.double_1 ? ConstantHelper.GetDoubleWithAllBitsSet() : (double)0;
                }
                return new Vector<T>(ref register);
            }
        }

        [Intrinsic]
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        internal static unsafe Vector<T> GreaterThan(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            if (Vector.IsHardwareAccelerated)
            {
                // This is an intrinsic operation and will not result in a recursive call.
                return GreaterThan(left, right);
            }
            else
            {
                Register register = new Register();
                if (typeof(T) == typeof(byte))
                {
                    register.byte_0 = left.register.byte_0 > right.register.byte_0 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_1 = left.register.byte_1 > right.register.byte_1 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_2 = left.register.byte_2 > right.register.byte_2 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_3 = left.register.byte_3 > right.register.byte_3 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_4 = left.register.byte_4 > right.register.byte_4 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_5 = left.register.byte_5 > right.register.byte_5 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_6 = left.register.byte_6 > right.register.byte_6 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_7 = left.register.byte_7 > right.register.byte_7 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_8 = left.register.byte_8 > right.register.byte_8 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_9 = left.register.byte_9 > right.register.byte_9 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_10 = left.register.byte_10 > right.register.byte_10 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_11 = left.register.byte_11 > right.register.byte_11 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_12 = left.register.byte_12 > right.register.byte_12 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_13 = left.register.byte_13 > right.register.byte_13 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_14 = left.register.byte_14 > right.register.byte_14 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                    register.byte_15 = left.register.byte_15 > right.register.byte_15 ? ConstantHelper.GetByteWithAllBitsSet() : (byte)0;
                }
                else if (typeof(T) == typeof(sbyte))
                {
                    register.sbyte_0 = left.register.sbyte_0 > right.register.sbyte_0 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_1 = left.register.sbyte_1 > right.register.sbyte_1 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_2 = left.register.sbyte_2 > right.register.sbyte_2 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_3 = left.register.sbyte_3 > right.register.sbyte_3 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_4 = left.register.sbyte_4 > right.register.sbyte_4 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_5 = left.register.sbyte_5 > right.register.sbyte_5 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_6 = left.register.sbyte_6 > right.register.sbyte_6 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_7 = left.register.sbyte_7 > right.register.sbyte_7 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_8 = left.register.sbyte_8 > right.register.sbyte_8 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_9 = left.register.sbyte_9 > right.register.sbyte_9 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_10 = left.register.sbyte_10 > right.register.sbyte_10 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_11 = left.register.sbyte_11 > right.register.sbyte_11 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_12 = left.register.sbyte_12 > right.register.sbyte_12 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_13 = left.register.sbyte_13 > right.register.sbyte_13 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_14 = left.register.sbyte_14 > right.register.sbyte_14 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                    register.sbyte_15 = left.register.sbyte_15 > right.register.sbyte_15 ? ConstantHelper.GetSByteWithAllBitsSet() : (sbyte)0;
                }
                else if (typeof(T) == typeof(ushort))
                {
                    register.uint16_0 = left.register.uint16_0 > right.register.uint16_0 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_1 = left.register.uint16_1 > right.register.uint16_1 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_2 = left.register.uint16_2 > right.register.uint16_2 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_3 = left.register.uint16_3 > right.register.uint16_3 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_4 = left.register.uint16_4 > right.register.uint16_4 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_5 = left.register.uint16_5 > right.register.uint16_5 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_6 = left.register.uint16_6 > right.register.uint16_6 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                    register.uint16_7 = left.register.uint16_7 > right.register.uint16_7 ? ConstantHelper.GetUInt16WithAllBitsSet() : (ushort)0;
                }
                else if (typeof(T) == typeof(short))
                {
                    register.int16_0 = left.register.int16_0 > right.register.int16_0 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_1 = left.register.int16_1 > right.register.int16_1 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_2 = left.register.int16_2 > right.register.int16_2 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_3 = left.register.int16_3 > right.register.int16_3 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_4 = left.register.int16_4 > right.register.int16_4 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_5 = left.register.int16_5 > right.register.int16_5 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_6 = left.register.int16_6 > right.register.int16_6 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                    register.int16_7 = left.register.int16_7 > right.register.int16_7 ? ConstantHelper.GetInt16WithAllBitsSet() : (short)0;
                }
                else if (typeof(T) == typeof(uint))
                {
                    register.uint32_0 = left.register.uint32_0 > right.register.uint32_0 ? ConstantHelper.GetUInt32WithAllBitsSet() : (uint)0;
                    register.uint32_1 = left.register.uint32_1 > right.register.uint32_1 ? ConstantHelper.GetUInt32WithAllBitsSet() : (uint)0;
                    register.uint32_2 = left.register.uint32_2 > right.register.uint32_2 ? ConstantHelper.GetUInt32WithAllBitsSet() : (uint)0;
                    register.uint32_3 = left.register.uint32_3 > right.register.uint32_3 ? ConstantHelper.GetUInt32WithAllBitsSet() : (uint)0;
                }
                else if (typeof(T) == typeof(int))
                {
                    register.int32_0 = left.register.int32_0 > right.register.int32_0 ? ConstantHelper.GetInt32WithAllBitsSet() : (int)0;
                    register.int32_1 = left.register.int32_1 > right.register.int32_1 ? ConstantHelper.GetInt32WithAllBitsSet() : (int)0;
                    register.int32_2 = left.register.int32_2 > right.register.int32_2 ? ConstantHelper.GetInt32WithAllBitsSet() : (int)0;
                    register.int32_3 = left.register.int32_3 > right.register.int32_3 ? ConstantHelper.GetInt32WithAllBitsSet() : (int)0;
                }
                else if (typeof(T) == typeof(ulong))
                {
                    register.uint64_0 = left.register.uint64_0 > right.register.uint64_0 ? ConstantHelper.GetUInt64WithAllBitsSet() : (ulong)0;
                    register.uint64_1 = left.register.uint64_1 > right.register.uint64_1 ? ConstantHelper.GetUInt64WithAllBitsSet() : (ulong)0;
                }
                else if (typeof(T) == typeof(long))
                {
                    register.int64_0 = left.register.int64_0 > right.register.int64_0 ? ConstantHelper.GetInt64WithAllBitsSet() : (long)0;
                    register.int64_1 = left.register.int64_1 > right.register.int64_1 ? ConstantHelper.GetInt64WithAllBitsSet() : (long)0;
                }
                else if (typeof(T) == typeof(float))
                {
                    register.single_0 = left.register.single_0 > right.register.single_0 ? ConstantHelper.GetSingleWithAllBitsSet() : (float)0;
                    register.single_1 = left.register.single_1 > right.register.single_1 ? ConstantHelper.GetSingleWithAllBitsSet() : (float)0;
                    register.single_2 = left.register.single_2 > right.register.single_2 ? ConstantHelper.GetSingleWithAllBitsSet() : (float)0;
                    register.single_3 = left.register.single_3 > right.register.single_3 ? ConstantHelper.GetSingleWithAllBitsSet() : (float)0;
                }
                else if (typeof(T) == typeof(double))
                {
                    register.double_0 = left.register.double_0 > right.register.double_0 ? ConstantHelper.GetDoubleWithAllBitsSet() : (double)0;
                    register.double_1 = left.register.double_1 > right.register.double_1 ? ConstantHelper.GetDoubleWithAllBitsSet() : (double)0;
                }
                return new Vector<T>(ref register);
            }
        }

        [Intrinsic]
        internal static Vector<T> GreaterThanOrEqual(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            if (Vector.IsHardwareAccelerated)
            {
                // This is an intrinsic operation and will not result in a recursive call.
                return GreaterThanOrEqual(left, right);
            }
            else
            {
                return Equals(left, right) | GreaterThan(left, right);
            }
        }

        [Intrinsic]
        internal static Vector<T> LessThanOrEqual(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            if (Vector.IsHardwareAccelerated)
            {
                // This is an intrinsic operation and will not result in a recursive call.
                return LessThanOrEqual(left, right);
            }
            else
            {
                return Equals(left, right) | LessThan(left, right);
            }
        }

        [Intrinsic]
        internal static Vector<T> ConditionalSelect(Vector<T> condition, Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            if (Vector.IsHardwareAccelerated)
            {
                // This is an intrinsic operation and will not result in a recursive call.
                return ConditionalSelect(condition, left, right);
            }
            else
            {
                return (left & condition) | (Vector.AndNot(right, condition));
            }
        }
        #endregion Comparison Methods

        #region Internal Math Methods
        [Intrinsic]
        internal static unsafe Vector<T> Abs(Vector<T> value)
        {
            ThrowIfUnsupportedType();

            if (typeof(T) == typeof(byte))
            {
                return value;
            }
            else if (typeof(T) == typeof(ushort))
            {
                return value;
            }
            else if (typeof(T) == typeof(uint))
            {
                return value;
            }
            else if (typeof(T) == typeof(ulong))
            {
                return value;
            }

            if (Vector.IsHardwareAccelerated)
            {
                // This is an intrinsic operation and will not result in a recursive call.
                return Abs(value);
            }
            else
            {
                if (typeof(T) == typeof(sbyte))
                {
                    value.register.sbyte_0 = (sbyte)(Math.Abs(value.register.sbyte_0));
                    value.register.sbyte_1 = (sbyte)(Math.Abs(value.register.sbyte_1));
                    value.register.sbyte_2 = (sbyte)(Math.Abs(value.register.sbyte_2));
                    value.register.sbyte_3 = (sbyte)(Math.Abs(value.register.sbyte_3));
                    value.register.sbyte_4 = (sbyte)(Math.Abs(value.register.sbyte_4));
                    value.register.sbyte_5 = (sbyte)(Math.Abs(value.register.sbyte_5));
                    value.register.sbyte_6 = (sbyte)(Math.Abs(value.register.sbyte_6));
                    value.register.sbyte_7 = (sbyte)(Math.Abs(value.register.sbyte_7));
                    value.register.sbyte_8 = (sbyte)(Math.Abs(value.register.sbyte_8));
                    value.register.sbyte_9 = (sbyte)(Math.Abs(value.register.sbyte_9));
                    value.register.sbyte_10 = (sbyte)(Math.Abs(value.register.sbyte_10));
                    value.register.sbyte_11 = (sbyte)(Math.Abs(value.register.sbyte_11));
                    value.register.sbyte_12 = (sbyte)(Math.Abs(value.register.sbyte_12));
                    value.register.sbyte_13 = (sbyte)(Math.Abs(value.register.sbyte_13));
                    value.register.sbyte_14 = (sbyte)(Math.Abs(value.register.sbyte_14));
                    value.register.sbyte_15 = (sbyte)(Math.Abs(value.register.sbyte_15));
                    return value;
                }
                else if (typeof(T) == typeof(short))
                {
                    value.register.int16_0 = (short)(Math.Abs(value.register.int16_0));
                    value.register.int16_1 = (short)(Math.Abs(value.register.int16_1));
                    value.register.int16_2 = (short)(Math.Abs(value.register.int16_2));
                    value.register.int16_3 = (short)(Math.Abs(value.register.int16_3));
                    value.register.int16_4 = (short)(Math.Abs(value.register.int16_4));
                    value.register.int16_5 = (short)(Math.Abs(value.register.int16_5));
                    value.register.int16_6 = (short)(Math.Abs(value.register.int16_6));
                    value.register.int16_7 = (short)(Math.Abs(value.register.int16_7));
                    return value;
                }
                else if (typeof(T) == typeof(int))
                {
                    value.register.int32_0 = (int)(Math.Abs(value.register.int32_0));
                    value.register.int32_1 = (int)(Math.Abs(value.register.int32_1));
                    value.register.int32_2 = (int)(Math.Abs(value.register.int32_2));
                    value.register.int32_3 = (int)(Math.Abs(value.register.int32_3));
                    return value;
                }
                else if (typeof(T) == typeof(long))
                {
                    value.register.int64_0 = (long)(Math.Abs(value.register.int64_0));
                    value.register.int64_1 = (long)(Math.Abs(value.register.int64_1));
                    return value;
                }
                else if (typeof(T) == typeof(float))
                {
                    value.register.single_0 = (float)(Math.Abs(value.register.single_0));
                    value.register.single_1 = (float)(Math.Abs(value.register.single_1));
                    value.register.single_2 = (float)(Math.Abs(value.register.single_2));
                    value.register.single_3 = (float)(Math.Abs(value.register.single_3));
                    return value;
                }
                else if (typeof(T) == typeof(double))
                {
                    value.register.double_0 = (double)(Math.Abs(value.register.double_0));
                    value.register.double_1 = (double)(Math.Abs(value.register.double_1));
                    return value;
                }
                else
                {
                    throw new NotSupportedException(SR.Arg_TypeNotSupported);
                }
            }
        }

        [Intrinsic]
        internal static unsafe Vector<T> Min(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            if (Vector.IsHardwareAccelerated)
            {
                // This is an intrinsic operation and will not result in a recursive call.
                return Min(left, right);
            }
            else
            {
                Vector<T> vec = new Vector<T>();
                if (typeof(T) == typeof(byte))
                {
                    vec.register.byte_0 = left.register.byte_0 < right.register.byte_0 ? left.register.byte_0 : right.register.byte_0;
                    vec.register.byte_1 = left.register.byte_1 < right.register.byte_1 ? left.register.byte_1 : right.register.byte_1;
                    vec.register.byte_2 = left.register.byte_2 < right.register.byte_2 ? left.register.byte_2 : right.register.byte_2;
                    vec.register.byte_3 = left.register.byte_3 < right.register.byte_3 ? left.register.byte_3 : right.register.byte_3;
                    vec.register.byte_4 = left.register.byte_4 < right.register.byte_4 ? left.register.byte_4 : right.register.byte_4;
                    vec.register.byte_5 = left.register.byte_5 < right.register.byte_5 ? left.register.byte_5 : right.register.byte_5;
                    vec.register.byte_6 = left.register.byte_6 < right.register.byte_6 ? left.register.byte_6 : right.register.byte_6;
                    vec.register.byte_7 = left.register.byte_7 < right.register.byte_7 ? left.register.byte_7 : right.register.byte_7;
                    vec.register.byte_8 = left.register.byte_8 < right.register.byte_8 ? left.register.byte_8 : right.register.byte_8;
                    vec.register.byte_9 = left.register.byte_9 < right.register.byte_9 ? left.register.byte_9 : right.register.byte_9;
                    vec.register.byte_10 = left.register.byte_10 < right.register.byte_10 ? left.register.byte_10 : right.register.byte_10;
                    vec.register.byte_11 = left.register.byte_11 < right.register.byte_11 ? left.register.byte_11 : right.register.byte_11;
                    vec.register.byte_12 = left.register.byte_12 < right.register.byte_12 ? left.register.byte_12 : right.register.byte_12;
                    vec.register.byte_13 = left.register.byte_13 < right.register.byte_13 ? left.register.byte_13 : right.register.byte_13;
                    vec.register.byte_14 = left.register.byte_14 < right.register.byte_14 ? left.register.byte_14 : right.register.byte_14;
                    vec.register.byte_15 = left.register.byte_15 < right.register.byte_15 ? left.register.byte_15 : right.register.byte_15;
                }
                else if (typeof(T) == typeof(sbyte))
                {
                    vec.register.sbyte_0 = left.register.sbyte_0 < right.register.sbyte_0 ? left.register.sbyte_0 : right.register.sbyte_0;
                    vec.register.sbyte_1 = left.register.sbyte_1 < right.register.sbyte_1 ? left.register.sbyte_1 : right.register.sbyte_1;
                    vec.register.sbyte_2 = left.register.sbyte_2 < right.register.sbyte_2 ? left.register.sbyte_2 : right.register.sbyte_2;
                    vec.register.sbyte_3 = left.register.sbyte_3 < right.register.sbyte_3 ? left.register.sbyte_3 : right.register.sbyte_3;
                    vec.register.sbyte_4 = left.register.sbyte_4 < right.register.sbyte_4 ? left.register.sbyte_4 : right.register.sbyte_4;
                    vec.register.sbyte_5 = left.register.sbyte_5 < right.register.sbyte_5 ? left.register.sbyte_5 : right.register.sbyte_5;
                    vec.register.sbyte_6 = left.register.sbyte_6 < right.register.sbyte_6 ? left.register.sbyte_6 : right.register.sbyte_6;
                    vec.register.sbyte_7 = left.register.sbyte_7 < right.register.sbyte_7 ? left.register.sbyte_7 : right.register.sbyte_7;
                    vec.register.sbyte_8 = left.register.sbyte_8 < right.register.sbyte_8 ? left.register.sbyte_8 : right.register.sbyte_8;
                    vec.register.sbyte_9 = left.register.sbyte_9 < right.register.sbyte_9 ? left.register.sbyte_9 : right.register.sbyte_9;
                    vec.register.sbyte_10 = left.register.sbyte_10 < right.register.sbyte_10 ? left.register.sbyte_10 : right.register.sbyte_10;
                    vec.register.sbyte_11 = left.register.sbyte_11 < right.register.sbyte_11 ? left.register.sbyte_11 : right.register.sbyte_11;
                    vec.register.sbyte_12 = left.register.sbyte_12 < right.register.sbyte_12 ? left.register.sbyte_12 : right.register.sbyte_12;
                    vec.register.sbyte_13 = left.register.sbyte_13 < right.register.sbyte_13 ? left.register.sbyte_13 : right.register.sbyte_13;
                    vec.register.sbyte_14 = left.register.sbyte_14 < right.register.sbyte_14 ? left.register.sbyte_14 : right.register.sbyte_14;
                    vec.register.sbyte_15 = left.register.sbyte_15 < right.register.sbyte_15 ? left.register.sbyte_15 : right.register.sbyte_15;
                }
                else if (typeof(T) == typeof(ushort))
                {
                    vec.register.uint16_0 = left.register.uint16_0 < right.register.uint16_0 ? left.register.uint16_0 : right.register.uint16_0;
                    vec.register.uint16_1 = left.register.uint16_1 < right.register.uint16_1 ? left.register.uint16_1 : right.register.uint16_1;
                    vec.register.uint16_2 = left.register.uint16_2 < right.register.uint16_2 ? left.register.uint16_2 : right.register.uint16_2;
                    vec.register.uint16_3 = left.register.uint16_3 < right.register.uint16_3 ? left.register.uint16_3 : right.register.uint16_3;
                    vec.register.uint16_4 = left.register.uint16_4 < right.register.uint16_4 ? left.register.uint16_4 : right.register.uint16_4;
                    vec.register.uint16_5 = left.register.uint16_5 < right.register.uint16_5 ? left.register.uint16_5 : right.register.uint16_5;
                    vec.register.uint16_6 = left.register.uint16_6 < right.register.uint16_6 ? left.register.uint16_6 : right.register.uint16_6;
                    vec.register.uint16_7 = left.register.uint16_7 < right.register.uint16_7 ? left.register.uint16_7 : right.register.uint16_7;
                }
                else if (typeof(T) == typeof(short))
                {
                    vec.register.int16_0 = left.register.int16_0 < right.register.int16_0 ? left.register.int16_0 : right.register.int16_0;
                    vec.register.int16_1 = left.register.int16_1 < right.register.int16_1 ? left.register.int16_1 : right.register.int16_1;
                    vec.register.int16_2 = left.register.int16_2 < right.register.int16_2 ? left.register.int16_2 : right.register.int16_2;
                    vec.register.int16_3 = left.register.int16_3 < right.register.int16_3 ? left.register.int16_3 : right.register.int16_3;
                    vec.register.int16_4 = left.register.int16_4 < right.register.int16_4 ? left.register.int16_4 : right.register.int16_4;
                    vec.register.int16_5 = left.register.int16_5 < right.register.int16_5 ? left.register.int16_5 : right.register.int16_5;
                    vec.register.int16_6 = left.register.int16_6 < right.register.int16_6 ? left.register.int16_6 : right.register.int16_6;
                    vec.register.int16_7 = left.register.int16_7 < right.register.int16_7 ? left.register.int16_7 : right.register.int16_7;
                }
                else if (typeof(T) == typeof(uint))
                {
                    vec.register.uint32_0 = left.register.uint32_0 < right.register.uint32_0 ? left.register.uint32_0 : right.register.uint32_0;
                    vec.register.uint32_1 = left.register.uint32_1 < right.register.uint32_1 ? left.register.uint32_1 : right.register.uint32_1;
                    vec.register.uint32_2 = left.register.uint32_2 < right.register.uint32_2 ? left.register.uint32_2 : right.register.uint32_2;
                    vec.register.uint32_3 = left.register.uint32_3 < right.register.uint32_3 ? left.register.uint32_3 : right.register.uint32_3;
                }
                else if (typeof(T) == typeof(int))
                {
                    vec.register.int32_0 = left.register.int32_0 < right.register.int32_0 ? left.register.int32_0 : right.register.int32_0;
                    vec.register.int32_1 = left.register.int32_1 < right.register.int32_1 ? left.register.int32_1 : right.register.int32_1;
                    vec.register.int32_2 = left.register.int32_2 < right.register.int32_2 ? left.register.int32_2 : right.register.int32_2;
                    vec.register.int32_3 = left.register.int32_3 < right.register.int32_3 ? left.register.int32_3 : right.register.int32_3;
                }
                else if (typeof(T) == typeof(ulong))
                {
                    vec.register.uint64_0 = left.register.uint64_0 < right.register.uint64_0 ? left.register.uint64_0 : right.register.uint64_0;
                    vec.register.uint64_1 = left.register.uint64_1 < right.register.uint64_1 ? left.register.uint64_1 : right.register.uint64_1;
                }
                else if (typeof(T) == typeof(long))
                {
                    vec.register.int64_0 = left.register.int64_0 < right.register.int64_0 ? left.register.int64_0 : right.register.int64_0;
                    vec.register.int64_1 = left.register.int64_1 < right.register.int64_1 ? left.register.int64_1 : right.register.int64_1;
                }
                else if (typeof(T) == typeof(float))
                {
                    vec.register.single_0 = left.register.single_0 < right.register.single_0 ? left.register.single_0 : right.register.single_0;
                    vec.register.single_1 = left.register.single_1 < right.register.single_1 ? left.register.single_1 : right.register.single_1;
                    vec.register.single_2 = left.register.single_2 < right.register.single_2 ? left.register.single_2 : right.register.single_2;
                    vec.register.single_3 = left.register.single_3 < right.register.single_3 ? left.register.single_3 : right.register.single_3;
                }
                else if (typeof(T) == typeof(double))
                {
                    vec.register.double_0 = left.register.double_0 < right.register.double_0 ? left.register.double_0 : right.register.double_0;
                    vec.register.double_1 = left.register.double_1 < right.register.double_1 ? left.register.double_1 : right.register.double_1;
                }
                return vec;
            }
        }

        [Intrinsic]
        internal static unsafe Vector<T> Max(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            if (Vector.IsHardwareAccelerated)
            {
                // This is an intrinsic operation and will not result in a recursive call.
                return Max(left, right);
            }
            else
            {
                Vector<T> vec = new Vector<T>();
                if (typeof(T) == typeof(byte))
                {
                    vec.register.byte_0 = left.register.byte_0 > right.register.byte_0 ? left.register.byte_0 : right.register.byte_0;
                    vec.register.byte_1 = left.register.byte_1 > right.register.byte_1 ? left.register.byte_1 : right.register.byte_1;
                    vec.register.byte_2 = left.register.byte_2 > right.register.byte_2 ? left.register.byte_2 : right.register.byte_2;
                    vec.register.byte_3 = left.register.byte_3 > right.register.byte_3 ? left.register.byte_3 : right.register.byte_3;
                    vec.register.byte_4 = left.register.byte_4 > right.register.byte_4 ? left.register.byte_4 : right.register.byte_4;
                    vec.register.byte_5 = left.register.byte_5 > right.register.byte_5 ? left.register.byte_5 : right.register.byte_5;
                    vec.register.byte_6 = left.register.byte_6 > right.register.byte_6 ? left.register.byte_6 : right.register.byte_6;
                    vec.register.byte_7 = left.register.byte_7 > right.register.byte_7 ? left.register.byte_7 : right.register.byte_7;
                    vec.register.byte_8 = left.register.byte_8 > right.register.byte_8 ? left.register.byte_8 : right.register.byte_8;
                    vec.register.byte_9 = left.register.byte_9 > right.register.byte_9 ? left.register.byte_9 : right.register.byte_9;
                    vec.register.byte_10 = left.register.byte_10 > right.register.byte_10 ? left.register.byte_10 : right.register.byte_10;
                    vec.register.byte_11 = left.register.byte_11 > right.register.byte_11 ? left.register.byte_11 : right.register.byte_11;
                    vec.register.byte_12 = left.register.byte_12 > right.register.byte_12 ? left.register.byte_12 : right.register.byte_12;
                    vec.register.byte_13 = left.register.byte_13 > right.register.byte_13 ? left.register.byte_13 : right.register.byte_13;
                    vec.register.byte_14 = left.register.byte_14 > right.register.byte_14 ? left.register.byte_14 : right.register.byte_14;
                    vec.register.byte_15 = left.register.byte_15 > right.register.byte_15 ? left.register.byte_15 : right.register.byte_15;
                }
                else if (typeof(T) == typeof(sbyte))
                {
                    vec.register.sbyte_0 = left.register.sbyte_0 > right.register.sbyte_0 ? left.register.sbyte_0 : right.register.sbyte_0;
                    vec.register.sbyte_1 = left.register.sbyte_1 > right.register.sbyte_1 ? left.register.sbyte_1 : right.register.sbyte_1;
                    vec.register.sbyte_2 = left.register.sbyte_2 > right.register.sbyte_2 ? left.register.sbyte_2 : right.register.sbyte_2;
                    vec.register.sbyte_3 = left.register.sbyte_3 > right.register.sbyte_3 ? left.register.sbyte_3 : right.register.sbyte_3;
                    vec.register.sbyte_4 = left.register.sbyte_4 > right.register.sbyte_4 ? left.register.sbyte_4 : right.register.sbyte_4;
                    vec.register.sbyte_5 = left.register.sbyte_5 > right.register.sbyte_5 ? left.register.sbyte_5 : right.register.sbyte_5;
                    vec.register.sbyte_6 = left.register.sbyte_6 > right.register.sbyte_6 ? left.register.sbyte_6 : right.register.sbyte_6;
                    vec.register.sbyte_7 = left.register.sbyte_7 > right.register.sbyte_7 ? left.register.sbyte_7 : right.register.sbyte_7;
                    vec.register.sbyte_8 = left.register.sbyte_8 > right.register.sbyte_8 ? left.register.sbyte_8 : right.register.sbyte_8;
                    vec.register.sbyte_9 = left.register.sbyte_9 > right.register.sbyte_9 ? left.register.sbyte_9 : right.register.sbyte_9;
                    vec.register.sbyte_10 = left.register.sbyte_10 > right.register.sbyte_10 ? left.register.sbyte_10 : right.register.sbyte_10;
                    vec.register.sbyte_11 = left.register.sbyte_11 > right.register.sbyte_11 ? left.register.sbyte_11 : right.register.sbyte_11;
                    vec.register.sbyte_12 = left.register.sbyte_12 > right.register.sbyte_12 ? left.register.sbyte_12 : right.register.sbyte_12;
                    vec.register.sbyte_13 = left.register.sbyte_13 > right.register.sbyte_13 ? left.register.sbyte_13 : right.register.sbyte_13;
                    vec.register.sbyte_14 = left.register.sbyte_14 > right.register.sbyte_14 ? left.register.sbyte_14 : right.register.sbyte_14;
                    vec.register.sbyte_15 = left.register.sbyte_15 > right.register.sbyte_15 ? left.register.sbyte_15 : right.register.sbyte_15;
                }
                else if (typeof(T) == typeof(ushort))
                {
                    vec.register.uint16_0 = left.register.uint16_0 > right.register.uint16_0 ? left.register.uint16_0 : right.register.uint16_0;
                    vec.register.uint16_1 = left.register.uint16_1 > right.register.uint16_1 ? left.register.uint16_1 : right.register.uint16_1;
                    vec.register.uint16_2 = left.register.uint16_2 > right.register.uint16_2 ? left.register.uint16_2 : right.register.uint16_2;
                    vec.register.uint16_3 = left.register.uint16_3 > right.register.uint16_3 ? left.register.uint16_3 : right.register.uint16_3;
                    vec.register.uint16_4 = left.register.uint16_4 > right.register.uint16_4 ? left.register.uint16_4 : right.register.uint16_4;
                    vec.register.uint16_5 = left.register.uint16_5 > right.register.uint16_5 ? left.register.uint16_5 : right.register.uint16_5;
                    vec.register.uint16_6 = left.register.uint16_6 > right.register.uint16_6 ? left.register.uint16_6 : right.register.uint16_6;
                    vec.register.uint16_7 = left.register.uint16_7 > right.register.uint16_7 ? left.register.uint16_7 : right.register.uint16_7;
                }
                else if (typeof(T) == typeof(short))
                {
                    vec.register.int16_0 = left.register.int16_0 > right.register.int16_0 ? left.register.int16_0 : right.register.int16_0;
                    vec.register.int16_1 = left.register.int16_1 > right.register.int16_1 ? left.register.int16_1 : right.register.int16_1;
                    vec.register.int16_2 = left.register.int16_2 > right.register.int16_2 ? left.register.int16_2 : right.register.int16_2;
                    vec.register.int16_3 = left.register.int16_3 > right.register.int16_3 ? left.register.int16_3 : right.register.int16_3;
                    vec.register.int16_4 = left.register.int16_4 > right.register.int16_4 ? left.register.int16_4 : right.register.int16_4;
                    vec.register.int16_5 = left.register.int16_5 > right.register.int16_5 ? left.register.int16_5 : right.register.int16_5;
                    vec.register.int16_6 = left.register.int16_6 > right.register.int16_6 ? left.register.int16_6 : right.register.int16_6;
                    vec.register.int16_7 = left.register.int16_7 > right.register.int16_7 ? left.register.int16_7 : right.register.int16_7;
                }
                else if (typeof(T) == typeof(uint))
                {
                    vec.register.uint32_0 = left.register.uint32_0 > right.register.uint32_0 ? left.register.uint32_0 : right.register.uint32_0;
                    vec.register.uint32_1 = left.register.uint32_1 > right.register.uint32_1 ? left.register.uint32_1 : right.register.uint32_1;
                    vec.register.uint32_2 = left.register.uint32_2 > right.register.uint32_2 ? left.register.uint32_2 : right.register.uint32_2;
                    vec.register.uint32_3 = left.register.uint32_3 > right.register.uint32_3 ? left.register.uint32_3 : right.register.uint32_3;
                }
                else if (typeof(T) == typeof(int))
                {
                    vec.register.int32_0 = left.register.int32_0 > right.register.int32_0 ? left.register.int32_0 : right.register.int32_0;
                    vec.register.int32_1 = left.register.int32_1 > right.register.int32_1 ? left.register.int32_1 : right.register.int32_1;
                    vec.register.int32_2 = left.register.int32_2 > right.register.int32_2 ? left.register.int32_2 : right.register.int32_2;
                    vec.register.int32_3 = left.register.int32_3 > right.register.int32_3 ? left.register.int32_3 : right.register.int32_3;
                }
                else if (typeof(T) == typeof(ulong))
                {
                    vec.register.uint64_0 = left.register.uint64_0 > right.register.uint64_0 ? left.register.uint64_0 : right.register.uint64_0;
                    vec.register.uint64_1 = left.register.uint64_1 > right.register.uint64_1 ? left.register.uint64_1 : right.register.uint64_1;
                }
                else if (typeof(T) == typeof(long))
                {
                    vec.register.int64_0 = left.register.int64_0 > right.register.int64_0 ? left.register.int64_0 : right.register.int64_0;
                    vec.register.int64_1 = left.register.int64_1 > right.register.int64_1 ? left.register.int64_1 : right.register.int64_1;
                }
                else if (typeof(T) == typeof(float))
                {
                    vec.register.single_0 = left.register.single_0 > right.register.single_0 ? left.register.single_0 : right.register.single_0;
                    vec.register.single_1 = left.register.single_1 > right.register.single_1 ? left.register.single_1 : right.register.single_1;
                    vec.register.single_2 = left.register.single_2 > right.register.single_2 ? left.register.single_2 : right.register.single_2;
                    vec.register.single_3 = left.register.single_3 > right.register.single_3 ? left.register.single_3 : right.register.single_3;
                }
                else if (typeof(T) == typeof(double))
                {
                    vec.register.double_0 = left.register.double_0 > right.register.double_0 ? left.register.double_0 : right.register.double_0;
                    vec.register.double_1 = left.register.double_1 > right.register.double_1 ? left.register.double_1 : right.register.double_1;
                }
                return vec;
            }
        }

        [Intrinsic]
        internal static T Dot(Vector<T> left, Vector<T> right)
        {
            ThrowIfUnsupportedType();

            if (Vector.IsHardwareAccelerated)
            {
                // This is an intrinsic operation and will not result in a recursive call.
                return Dot(left, right);
            }
            else
            {
                if (typeof(T) == typeof(byte))
                {
                    byte product = 0;
                    product += (byte)(left.register.byte_0 * right.register.byte_0);
                    product += (byte)(left.register.byte_1 * right.register.byte_1);
                    product += (byte)(left.register.byte_2 * right.register.byte_2);
                    product += (byte)(left.register.byte_3 * right.register.byte_3);
                    product += (byte)(left.register.byte_4 * right.register.byte_4);
                    product += (byte)(left.register.byte_5 * right.register.byte_5);
                    product += (byte)(left.register.byte_6 * right.register.byte_6);
                    product += (byte)(left.register.byte_7 * right.register.byte_7);
                    product += (byte)(left.register.byte_8 * right.register.byte_8);
                    product += (byte)(left.register.byte_9 * right.register.byte_9);
                    product += (byte)(left.register.byte_10 * right.register.byte_10);
                    product += (byte)(left.register.byte_11 * right.register.byte_11);
                    product += (byte)(left.register.byte_12 * right.register.byte_12);
                    product += (byte)(left.register.byte_13 * right.register.byte_13);
                    product += (byte)(left.register.byte_14 * right.register.byte_14);
                    product += (byte)(left.register.byte_15 * right.register.byte_15);
                    return (T)(object)product;
                }
                else if (typeof(T) == typeof(sbyte))
                {
                    sbyte product = 0;
                    product += (sbyte)(left.register.sbyte_0 * right.register.sbyte_0);
                    product += (sbyte)(left.register.sbyte_1 * right.register.sbyte_1);
                    product += (sbyte)(left.register.sbyte_2 * right.register.sbyte_2);
                    product += (sbyte)(left.register.sbyte_3 * right.register.sbyte_3);
                    product += (sbyte)(left.register.sbyte_4 * right.register.sbyte_4);
                    product += (sbyte)(left.register.sbyte_5 * right.register.sbyte_5);
                    product += (sbyte)(left.register.sbyte_6 * right.register.sbyte_6);
                    product += (sbyte)(left.register.sbyte_7 * right.register.sbyte_7);
                    product += (sbyte)(left.register.sbyte_8 * right.register.sbyte_8);
                    product += (sbyte)(left.register.sbyte_9 * right.register.sbyte_9);
                    product += (sbyte)(left.register.sbyte_10 * right.register.sbyte_10);
                    product += (sbyte)(left.register.sbyte_11 * right.register.sbyte_11);
                    product += (sbyte)(left.register.sbyte_12 * right.register.sbyte_12);
                    product += (sbyte)(left.register.sbyte_13 * right.register.sbyte_13);
                    product += (sbyte)(left.register.sbyte_14 * right.register.sbyte_14);
                    product += (sbyte)(left.register.sbyte_15 * right.register.sbyte_15);
                    return (T)(object)product;
                }
                else if (typeof(T) == typeof(ushort))
                {
                    ushort product = 0;
                    product += (ushort)(left.register.uint16_0 * right.register.uint16_0);
                    product += (ushort)(left.register.uint16_1 * right.register.uint16_1);
                    product += (ushort)(left.register.uint16_2 * right.register.uint16_2);
                    product += (ushort)(left.register.uint16_3 * right.register.uint16_3);
                    product += (ushort)(left.register.uint16_4 * right.register.uint16_4);
                    product += (ushort)(left.register.uint16_5 * right.register.uint16_5);
                    product += (ushort)(left.register.uint16_6 * right.register.uint16_6);
                    product += (ushort)(left.register.uint16_7 * right.register.uint16_7);
                    return (T)(object)product;
                }
                else if (typeof(T) == typeof(short))
                {
                    short product = 0;
                    product += (short)(left.register.int16_0 * right.register.int16_0);
                    product += (short)(left.register.int16_1 * right.register.int16_1);
                    product += (short)(left.register.int16_2 * right.register.int16_2);
                    product += (short)(left.register.int16_3 * right.register.int16_3);
                    product += (short)(left.register.int16_4 * right.register.int16_4);
                    product += (short)(left.register.int16_5 * right.register.int16_5);
                    product += (short)(left.register.int16_6 * right.register.int16_6);
                    product += (short)(left.register.int16_7 * right.register.int16_7);
                    return (T)(object)product;
                }
                else if (typeof(T) == typeof(uint))
                {
                    uint product = 0;
                    product += (uint)(left.register.uint32_0 * right.register.uint32_0);
                    product += (uint)(left.register.uint32_1 * right.register.uint32_1);
                    product += (uint)(left.register.uint32_2 * right.register.uint32_2);
                    product += (uint)(left.register.uint32_3 * right.register.uint32_3);
                    return (T)(object)product;
                }
                else if (typeof(T) == typeof(int))
                {
                    int product = 0;
                    product += (int)(left.register.int32_0 * right.register.int32_0);
                    product += (int)(left.register.int32_1 * right.register.int32_1);
                    product += (int)(left.register.int32_2 * right.register.int32_2);
                    product += (int)(left.register.int32_3 * right.register.int32_3);
                    return (T)(object)product;
                }
                else if (typeof(T) == typeof(ulong))
                {
                    ulong product = 0;
                    product += (ulong)(left.register.uint64_0 * right.register.uint64_0);
                    product += (ulong)(left.register.uint64_1 * right.register.uint64_1);
                    return (T)(object)product;
                }
                else if (typeof(T) == typeof(long))
                {
                    long product = 0;
                    product += (long)(left.register.int64_0 * right.register.int64_0);
                    product += (long)(left.register.int64_1 * right.register.int64_1);
                    return (T)(object)product;
                }
                else if (typeof(T) == typeof(float))
                {
                    float product = 0;
                    product += (float)(left.register.single_0 * right.register.single_0);
                    product += (float)(left.register.single_1 * right.register.single_1);
                    product += (float)(left.register.single_2 * right.register.single_2);
                    product += (float)(left.register.single_3 * right.register.single_3);
                    return (T)(object)product;
                }
                else if (typeof(T) == typeof(double))
                {
                    double product = 0;
                    product += (double)(left.register.double_0 * right.register.double_0);
                    product += (double)(left.register.double_1 * right.register.double_1);
                    return (T)(object)product;
                }
                else
                {
                    throw new NotSupportedException(SR.Arg_TypeNotSupported);
                }
            }
        }

        [Intrinsic]
        internal static unsafe Vector<T> SquareRoot(Vector<T> value)
        {
            ThrowIfUnsupportedType();

            if (Vector.IsHardwareAccelerated)
            {
                // This is an intrinsic operation and will not result in a recursive call.
                return SquareRoot(value);
            }
            else
            {
                if (typeof(T) == typeof(byte))
                {
                    value.register.byte_0 = (byte)Math.Sqrt(value.register.byte_0);
                    value.register.byte_1 = (byte)Math.Sqrt(value.register.byte_1);
                    value.register.byte_2 = (byte)Math.Sqrt(value.register.byte_2);
                    value.register.byte_3 = (byte)Math.Sqrt(value.register.byte_3);
                    value.register.byte_4 = (byte)Math.Sqrt(value.register.byte_4);
                    value.register.byte_5 = (byte)Math.Sqrt(value.register.byte_5);
                    value.register.byte_6 = (byte)Math.Sqrt(value.register.byte_6);
                    value.register.byte_7 = (byte)Math.Sqrt(value.register.byte_7);
                    value.register.byte_8 = (byte)Math.Sqrt(value.register.byte_8);
                    value.register.byte_9 = (byte)Math.Sqrt(value.register.byte_9);
                    value.register.byte_10 = (byte)Math.Sqrt(value.register.byte_10);
                    value.register.byte_11 = (byte)Math.Sqrt(value.register.byte_11);
                    value.register.byte_12 = (byte)Math.Sqrt(value.register.byte_12);
                    value.register.byte_13 = (byte)Math.Sqrt(value.register.byte_13);
                    value.register.byte_14 = (byte)Math.Sqrt(value.register.byte_14);
                    value.register.byte_15 = (byte)Math.Sqrt(value.register.byte_15);
                }
                else if (typeof(T) == typeof(sbyte))
                {
                    value.register.sbyte_0 = (sbyte)Math.Sqrt(value.register.sbyte_0);
                    value.register.sbyte_1 = (sbyte)Math.Sqrt(value.register.sbyte_1);
                    value.register.sbyte_2 = (sbyte)Math.Sqrt(value.register.sbyte_2);
                    value.register.sbyte_3 = (sbyte)Math.Sqrt(value.register.sbyte_3);
                    value.register.sbyte_4 = (sbyte)Math.Sqrt(value.register.sbyte_4);
                    value.register.sbyte_5 = (sbyte)Math.Sqrt(value.register.sbyte_5);
                    value.register.sbyte_6 = (sbyte)Math.Sqrt(value.register.sbyte_6);
                    value.register.sbyte_7 = (sbyte)Math.Sqrt(value.register.sbyte_7);
                    value.register.sbyte_8 = (sbyte)Math.Sqrt(value.register.sbyte_8);
                    value.register.sbyte_9 = (sbyte)Math.Sqrt(value.register.sbyte_9);
                    value.register.sbyte_10 = (sbyte)Math.Sqrt(value.register.sbyte_10);
                    value.register.sbyte_11 = (sbyte)Math.Sqrt(value.register.sbyte_11);
                    value.register.sbyte_12 = (sbyte)Math.Sqrt(value.register.sbyte_12);
                    value.register.sbyte_13 = (sbyte)Math.Sqrt(value.register.sbyte_13);
                    value.register.sbyte_14 = (sbyte)Math.Sqrt(value.register.sbyte_14);
                    value.register.sbyte_15 = (sbyte)Math.Sqrt(value.register.sbyte_15);
                }
                else if (typeof(T) == typeof(ushort))
                {
                    value.register.uint16_0 = (ushort)Math.Sqrt(value.register.uint16_0);
                    value.register.uint16_1 = (ushort)Math.Sqrt(value.register.uint16_1);
                    value.register.uint16_2 = (ushort)Math.Sqrt(value.register.uint16_2);
                    value.register.uint16_3 = (ushort)Math.Sqrt(value.register.uint16_3);
                    value.register.uint16_4 = (ushort)Math.Sqrt(value.register.uint16_4);
                    value.register.uint16_5 = (ushort)Math.Sqrt(value.register.uint16_5);
                    value.register.uint16_6 = (ushort)Math.Sqrt(value.register.uint16_6);
                    value.register.uint16_7 = (ushort)Math.Sqrt(value.register.uint16_7);
                }
                else if (typeof(T) == typeof(short))
                {
                    value.register.int16_0 = (short)Math.Sqrt(value.register.int16_0);
                    value.register.int16_1 = (short)Math.Sqrt(value.register.int16_1);
                    value.register.int16_2 = (short)Math.Sqrt(value.register.int16_2);
                    value.register.int16_3 = (short)Math.Sqrt(value.register.int16_3);
                    value.register.int16_4 = (short)Math.Sqrt(value.register.int16_4);
                    value.register.int16_5 = (short)Math.Sqrt(value.register.int16_5);
                    value.register.int16_6 = (short)Math.Sqrt(value.register.int16_6);
                    value.register.int16_7 = (short)Math.Sqrt(value.register.int16_7);
                }
                else if (typeof(T) == typeof(uint))
                {
                    value.register.uint32_0 = (uint)Math.Sqrt(value.register.uint32_0);
                    value.register.uint32_1 = (uint)Math.Sqrt(value.register.uint32_1);
                    value.register.uint32_2 = (uint)Math.Sqrt(value.register.uint32_2);
                    value.register.uint32_3 = (uint)Math.Sqrt(value.register.uint32_3);
                }
                else if (typeof(T) == typeof(int))
                {
                    value.register.int32_0 = (int)Math.Sqrt(value.register.int32_0);
                    value.register.int32_1 = (int)Math.Sqrt(value.register.int32_1);
                    value.register.int32_2 = (int)Math.Sqrt(value.register.int32_2);
                    value.register.int32_3 = (int)Math.Sqrt(value.register.int32_3);
                }
                else if (typeof(T) == typeof(ulong))
                {
                    value.register.uint64_0 = (ulong)Math.Sqrt(value.register.uint64_0);
                    value.register.uint64_1 = (ulong)Math.Sqrt(value.register.uint64_1);
                }
                else if (typeof(T) == typeof(long))
                {
                    value.register.int64_0 = (long)Math.Sqrt(value.register.int64_0);
                    value.register.int64_1 = (long)Math.Sqrt(value.register.int64_1);
                }
                else if (typeof(T) == typeof(float))
                {
                    value.register.single_0 = (float)Math.Sqrt(value.register.single_0);
                    value.register.single_1 = (float)Math.Sqrt(value.register.single_1);
                    value.register.single_2 = (float)Math.Sqrt(value.register.single_2);
                    value.register.single_3 = (float)Math.Sqrt(value.register.single_3);
                }
                else if (typeof(T) == typeof(double))
                {
                    value.register.double_0 = (double)Math.Sqrt(value.register.double_0);
                    value.register.double_1 = (double)Math.Sqrt(value.register.double_1);
                }
                return value;
            }
        }
        #endregion Internal Math Methods

        #region Helper Methods
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private static T GetOneValue()
        {
            if (typeof(T) == typeof(byte))
            {
                byte value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(sbyte))
            {
                sbyte value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(ushort))
            {
                ushort value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(short))
            {
                short value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(uint))
            {
                uint value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(int))
            {
                int value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(ulong))
            {
                ulong value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(long))
            {
                long value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(float))
            {
                float value = 1;
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(double))
            {
                double value = 1;
                return (T)(object)value;
            }
            else
            {
                throw new NotSupportedException(SR.Arg_TypeNotSupported);
            }
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private static T GetAllBitsSetValue()
        {
            if (typeof(T) == typeof(byte))
            {
                return (T)(object)ConstantHelper.GetByteWithAllBitsSet();
            }
            else if (typeof(T) == typeof(sbyte))
            {
                return (T)(object)ConstantHelper.GetSByteWithAllBitsSet();
            }
            else if (typeof(T) == typeof(ushort))
            {
                return (T)(object)ConstantHelper.GetUInt16WithAllBitsSet();
            }
            else if (typeof(T) == typeof(short))
            {
                return (T)(object)ConstantHelper.GetInt16WithAllBitsSet();
            }
            else if (typeof(T) == typeof(uint))
            {
                return (T)(object)ConstantHelper.GetUInt32WithAllBitsSet();
            }
            else if (typeof(T) == typeof(int))
            {
                return (T)(object)ConstantHelper.GetInt32WithAllBitsSet();
            }
            else if (typeof(T) == typeof(ulong))
            {
                return (T)(object)ConstantHelper.GetUInt64WithAllBitsSet();
            }
            else if (typeof(T) == typeof(long))
            {
                return (T)(object)ConstantHelper.GetInt64WithAllBitsSet();
            }
            else if (typeof(T) == typeof(float))
            {
                return (T)(object)ConstantHelper.GetSingleWithAllBitsSet();
            }
            else if (typeof(T) == typeof(double))
            {
                return (T)(object)ConstantHelper.GetDoubleWithAllBitsSet();
            }
            else
            {
                throw new NotSupportedException(SR.Arg_TypeNotSupported);
            }
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        [StackTraceHiddenAttribute]
        private static void ThrowIfUnsupportedType()
        {
            // These conditions are evaluated and collapsed during JIT, which means that
            // ultimately this method ends up as empty (and is optimized away completely)
            // or becomes a single unconditional throw statement.

            if ((typeof(T) != typeof(byte))
                && (typeof(T) != typeof(sbyte))
                && (typeof(T) != typeof(ushort))
                && (typeof(T) != typeof(short))
                && (typeof(T) != typeof(uint))
                && (typeof(T) != typeof(int))
                && (typeof(T) != typeof(ulong))
                && (typeof(T) != typeof(long))
                && (typeof(T) != typeof(float))
                && (typeof(T) != typeof(double)))
            {
                throw new NotSupportedException(SR.Arg_TypeNotSupported);
            }
        }
        #endregion
    }

    [Intrinsic]
    public static partial class Vector
    {
        #region Widen/Narrow
        /// <summary>
        /// Widens a Vector{Byte} into two Vector{UInt16}'s.
        /// <param name="source">The source vector whose elements are widened into the outputs.</param>
        /// <param name="low">The first output vector, whose elements will contain the widened elements from lower indices in the source vector.</param>
        /// <param name="high">The second output vector, whose elements will contain the widened elements from higher indices in the source vector.</param>
        /// </summary>
        [CLSCompliant(false)]
        [Intrinsic]
        public static unsafe void Widen(Vector<byte> source, out Vector<ushort> low, out Vector<ushort> high)
        {
            int elements = Vector<byte>.Count;
            ushort* lowPtr = stackalloc ushort[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                lowPtr[i] = (ushort)source[i];
            }
            ushort* highPtr = stackalloc ushort[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                highPtr[i] = (ushort)source[i + (elements / 2)];
            }

            low = new Vector<ushort>(lowPtr);
            high = new Vector<ushort>(highPtr);
        }

        /// <summary>
        /// Widens a Vector{UInt16} into two Vector{UInt32}'s.
        /// <param name="source">The source vector whose elements are widened into the outputs.</param>
        /// <param name="low">The first output vector, whose elements will contain the widened elements from lower indices in the source vector.</param>
        /// <param name="high">The second output vector, whose elements will contain the widened elements from higher indices in the source vector.</param>
        /// </summary>
        [CLSCompliant(false)]
        [Intrinsic]
        public static unsafe void Widen(Vector<ushort> source, out Vector<uint> low, out Vector<uint> high)
        {
            int elements = Vector<ushort>.Count;
            uint* lowPtr = stackalloc uint[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                lowPtr[i] = (uint)source[i];
            }
            uint* highPtr = stackalloc uint[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                highPtr[i] = (uint)source[i + (elements / 2)];
            }

            low = new Vector<uint>(lowPtr);
            high = new Vector<uint>(highPtr);
        }

        /// <summary>
        /// Widens a Vector{UInt32} into two Vector{UInt64}'s.
        /// <param name="source">The source vector whose elements are widened into the outputs.</param>
        /// <param name="low">The first output vector, whose elements will contain the widened elements from lower indices in the source vector.</param>
        /// <param name="high">The second output vector, whose elements will contain the widened elements from higher indices in the source vector.</param>
        /// </summary>
        [CLSCompliant(false)]
        [Intrinsic]
        public static unsafe void Widen(Vector<uint> source, out Vector<ulong> low, out Vector<ulong> high)
        {
            int elements = Vector<uint>.Count;
            ulong* lowPtr = stackalloc ulong[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                lowPtr[i] = (ulong)source[i];
            }
            ulong* highPtr = stackalloc ulong[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                highPtr[i] = (ulong)source[i + (elements / 2)];
            }

            low = new Vector<ulong>(lowPtr);
            high = new Vector<ulong>(highPtr);
        }

        /// <summary>
        /// Widens a Vector{SByte} into two Vector{Int16}'s.
        /// <param name="source">The source vector whose elements are widened into the outputs.</param>
        /// <param name="low">The first output vector, whose elements will contain the widened elements from lower indices in the source vector.</param>
        /// <param name="high">The second output vector, whose elements will contain the widened elements from higher indices in the source vector.</param>
        /// </summary>
        [CLSCompliant(false)]
        [Intrinsic]
        public static unsafe void Widen(Vector<sbyte> source, out Vector<short> low, out Vector<short> high)
        {
            int elements = Vector<sbyte>.Count;
            short* lowPtr = stackalloc short[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                lowPtr[i] = (short)source[i];
            }
            short* highPtr = stackalloc short[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                highPtr[i] = (short)source[i + (elements / 2)];
            }

            low = new Vector<short>(lowPtr);
            high = new Vector<short>(highPtr);
        }

        /// <summary>
        /// Widens a Vector{Int16} into two Vector{Int32}'s.
        /// <param name="source">The source vector whose elements are widened into the outputs.</param>
        /// <param name="low">The first output vector, whose elements will contain the widened elements from lower indices in the source vector.</param>
        /// <param name="high">The second output vector, whose elements will contain the widened elements from higher indices in the source vector.</param>
        /// </summary>
        [Intrinsic]
        public static unsafe void Widen(Vector<short> source, out Vector<int> low, out Vector<int> high)
        {
            int elements = Vector<short>.Count;
            int* lowPtr = stackalloc int[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                lowPtr[i] = (int)source[i];
            }
            int* highPtr = stackalloc int[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                highPtr[i] = (int)source[i + (elements / 2)];
            }

            low = new Vector<int>(lowPtr);
            high = new Vector<int>(highPtr);
        }

        /// <summary>
        /// Widens a Vector{Int32} into two Vector{Int64}'s.
        /// <param name="source">The source vector whose elements are widened into the outputs.</param>
        /// <param name="low">The first output vector, whose elements will contain the widened elements from lower indices in the source vector.</param>
        /// <param name="high">The second output vector, whose elements will contain the widened elements from higher indices in the source vector.</param>
        /// </summary>
        [Intrinsic]
        public static unsafe void Widen(Vector<int> source, out Vector<long> low, out Vector<long> high)
        {
            int elements = Vector<int>.Count;
            long* lowPtr = stackalloc long[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                lowPtr[i] = (long)source[i];
            }
            long* highPtr = stackalloc long[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                highPtr[i] = (long)source[i + (elements / 2)];
            }

            low = new Vector<long>(lowPtr);
            high = new Vector<long>(highPtr);
        }

        /// <summary>
        /// Widens a Vector{Single} into two Vector{Double}'s.
        /// <param name="source">The source vector whose elements are widened into the outputs.</param>
        /// <param name="low">The first output vector, whose elements will contain the widened elements from lower indices in the source vector.</param>
        /// <param name="high">The second output vector, whose elements will contain the widened elements from higher indices in the source vector.</param>
        /// </summary>
        [Intrinsic]
        public static unsafe void Widen(Vector<float> source, out Vector<double> low, out Vector<double> high)
        {
            int elements = Vector<float>.Count;
            double* lowPtr = stackalloc double[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                lowPtr[i] = (double)source[i];
            }
            double* highPtr = stackalloc double[elements / 2];
            for (int i = 0; i < elements / 2; i++)
            {
                highPtr[i] = (double)source[i + (elements / 2)];
            }

            low = new Vector<double>(lowPtr);
            high = new Vector<double>(highPtr);
        }

        /// <summary>
        /// Narrows two Vector{UInt16}'s into one Vector{Byte}.
        /// <param name="low">The first source vector, whose elements become the lower-index elements of the return value.</param>
        /// <param name="high">The second source vector, whose elements become the higher-index elements of the return value.</param>
        /// <returns>A Vector{Byte} containing elements narrowed from the source vectors.</returns>
        /// </summary>
        [CLSCompliant(false)]
        [Intrinsic]
        public static unsafe Vector<byte> Narrow(Vector<ushort> low, Vector<ushort> high)
        {
            unchecked
            {
                int elements = Vector<byte>.Count;
                byte* retPtr = stackalloc byte[elements];
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i] = (byte)low[i];
                }
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i + (elements / 2)] = (byte)high[i];
                }

                return new Vector<byte>(retPtr);
            }
        }

        /// <summary>
        /// Narrows two Vector{UInt32}'s into one Vector{UInt16}.
        /// <param name="low">The first source vector, whose elements become the lower-index elements of the return value.</param>
        /// <param name="high">The second source vector, whose elements become the higher-index elements of the return value.</param>
        /// <returns>A Vector{UInt16} containing elements narrowed from the source vectors.</returns>
        /// </summary>
        [CLSCompliant(false)]
        [Intrinsic]
        public static unsafe Vector<ushort> Narrow(Vector<uint> low, Vector<uint> high)
        {
            unchecked
            {
                int elements = Vector<ushort>.Count;
                ushort* retPtr = stackalloc ushort[elements];
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i] = (ushort)low[i];
                }
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i + (elements / 2)] = (ushort)high[i];
                }

                return new Vector<ushort>(retPtr);
            }
        }

        /// <summary>
        /// Narrows two Vector{UInt64}'s into one Vector{UInt32}.
        /// <param name="low">The first source vector, whose elements become the lower-index elements of the return value.</param>
        /// <param name="high">The second source vector, whose elements become the higher-index elements of the return value.</param>
        /// <returns>A Vector{UInt32} containing elements narrowed from the source vectors.</returns>
        /// </summary>
        [CLSCompliant(false)]
        [Intrinsic]
        public static unsafe Vector<uint> Narrow(Vector<ulong> low, Vector<ulong> high)
        {
            unchecked
            {
                int elements = Vector<uint>.Count;
                uint* retPtr = stackalloc uint[elements];
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i] = (uint)low[i];
                }
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i + (elements / 2)] = (uint)high[i];
                }

                return new Vector<uint>(retPtr);
            }
        }

        /// <summary>
        /// Narrows two Vector{Int16}'s into one Vector{SByte}.
        /// <param name="low">The first source vector, whose elements become the lower-index elements of the return value.</param>
        /// <param name="high">The second source vector, whose elements become the higher-index elements of the return value.</param>
        /// <returns>A Vector{SByte} containing elements narrowed from the source vectors.</returns>
        /// </summary>
        [CLSCompliant(false)]
        [Intrinsic]
        public static unsafe Vector<sbyte> Narrow(Vector<short> low, Vector<short> high)
        {
            unchecked
            {
                int elements = Vector<sbyte>.Count;
                sbyte* retPtr = stackalloc sbyte[elements];
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i] = (sbyte)low[i];
                }
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i + (elements / 2)] = (sbyte)high[i];
                }

                return new Vector<sbyte>(retPtr);
            }
        }

        /// <summary>
        /// Narrows two Vector{Int32}'s into one Vector{Int16}.
        /// <param name="low">The first source vector, whose elements become the lower-index elements of the return value.</param>
        /// <param name="high">The second source vector, whose elements become the higher-index elements of the return value.</param>
        /// <returns>A Vector{Int16} containing elements narrowed from the source vectors.</returns>
        /// </summary>
        [Intrinsic]
        public static unsafe Vector<short> Narrow(Vector<int> low, Vector<int> high)
        {
            unchecked
            {
                int elements = Vector<short>.Count;
                short* retPtr = stackalloc short[elements];
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i] = (short)low[i];
                }
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i + (elements / 2)] = (short)high[i];
                }

                return new Vector<short>(retPtr);
            }
        }

        /// <summary>
        /// Narrows two Vector{Int64}'s into one Vector{Int32}.
        /// <param name="low">The first source vector, whose elements become the lower-index elements of the return value.</param>
        /// <param name="high">The second source vector, whose elements become the higher-index elements of the return value.</param>
        /// <returns>A Vector{Int32} containing elements narrowed from the source vectors.</returns>
        /// </summary>
        [Intrinsic]
        public static unsafe Vector<int> Narrow(Vector<long> low, Vector<long> high)
        {
            unchecked
            {
                int elements = Vector<int>.Count;
                int* retPtr = stackalloc int[elements];
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i] = (int)low[i];
                }
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i + (elements / 2)] = (int)high[i];
                }

                return new Vector<int>(retPtr);
            }
        }

        /// <summary>
        /// Narrows two Vector{Double}'s into one Vector{Single}.
        /// <param name="low">The first source vector, whose elements become the lower-index elements of the return value.</param>
        /// <param name="high">The second source vector, whose elements become the higher-index elements of the return value.</param>
        /// <returns>A Vector{Single} containing elements narrowed from the source vectors.</returns>
        /// </summary>
        [Intrinsic]
        public static unsafe Vector<float> Narrow(Vector<double> low, Vector<double> high)
        {
            unchecked
            {
                int elements = Vector<float>.Count;
                float* retPtr = stackalloc float[elements];
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i] = (float)low[i];
                }
                for (int i = 0; i < elements / 2; i++)
                {
                    retPtr[i + (elements / 2)] = (float)high[i];
                }

                return new Vector<float>(retPtr);
            }
        }

        #endregion Widen/Narrow

        #region Same-Size Conversion
        /// <summary>
        /// Converts a Vector{Int32} to a Vector{Single}.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The converted vector.</returns>
        [Intrinsic]
        public static unsafe Vector<float> ConvertToSingle(Vector<int> value)
        {
            unchecked
            {
                int elements = Vector<float>.Count;
                float* retPtr = stackalloc float[elements];
                for (int i = 0; i < elements; i++)
                {
                    retPtr[i] = (float)value[i];
                }

                return new Vector<float>(retPtr);
            }
        }

        /// <summary>
        /// Converts a Vector{UInt32} to a Vector{Single}.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The converted vector.</returns>
        [CLSCompliant(false)]
        [Intrinsic]
        public static unsafe Vector<float> ConvertToSingle(Vector<uint> value)
        {
            unchecked
            {
                int elements = Vector<float>.Count;
                float* retPtr = stackalloc float[elements];
                for (int i = 0; i < elements; i++)
                {
                    retPtr[i] = (float)value[i];
                }

                return new Vector<float>(retPtr);
            }
        }

        /// <summary>
        /// Converts a Vector{Int64} to a Vector{Double}.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The converted vector.</returns>
        [Intrinsic]
        public static unsafe Vector<double> ConvertToDouble(Vector<long> value)
        {
            unchecked
            {
                int elements = Vector<double>.Count;
                double* retPtr = stackalloc double[elements];
                for (int i = 0; i < elements; i++)
                {
                    retPtr[i] = (double)value[i];
                }

                return new Vector<double>(retPtr);
            }
        }

        /// <summary>
        /// Converts a Vector{UInt64} to a Vector{Double}.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The converted vector.</returns>
        [CLSCompliant(false)]
        [Intrinsic]
        public static unsafe Vector<double> ConvertToDouble(Vector<ulong> value)
        {
            unchecked
            {
                int elements = Vector<double>.Count;
                double* retPtr = stackalloc double[elements];
                for (int i = 0; i < elements; i++)
                {
                    retPtr[i] = (double)value[i];
                }

                return new Vector<double>(retPtr);
            }
        }

        /// <summary>
        /// Converts a Vector{Single} to a Vector{Int32}.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The converted vector.</returns>
        [Intrinsic]
        public static unsafe Vector<int> ConvertToInt32(Vector<float> value)
        {
            unchecked
            {
                int elements = Vector<int>.Count;
                int* retPtr = stackalloc int[elements];
                for (int i = 0; i < elements; i++)
                {
                    retPtr[i] = (int)value[i];
                }

                return new Vector<int>(retPtr);
            }
        }

        /// <summary>
        /// Converts a Vector{Single} to a Vector{UInt32}.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The converted vector.</returns>
        [CLSCompliant(false)]
        [Intrinsic]
        public static unsafe Vector<uint> ConvertToUInt32(Vector<float> value)
        {
            unchecked
            {
                int elements = Vector<uint>.Count;
                uint* retPtr = stackalloc uint[elements];
                for (int i = 0; i < elements; i++)
                {
                    retPtr[i] = (uint)value[i];
                }

                return new Vector<uint>(retPtr);
            }
        }

        /// <summary>
        /// Converts a Vector{Double} to a Vector{Int64}.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The converted vector.</returns>
        [Intrinsic]
        public static unsafe Vector<long> ConvertToInt64(Vector<double> value)
        {
            unchecked
            {
                int elements = Vector<long>.Count;
                long* retPtr = stackalloc long[elements];
                for (int i = 0; i < elements; i++)
                {
                    retPtr[i] = (long)value[i];
                }

                return new Vector<long>(retPtr);
            }
        }

        /// <summary>
        /// Converts a Vector{Double} to a Vector{UInt64}.
        /// </summary>
        /// <param name="value">The source vector.</param>
        /// <returns>The converted vector.</returns>
        [CLSCompliant(false)]
        [Intrinsic]
        public static unsafe Vector<ulong> ConvertToUInt64(Vector<double> value)
        {
            unchecked
            {
                int elements = Vector<ulong>.Count;
                ulong* retPtr = stackalloc ulong[elements];
                for (int i = 0; i < elements; i++)
                {
                    retPtr[i] = (ulong)value[i];
                }

                return new Vector<ulong>(retPtr);
            }
        }

        #endregion Same-Size Conversion
    }
}
