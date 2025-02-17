﻿using Reclaimer.Blam.Common;
using Reclaimer.Saber3D.Common;
using System.IO;
using System.Runtime.CompilerServices;

namespace Reclaimer.Blam.Utilities
{
    internal static class Exceptions
    {
        public static ArgumentException ParamMustBeNonZero(string paramName) => new ArgumentException(Utils.CurrentCulture($"{paramName} cannot be zero."), paramName);
        public static InvalidOperationException CoordSysNotConvertable() => new InvalidOperationException(Utils.CurrentCulture($"No conversion exists between the given coordinate systems."));
        public static ArgumentException NotAValidMapFile(string fileName) => new ArgumentException(Utils.CurrentCulture($"The file '{Utils.GetFileName(fileName)}' cannot be opened. It is not a valid map file or it may be compressed."));
        public static ArgumentException UnknownMapFile(string fileName) => new ArgumentException(Utils.CurrentCulture($"The file '{Utils.GetFileName(fileName)}' cannot be opened. It looks like a valid map file, but may not be a supported version."));
        public static NotSupportedException BitmapFormatNotSupported(string formatName) => new NotSupportedException($"The BitmapFormat '{formatName}' is not supported.");
        public static ArgumentException NotASaberTextureItem(IPakItem item) => new ArgumentException($"'{item.Name}' is not a texture file.");
        public static NotSupportedException AmbiguousScenarioReference() => new NotSupportedException("Could not determine primary scenario tag.");
        public static InvalidOperationException GeometryHasNoEdges() => new InvalidOperationException("Geometry contains no edges.");
        public static NotSupportedException ResourceDataNotSupported(ICacheFile cache) => new NotSupportedException($"Cannot read resource data for {nameof(CacheType)}.{cache.CacheType}");

        public static void ThrowIfFileNotFound(string argument)
        {
            if (!File.Exists(argument))
                throw new FileNotFoundException("The file does not exist.", argument);
        }

        public static void ThrowIfNegative(int argument, [CallerArgumentExpression("argument")] string paramName = null)
        {
            if (argument < 0)
                throw new ArgumentOutOfRangeException(paramName, $"'{paramName}' must be non-negative.");
        }

        public static void ThrowIfNonPositive(int argument, [CallerArgumentExpression("argument")] string paramName = null)
        {
            if (argument <= 0)
                throw new ArgumentOutOfRangeException(paramName, $"'{paramName}' must be greater than zero.");
        }

        public static void ThrowIfOutOfRange<T>(T argument, T inclusiveMin, T exclusiveMax, [CallerArgumentExpression("argument")] string paramName = null) where T : IComparable<T>
        {
            if (argument.CompareTo(inclusiveMin) < 0 || argument.CompareTo(exclusiveMax) >= 0)
                throw new ArgumentOutOfRangeException(paramName, $"'{paramName}' must be greater than or equal to {inclusiveMin} and less than {exclusiveMax}.");
        }

        public static void ThrowIfIndexOutOfRange(int argument, int count, [CallerArgumentExpression("argument")] string paramName = null)
        {
            if (argument < 0 || argument >= count)
                throw new ArgumentOutOfRangeException(paramName, "Index was out of range. Must be non-negative and less than the size of the collection.");
        }
    }
}
