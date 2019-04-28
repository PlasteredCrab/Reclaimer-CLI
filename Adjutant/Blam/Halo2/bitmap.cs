﻿using Adjutant.Spatial;
using Adjutant.Utilities;
using System;
using System.Collections.Generic;
using System.IO.Endian;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Dds;
using System.IO;
using Adjutant.IO;

namespace Adjutant.Blam.Halo2
{
    public class bitmap : IBitmap
    {
        private const string mainmenuMapName = "mainmenu.map";
        private const string sharedMapName = "shared.map";
        private const string singlePlayerSharedMapName = "single_player_shared.map";

        private readonly CacheFile cache;

        public bitmap(CacheFile cache)
        {
            this.cache = cache;
        }

        [Offset(60)]
        public BlockCollection<Sequence> Sequences { get; set; }

        [Offset(68)]
        public BlockCollection<BitmapData> Bitmaps { get; set; }

        #region IBitmap

        int IBitmap.BitmapCount => Bitmaps.Count;

        public DdsImage ToDds(int index)
        {
            if (index < 0 || index >= Bitmaps.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            var submap = Bitmaps[index];

            var cacheIndex = (submap.Lod0Pointer & 0xC0000000) >> 30;
            var address = submap.Lod0Pointer & 0x3FFFFFFF;

            var dir = Directory.GetParent(cache.FileName).FullName;

            var mapName = Path.GetFileName(cache.FileName);
            switch(cacheIndex)
            {
                case 1:
                    mapName = mainmenuMapName;
                    break;
                case 2:
                    mapName = sharedMapName;
                    break;
                case 3:
                    mapName = singlePlayerSharedMapName;
                    break;
            }

            var targetMap = Path.Combine(dir, mapName);

            byte[] data;

            using (var fs = new FileStream(targetMap, FileMode.Open, FileAccess.Read))
            using (var reader = new DependencyReader(fs, ByteOrder.LittleEndian))
            {
                reader.Seek(address, SeekOrigin.Begin);
                data = reader.ReadBytes(submap.Lod0Size);
            }

            DxgiFormat dxgi;
            switch (submap.BitmapFormat)
            {
                case TextureFormat.DXT1:
                    dxgi = DxgiFormat.BC1_UNorm;
                    break;
                case TextureFormat.DXT3:
                    dxgi = DxgiFormat.BC2_UNorm;
                    break;
                case TextureFormat.DXT5:
                    dxgi = DxgiFormat.BC3_UNorm;
                    break;

                case TextureFormat.A8R8G8B8:
                    dxgi = DxgiFormat.B8G8R8A8_UNorm;
                    break;

                case TextureFormat.X8R8G8B8:
                    dxgi = DxgiFormat.B8G8R8X8_UNorm;
                    break;

                case TextureFormat.R5G6B5:
                    dxgi = DxgiFormat.B5G6R5_UNorm;
                    break;

                case TextureFormat.A1R5G5B5:
                    dxgi = DxgiFormat.B5G5R5A1_UNorm;
                    break;

                case TextureFormat.A4R4G4B4:
                    dxgi = DxgiFormat.B4G4R4A4_UNorm;
                    break;

                case TextureFormat.P8_bump:
                case TextureFormat.P8:
                    dxgi = DxgiFormat.P8;
                    break;

                default: throw new NotSupportedException();
            }

            return new DdsImage(submap.Height, submap.Width, dxgi, DxgiTextureType.Texture2D, data);
        } 

        #endregion
    }

    [FixedSize(60)]
    public class Sequence
    {
        [Offset(0)]
        [NullTerminated(Length = 32)]
        public string Name { get; set; }

        [Offset(32)]
        public short FirstSubmapIndex { get; set; }

        [Offset(34)]
        public short BitmapCount { get; set; }

        [Offset(52)]
        public BlockCollection<Sprite> Sprites { get; set; }
    }

    [FixedSize(32)]
    public class Sprite
    {
        [Offset(0)]
        public short SubmapIndex { get; set; }

        [Offset(8)]
        public float Left { get; set; }

        [Offset(12)]
        public float Right { get; set; }

        [Offset(16)]
        public float Top { get; set; }

        [Offset(20)]
        public float Bottom { get; set; }

        [Offset(24)]
        public RealVector2D RegPoint { get; set; }
    }

    [FixedSize(116)]
    public class BitmapData
    {
        [Offset(0)]
        [FixedLength(4)]
        public string Class { get; set; }

        [Offset(4)]
        public short Width { get; set; }

        [Offset(6)]
        public short Height { get; set; }

        [Offset(8)]
        public short Depth { get; set; }

        [Offset(10)]
        public TextureType BitmapType { get; set; }

        [Offset(12)]
        public TextureFormat BitmapFormat { get; set; }

        [Offset(14)]
        public short Flags { get; set; }

        [Offset(16)]
        public short RegX { get; set; }

        [Offset(18)]
        public short RegY { get; set; }

        [Offset(20)]
        public short MipmapCount { get; set; }

        [Offset(28)]
        public int Lod0Pointer { get; set; }

        [Offset(32)]
        public int Lod1Pointer { get; set; }

        [Offset(36)]
        public int Lod2Pointer { get; set; }

        [Offset(52)]
        public int Lod0Size { get; set; }

        [Offset(56)]
        public int Lod1Size { get; set; }

        [Offset(60)]
        public int Lod2Size { get; set; }
    }

    public enum TextureType : short
    {
        Texture2D = 0,
        Texture3D = 1,
        CubeMap = 2,
        Sprite = 3,
        UIBitmap = 4
    }

    public enum TextureFormat : short
    {
        A8 = 0,
        Y8 = 1,
        AY8 = 2,
        A8Y8 = 3,
        Unused4 = 4,
        Unused5 = 5,
        R5G6B5 = 6,
        Unused7 = 7,
        A1R5G5B5 = 8,
        A4R4G4B4 = 9,
        X8R8G8B8 = 10,
        A8R8G8B8 = 11,
        Unused12 = 12,
        Unused13 = 13,
        DXT1 = 14,
        DXT3 = 15,
        DXT5 = 16,
        P8_bump = 17,
        P8 = 18,
        ARGBFP32 = 19,
        RGBFP32 = 20,
        RGBFP16 = 21,
        U8V8 = 22
    }
}
