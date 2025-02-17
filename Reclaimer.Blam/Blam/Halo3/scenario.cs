﻿using Reclaimer.Blam.Common;
using Reclaimer.IO;

namespace Reclaimer.Blam.Halo3
{
    public class scenario
    {
        [Offset(12, MaxVersion = (int)CacheType.Halo3Retail)]
        [Offset(20, MinVersion = (int)CacheType.Halo3Retail, MaxVersion = (int)CacheType.MccHalo3U12)]
        [Offset(24, MinVersion = (int)CacheType.MccHalo3U12, MaxVersion = (int)CacheType.Halo3ODST)]
        [Offset(20, MinVersion = (int)CacheType.Halo3ODST, MaxVersion = (int)CacheType.MccHalo3ODSTU7)]
        [Offset(24, MinVersion = (int)CacheType.MccHalo3ODSTU7)]
        public BlockCollection<StructureBspBlock> StructureBsps { get; set; }

        [Offset(1736, MaxVersion = (int)CacheType.Halo3Beta)]
        [Offset(1720, MinVersion = (int)CacheType.Halo3Beta, MaxVersion = (int)CacheType.Halo3Retail)]
        [Offset(1776, MinVersion = (int)CacheType.Halo3Retail, MaxVersion = (int)CacheType.MccHalo3F6)]
        [Offset(1764, MinVersion = (int)CacheType.MccHalo3F6, MaxVersion = (int)CacheType.MccHalo3U12)]
        [Offset(1700, MinVersion = (int)CacheType.MccHalo3U12, MaxVersion = (int)CacheType.MccHalo3U13)]
        [Offset(1708, MinVersion = (int)CacheType.MccHalo3U13, MaxVersion = (int)CacheType.Halo3ODST)]
        [Offset(1852, MinVersion = (int)CacheType.Halo3ODST, MaxVersion = (int)CacheType.MccHalo3ODSTF3)]
        [Offset(1840, MinVersion = (int)CacheType.MccHalo3ODSTF3, MaxVersion = (int)CacheType.MccHalo3ODSTU7)]
        [Offset(1776, MinVersion = (int)CacheType.MccHalo3ODSTU7, MaxVersion = (int)CacheType.MccHalo3ODSTU8)]
        [Offset(1772, MinVersion = (int)CacheType.MccHalo3ODSTU8)]
        public TagReference ScenarioLightmapReference { get; set; }
    }

    [FixedSize(104, MaxVersion = (int)CacheType.Halo3Retail)]
    [FixedSize(108, MinVersion = (int)CacheType.Halo3Retail)]
    [DebuggerDisplay($"{{{nameof(BspReference)},nq}}")]
    public class StructureBspBlock
    {
        [Offset(0)]
        public TagReference BspReference { get; set; }
    }
}
