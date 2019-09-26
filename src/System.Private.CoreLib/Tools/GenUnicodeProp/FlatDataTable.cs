// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace GenUnicodeProp
{
    internal sealed class FlatDataTable<T>
    {
        // If a codepoint does not have data, this specifies the default value.
        private readonly T DefaultValue;

        // This contains the data mapping between codepoints and values.
        private readonly SortedDictionary<uint, T> RawData = new SortedDictionary<uint, T>();

        public FlatDataTable(T defaultValue)
        {
            DefaultValue = defaultValue;
        }

        public void AddData(uint codepoint, T value) => RawData[codepoint] = value;

        public byte[] GetBytesFlat(Func<T, byte[]> getValueBytesCallback)
        {
            var str = new List<byte>();
            foreach (var v in RawData.Values)
                str.AddRange(getValueBytesCallback(v ?? DefaultValue));
            return str.ToArray();
        }
    }
}
