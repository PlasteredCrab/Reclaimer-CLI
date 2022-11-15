﻿using Adjutant.Spatial;
using Reclaimer.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Reclaimer.Saber3D.Halo1X.Geometry
{
    [DataBlock(0xE402)]
    public class TemplateBlock : DataBlock
    {
        public List<DataBlock> ChildBlocks { get; } = new List<DataBlock>();

        internal override void Read(EndianReader reader)
        {
            while (reader.Position < EndOfBlock)
                ChildBlocks.Add(reader.ReadBlock());
        }
    }

    [DataBlock(0x0100, ExpectedSize = 0)]
    public class EmptyBlock : DataBlock
    {

    }

    [DataBlock(0xE502)]
    public class StringBlock0xE502 : StringBlock
    {
        internal override int ExpectedSize => Value.Length + 2;

        public byte Unknown { get; set; }

        internal override void Read(EndianReader reader)
        {
            Value = reader.ReadNullTerminatedString();
            Unknown = reader.ReadByte();
        }

        internal override void Validate()
        {
            if (Unknown != 0 || BlockSize > Value.Length + 2)
                Debugger.Break();
        }
    }

    [DataBlock(0x1603, ExpectedSize = 3)]
    public class Block0x1603 : DataBlock
    {
        [Offset(0)]
        public byte Unknown0 { get; set; }

        [Offset(1)]
        public byte Unknown1 { get; set; }

        [Offset(2)]
        public byte Unknown2 { get; set; }

        internal override void Validate()
        {
            if ((Unknown0, Unknown1, Unknown2) != (2, 1, 1) || BlockSize > 3)
                Debugger.Break();
        }
    }

    [DataBlock(0x5501)]
    public class MaterialListBlock : DataBlock
    {
        public int MaterialCount { get; set; }
        public List<MaterialReferenceBlock> Materials { get; } = new List<MaterialReferenceBlock>();

        internal override void Read(EndianReader reader)
        {
            MaterialCount = reader.ReadInt32();

            while (Materials.Count < MaterialCount)
                Materials.Add((MaterialReferenceBlock)reader.ReadBlock());

            EndRead(reader.Position);
        }
    }

    public abstract class StringBlock : DataBlock
    {
        internal override int ExpectedSize => Value.Length + 1;

        [Offset(0), NullTerminated]
        public virtual string Value { get; set; }

        protected override string GetDebuggerDisplay() => new { Type = $"[{BlockType:X4}] {TypeName}", StartOfBlock, Value }.ToString();
    }

    [DataBlock(0x5601)]
    public class MaterialReferenceBlock : StringBlock
    {

    }

    [DataBlock(0x0403)]
    public class StringBlock0x0403 : StringBlock
    {

    }

    [DataBlock(0x0503)]
    public class TransformBlock0x0503 : DataBlock
    {
        public List<DataBlock> ChildBlocks { get; } = new List<DataBlock>();

        public MatrixListBlock0x0D03 MatrixList => ChildBlocks.OfType<MatrixListBlock0x0D03>().SingleOrDefault();

        internal override void Read(EndianReader reader)
        {
            while (reader.Position < EndOfBlock)
                ChildBlocks.Add(reader.ReadBlock());
        }
    }

    [DataBlock(0x0D03)]
    public class MatrixListBlock0x0D03 : DataBlock
    {
        public int MatrixCount { get; set; }
        public short Unknown0 { get; set; } //always 3?
        public byte Unknown1 { get; set; } //always 0, 2 or 3?

        public List<Matrix4x4> Matrices { get; } = new List<Matrix4x4>();

        internal override void Read(EndianReader reader)
        {
            MatrixCount = reader.ReadInt32();
            Unknown0 = reader.ReadInt16();
            Unknown1 = reader.ReadByte();

            while (reader.Position < EndOfBlock && Matrices.Count < MatrixCount)
                Matrices.Add(reader.ReadMatrix4x4());

            EndRead(reader.Position);
        }
    }

    [DataBlock(0xBA01)]
    public class StringBlock0xBA01 : StringBlock
    {

    }

    [DataBlock(0x1501)]
    public class StringBlock0x1501 : StringBlock
    {

    }

    [DataBlock(0x0803, ExpectedSize = 4 + 4 * 3 * 2)]
    public class BoundsBlock0x0803 : DataBlock
    {
        [Offset(0)]
        public int Unknown0 { get; set; } //count?

        [Offset(4)]
        public RealVector3D MinBound { get; set; }

        [Offset(16)]
        public RealVector3D MaxBound { get; set; }

        public bool IsEmpty => MinBound == MaxBound;
    }

    [DataBlock(0x1D01)]
    public class BoundsBlock0x1D01 : BoundsBlock0x0803
    {

    }

    [DataBlock(0xE802)]
    public class BoneListBlock : DataBlock
    {
        public int BoneCount { get; set; }
        public List<DataBlock> ChildBlocks { get; } = new List<DataBlock>();
        public List<BoneBlock> Bones { get; } = new List<BoneBlock>();

        internal override void Read(EndianReader reader)
        {
            BoneCount = reader.ReadInt32();

            //empty block after every bone for some reason
            while (ChildBlocks.Count < BoneCount * 2)
                ChildBlocks.Add(reader.ReadBlock());

            Bones.AddRange(ChildBlocks.OfType<BoneBlock>());

            EndRead(reader.Position);
        }
    }

    [DataBlock(0xE902)]
    public class BoneBlock : DataBlock
    {
        public float Unknown { get; set; }
        public int UnknownAsInt { get; set; }

        public List<DataBlock> ChildBlocks { get; } = new List<DataBlock>();

        public RealVector3D Position => ChildBlocks.OfType<PositionBlock>().Single().Value;
        public RealVector4D Rotation => ChildBlocks.OfType<RotationBlock>().Single().Value;
        public RealVector3D UnknownVector0xFC02 => ChildBlocks.OfType<VectorBlock0xFC02>().Single().Value;
        public float Scale => ChildBlocks.OfType<ScaleBlock0x0A03>().Single().Value;

        internal override void Read(EndianReader reader)
        {
            UnknownAsInt = reader.PeekInt32();
            Unknown = reader.ReadSingle();

            while (reader.Position < EndOfBlock)
                ChildBlocks.Add(reader.ReadBlock());
        }
    }

    [DataBlock(0xFA02, ExpectedSize = 4 * 3)]
    public class PositionBlock : DataBlock
    {
        [Offset(0)]
        public RealVector3D Value { get; set; }
    }

    [DataBlock(0xFB02, ExpectedSize = 4 * 4)]
    public class RotationBlock : DataBlock
    {
        [Offset(0)]
        public RealVector4D Value { get; set; }
    }

    [DataBlock(0xFC02, ExpectedSize = 4 * 3)]
    public class VectorBlock0xFC02 : DataBlock
    {
        [Offset(0)]
        public RealVector3D Value { get; set; } //usually 1,1,1 (maybe actually scale?)
    }

    [DataBlock(0x0A03, ExpectedSize = 4)]
    public class ScaleBlock0x0A03 : DataBlock
    {
        [Offset(0)]
        public float Value { get; set; } //usually 1
    }

    [DataBlock(0xF900, ExpectedSize = 4 * 16)]
    public class MatrixBlock0xF900 : DataBlock
    {
        public Matrix4x4 Value { get; set; }

        internal override void Read(EndianReader reader)
        {
            Value = reader.ReadMatrix4x4();
        }
    }

    [DataBlock(0x1103)]
    public class UnknownBlock0x1103 : DataBlock
    {
        internal override int ExpectedSize => 4 + 4 * Count;

        public int Count { get; set; }

        public short[] Unknown { get; set; }

        internal override void Read(EndianReader reader)
        {
            Count = reader.ReadInt32();
            Unknown = reader.ReadEnumerable<short>(Count * 2).ToArray();
        }
    }

    [DataBlock(0x1203)]
    public class StringBlock0x1203 : StringBlock
    {
        internal override int ExpectedSize => 4 + Value.Length;

        [Offset(0), LengthPrefixed]
        public override string Value { get; set; }
    }

    [DataBlock(0xF000)]
    public class NodeGraphBlock0xF000 : DataBlock
    {
        public List<DataBlock> ChildBlocks { get; } = new List<DataBlock>();
        public List<NodeGraphBlock0xF000> ChildNodes { get; } = new List<NodeGraphBlock0xF000>();

        public bool IsRootNode => ChildBlocks[0] is CountBlock0x2C01;
        public int ChildNodeCount => ChildBlocks.OfType<CountBlock0x2C01>().SingleOrDefault()?.Count ?? default;
        public string MeshName => Mesh?.Name;
        public string UnknownString0xFD00 => ChildBlocks.OfType<UnknownBlock0xFD00>().SingleOrDefault()?.UnknownString;
        public string UnknownString0x1501 => ChildBlocks.OfType<StringBlock0x1501>().SingleOrDefault()?.Value;
        public MeshBlock0xB903 Mesh => ChildBlocks.OfType<MeshBlock0xB903>().SingleOrDefault();
        public Matrix4x4? Transform => ChildBlocks.OfType<MatrixBlock0xF900>().SingleOrDefault()?.Value;
        public BoundsBlock0x1D01 Bounds => ChildBlocks.OfType<BoundsBlock0x1D01>().SingleOrDefault();
        public FaceListBlock Faces => ChildBlocks.OfType<FaceListBlock>().SingleOrDefault();

        internal override void Read(EndianReader reader)
        {
            while (reader.Position < EndOfBlock)
                ChildBlocks.Add(reader.ReadBlock());

            ChildNodes.AddRange(ChildBlocks.OfType<NodeGraphBlock0xF000>());
        }

        internal override void Validate()
        {
            if (ChildBlocks[0] is CountBlock0x2C01 c && ChildBlocks.Count != c.Count * 2 + 1)
                Debugger.Break();

            if (ChildBlocks.OfType<MeshBlock0xB903>().Skip(1).Any())
                Debugger.Break();
        }

        protected override string GetDebuggerDisplay() => new { Type = $"[{BlockType:X4}] {TypeName}", ChildNodeCount, HasGeometry = Mesh?.VertexCount > 0, Name = MeshName }.ToString();
    }

    [DataBlock(0x2C01, ExpectedSize = 4)]
    public class CountBlock0x2C01 : DataBlock
    {
        [Offset(0)]
        public int Count { get; set; }
    }

    [DataBlock(0xFD00, ExpectedChildCount = 1)]
    public class UnknownBlock0xFD00 : DataBlock
    {
        public List<DataBlock> ChildBlocks { get; } = new List<DataBlock>();

        public string UnknownString => ChildBlocks.OfType<StringBlock0xBA01>().SingleOrDefault()?.Value;

        internal override void Read(EndianReader reader)
        {
            while (reader.Position < EndOfBlock)
                ChildBlocks.Add(reader.ReadBlock());
        }

        internal override void Validate()
        {
            if (ChildBlocks.Count > ExpectedChildCount)
                Debugger.Break();
        }
    }

    [DataBlock(0xB903)]
    public class MeshBlock0xB903 : DataBlock
    {
        public string Name { get; set; }
        public short Id { get; set; }
        public short Unknown0 { get; set; } // 0x2400
        public byte Unknown1 { get; set; }
        public short Unknown2 { get; set; }
        public short Unknown3 { get; set; }
        public int VertexCount { get; set; }
        public int FaceCount { get; set; }

        internal override void Read(EndianReader reader)
        {
            Name = reader.ReadNullTerminatedString();
            Id = reader.ReadInt16();
            Unknown0 = reader.ReadInt16();
            Unknown1 = reader.ReadByte();
            Unknown2 = reader.ReadInt16();
            Unknown3 = reader.ReadInt16();
            VertexCount = reader.ReadInt32();
            FaceCount = reader.ReadInt32();

            EndRead(reader.Position);
        }

        protected override string GetDebuggerDisplay() => new { Type = $"[{BlockType:X4}] {TypeName}", VertexCount, Name }.ToString();
    }

    [DataBlock(0x2801, ExpectedSize = 28)]
    public class SubmeshBlock0x2801 : DataBlock
    {
        public byte Unknown0 { get; set; } //0x81
        public int Unknown1 { get; set; }
        public byte Unknown2 { get; set; } //0xFF
        public short UnknownEnum { get; set; }

        public short VertexCount { get; set; }
        public short IndexCount { get; set; }
        public int UnknownId { get; set; }
        public int Unknown3 { get; set; }
        public int Unknown4 { get; set; }
        public short Unknown5 { get; set; }
        public short Unknown6 { get; set; }

        internal override void Read(EndianReader reader)
        {
            Unknown0 = reader.ReadByte();
            Unknown1 = reader.ReadInt32();
            Unknown2 = reader.ReadByte();
            UnknownEnum = reader.ReadInt16(); //mesh type enum? 16 = standard, 18 = skin, 19 = skincompound

            VertexCount = reader.ReadInt16(); //vertex count
            IndexCount = reader.ReadInt16(); //face count * 3 [usually]
            UnknownId = reader.ReadInt32(); //object ID, unknown purpose, same as parent ID, only used on vertless meshes (inheritors)
            Unknown3 = reader.ReadInt32(); //increases with vert count
            Unknown4 = reader.ReadInt32(); //seems to increase with mesh size
            Unknown5 = reader.ReadInt16(); //not used on standard meshes
            Unknown6 = reader.ReadInt16(); //not used on standard meshes

            EndRead(reader.Position);
        }
    }

    [DataBlock(0x3201, ExpectedSize = 6)]
    public class SubmeshBlock0x3201 : DataBlock
    {
        [Offset(0)]
        public short UnknownId0 { get; set; } //points to first inheritor if skincompound, otherwise parent bone

        [Offset(2)]
        public byte UnknownCount0 { get; set; } //number of inheritors/bones (starts at unkID0 and increments through object IDs)

        [Offset(3)]
        public short UnknownId1 { get; set; } //secondary parent bone

        [Offset(5)]
        public byte UnknownCount1 { get; set; } //secondary number of bones
    }

    [DataBlock(0x3401, ExpectedSize = 2)]
    public class SubmeshBlock0x3401 : DataBlock
    {
        [Offset(0)]
        public short UnknownId0 { get; set; } //points to inherited sharingObj
    }

    [DataBlock(0x2F01)]
    public class MaterialBlock0x2F01 : DataBlock
    {
        internal override int ExpectedSize => 1 + 5 * Count;

        public byte Count { get; set; }
        public (byte Index, int Count)[] UnknownArray { get; set; }

        internal override void Read(EndianReader reader)
        {
            Count = reader.ReadByte();
            UnknownArray = new (byte, int)[Count];

            for (var i = 0; i < Count; i++)
                UnknownArray[i] = (reader.ReadByte(), reader.ReadInt32());
        }
    }

    [DataBlock(0x1C01, ExpectedSize = 8)]
    public class MaterialBlock0x1C01 : DataBlock
    {
        [Offset(0)]
        public float Unknown0 { get; set; }

        [Offset(4)]
        public float Unknown1 { get; set; }
    }

    [DataBlock(0x2001, ExpectedSize = 32)]
    public class MaterialBlock0x2001 : DataBlock
    {
        [Offset(0)]
        public float Unknown0 { get; set; }

        [Offset(4)]
        public float Unknown1 { get; set; }

        [Offset(8)]
        public float Unknown2 { get; set; }

        [Offset(12)]
        public float Unknown3 { get; set; }

        [Offset(16)]
        public float Unknown4 { get; set; }

        [Offset(20)]
        public float Unknown5 { get; set; }

        [Offset(24)]
        public float Unknown6 { get; set; }

        [Offset(28)]
        public float Unknown7 { get; set; }
    }

    [DataBlock(0x2E01, ExpectedSize = 5)]
    public class MeshBlock0x3501 : DataBlock
    {
        [Offset(0)]
        public short Unknown0 { get; set; } //0x1200

        [Offset(2)]
        public byte UnknownEnum { get; set; } //maybe flags? 03 for no materials, 86/8E for world, 87 for one material, 8F for two, 9F for three, BF for four

        [Offset(3)]
        public short Unknown1 { get; set; } //often 0x4001
    }

    [DataBlock(0x2901, ExpectedSize = 10)]
    public class MeshBlock0x2901 : DataBlock
    {
        [Offset(0)]
        public short ParentId { get; set; }

        [Offset(2)]
        public int VertexOffset { get; set; }

        [Offset(6)]
        public int IndexOffset { get; set; }
    }

    [DataBlock(0x3501, ExpectedSize = 12)]
    public class UnknownBlock0x3501 : DataBlock
    {
        [Offset(0)]
        public short Unknown0 { get; set; }

        [Offset(2)]
        public short Unknown1 { get; set; }

        [Offset(4)]
        public short Unknown2 { get; set; }

        [Offset(6)]
        public short Unknown3 { get; set; } //0x4801

        [Offset(8)]
        public short Unknown4 { get; set; } //0x4801

        [Offset(10)]
        public short Unknown5 { get; set; } //0x4801
    }

    [DataBlock(0xF200)]
    public class FaceListBlock : DataBlock
    {
        internal override int ExpectedSize => 4 + 2 * Count * 3;

        [Offset(0)]
        public int Count { get; set; }

        // + ushort * Count * 3
    }

    [DataBlock(0x3301)]
    public class BlendIndexBlock : DataBlock
    {
        [Offset(0)]
        public short FirstBoneId { get; set; }

        [Offset(2)]
        public short BoneCount { get; set; }

        // + UByte4 * vertex count
    }

    [DataBlock(0x1A01)]
    public class BlendWeightBlock : DataBlock
    {
        //UByteN4 * vertex count
    }
}
