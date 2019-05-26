﻿using Adjutant.Geometry;
using Adjutant.Utilities;
using System;
using System.Collections.Generic;
using System.IO.Endian;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adjutant.Spatial
{
    /// <summary>
    /// A 4-dimensional vector compressed into 64 bits.
    /// Each dimension is limited to a minimum of 0 and a maximum of 1.
    /// Each dimension has 16 bits of precision.
    /// </summary>
    public struct UInt16N4 : IXMVector
    {
        private ushort x, y, z, w;

        public float X
        {
            get { return x / (float)ushort.MaxValue; }
            set { x = (ushort)(value * ushort.MaxValue); }
        }

        public float Y
        {
            get { return y / (float)ushort.MaxValue; }
            set { y = (ushort)(value * ushort.MaxValue); }
        }

        public float Z
        {
            get { return z / (float)ushort.MaxValue; }
            set { z = (ushort)(value * ushort.MaxValue); }
        }

        public float W
        {
            get { return w / (float)ushort.MaxValue; }
            set { w = (ushort)(value * ushort.MaxValue); }
        }

        [CLSCompliant(false)]
        public UInt16N4(ushort x, ushort y, ushort z, ushort w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public UInt16N4(float x, float y, float z, float w)
        {
            this.x = (ushort)(x * ushort.MaxValue);
            this.y = (ushort)(y * ushort.MaxValue);
            this.z = (ushort)(z * ushort.MaxValue);
            this.w = (ushort)(w * ushort.MaxValue);
        }

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z + W * W);

        public override string ToString() => Utils.CurrentCulture($"[{X:F6}, {Y:F6}, {Z:F6}, {W:F6}]");

        #region IXMVector

        VectorType IXMVector.VectorType => VectorType.UInt16_N4;

        #endregion
    }
}
