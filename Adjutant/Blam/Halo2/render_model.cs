﻿using Adjutant.Geometry;
using Adjutant.Spatial;
using Adjutant.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Endian;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Adjutant.Blam.Halo2
{
    public class render_model : IRenderGeometry
    {
        private readonly CacheFile cache;

        public render_model(CacheFile cache)
        {
            this.cache = cache;
        }

        [Offset(20)]
        public BlockCollection<BoundingBoxBlock> BoundingBoxes { get; set; }

        [Offset(28)]
        public BlockCollection<RegionBlock> Regions { get; set; }

        [Offset(36)]
        public BlockCollection<SectionBlock> Sections { get; set; }

        [Offset(72)]
        public BlockCollection<NodeBlock> Nodes { get; set; }

        [Offset(88)]
        public BlockCollection<MarkerGroupBlock> MarkerGroups { get; set; }

        [Offset(96)]
        public BlockCollection<ShaderBlock> Shaders { get; set; }

        #region IRenderGeometry

        int IRenderGeometry.LodCount => 1;

        public IGeometryModel ReadGeometry(int lod)
        {
            if (lod < 0 || lod >= ((IRenderGeometry)this).LodCount)
                throw new ArgumentOutOfRangeException(nameof(lod));

            var model = new GeometryModel { CoordinateSystem = CoordinateSystem.Default };

            model.Nodes.AddRange(Nodes);
            model.MarkerGroups.AddRange(MarkerGroups);
            model.Bounds.AddRange(BoundingBoxes);

            #region Shaders
            var shadersMeta = Shaders.Select(s => s.ShaderReference.Tag.ReadMetadata<shader>()).ToList();
            foreach (var shader in shadersMeta)
            {
                var bitmTag = shader.ShaderMaps[0].DiffuseBitmapReference.Tag;
                if (bitmTag == null)
                {
                    model.Materials.Add(null);
                    continue;
                }

                var mat = new GeometryMaterial
                {
                    Name = bitmTag.FileName,
                    Diffuse = bitmTag.ReadMetadata<bitmap>(),
                    Tiling = new RealVector2D(1, 1)
                };

                model.Materials.Add(mat);
            } 
            #endregion

            foreach (var region in Regions)
            {
                var gRegion = new GeometryRegion { Name = region.Name };
                gRegion.Permutations.AddRange(region.Permutations.Select(p =>
                    new GeometryPermutation
                    {
                        Name = p.Name,
                        NodeIndex = byte.MaxValue,
                        Transform = Matrix4x4.Identity,
                        TransformScale = 1,
                        BoundsIndex = 0,
                        MeshIndex = p.SectionIndex
                    }));

                model.Regions.Add(gRegion);
            }

            foreach (var section in Sections)
            {
                var data = section.RawPointer.ReadData(section.RawSize);
                var headerSize = section.RawSize - section.DataSize - 4;

                using (var ms = new MemoryStream(data))
                using (var reader = new EndianReader(ms))
                {
                    var sectionInfo = reader.ReadObject<ResourceDetails>();

                    var submeshResource = section.Resources[0];
                    var indexResource = section.Resources.FirstOrDefault(r => r.Type0 == 32);
                    var vertexResource = section.Resources.FirstOrDefault(r => r.Type0 == 56 && r.Type1 == 0);
                    var uvResource = section.Resources.FirstOrDefault(r => r.Type0 == 56 && r.Type1 == 1);
                    var normalsResource = section.Resources.FirstOrDefault(r => r.Type0 == 56 && r.Type1 == 2);
                    var nodeMapResource = section.Resources.FirstOrDefault(r => r.Type0 == 100);

                    reader.Seek(headerSize + submeshResource.Offset, SeekOrigin.Begin);
                    var submeshes = reader.ReadEnumerable<Submesh>(submeshResource.Size / 72).ToList();

                    foreach (var submesh in submeshes)
                    {
                        var gSubmesh = new GeometrySubmesh
                        {
                            MaterialIndex = submesh.ShaderIndex,
                            IndexStart = submesh.IndexStart,
                            IndexLength = submesh.IndexLength
                        };

                        var permutations = model.Regions
                            .SelectMany(r => r.Permutations)
                            .Where(p => p.MeshIndex == Sections.IndexOf(section));

                        foreach (var p in permutations)
                            ((List<IGeometrySubmesh>)p.Submeshes).Add(gSubmesh);
                    }

                    var mesh = new GeometryMesh();

                    if (section.FaceCount * 3 == sectionInfo.IndexCount)
                        mesh.IndexFormat = IndexFormat.Triangles;
                    else mesh.IndexFormat = IndexFormat.Stripped;

                    reader.Seek(headerSize + indexResource.Offset, SeekOrigin.Begin);
                    mesh.Indicies = reader.ReadEnumerable<ushort>(sectionInfo.IndexCount).Select(i => (int)i).ToArray();

                    var nodeMap = new byte[0];
                    if (nodeMapResource != null)
                    {
                        reader.Seek(headerSize + nodeMapResource.Offset, SeekOrigin.Begin);
                        nodeMap = reader.ReadBytes(sectionInfo.NodeMapCount);
                    }

                    #region Vertices
                    mesh.Vertices = new IVertex[section.VertexCount];
                    var vertexSize = vertexResource.Size / section.VertexCount;
                    for (int i = 0; i < section.VertexCount; i++)
                    {
                        var vert = new Vertex();

                        reader.Seek(headerSize + vertexResource.Offset + i * vertexSize, SeekOrigin.Begin);
                        vert.Position = new Int16N3(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());

                        mesh.Vertices[i] = vert;
                    }

                    for (int i = 0; i < section.VertexCount; i++)
                    {
                        var vert = (Vertex)mesh.Vertices[i];

                        reader.Seek(headerSize + uvResource.Offset + i * 4, SeekOrigin.Begin);
                        vert.TexCoords = new Int16N2(reader.ReadInt16(), reader.ReadInt16());
                    }

                    for (int i = 0; i < section.VertexCount; i++)
                    {
                        var vert = (Vertex)mesh.Vertices[i];

                        reader.Seek(headerSize + normalsResource.Offset + i * 12, SeekOrigin.Begin);
                        vert.Normal = new HenDN3(reader.ReadUInt32());
                    } 
                    #endregion

                    model.Meshes.Add(mesh);
                }
            }

            return model;
        }

        #endregion

        private struct ResourceDetails
        {
            [Offset(40)]
            [StoreType(typeof(ushort))]
            public int IndexCount { get; set; }

            [Offset(108)]
            [StoreType(typeof(ushort))]
            public int NodeMapCount { get; set; }
        }

        [FixedSize(72)]
        private struct Submesh
        {
            [Offset(4)]
            public short ShaderIndex { get; set; }

            [Offset(6)]
            [StoreType(typeof(ushort))]
            public int IndexStart { get; set; }

            [Offset(8)]
            [StoreType(typeof(ushort))]
            public int IndexLength { get; set; }
        }
    }

    [FixedSize(56)]
    public class BoundingBoxBlock : IRealBounds5D
    {
        [Offset(0)]
        public RealBounds XBounds { get; set; }

        [Offset(8)]
        public RealBounds YBounds { get; set; }

        [Offset(16)]
        public RealBounds ZBounds { get; set; }

        [Offset(24)]
        public RealBounds UBounds { get; set; }

        [Offset(32)]
        public RealBounds VBounds { get; set; }

        #region IRealBounds5D

        IRealBounds IRealBounds5D.XBounds => XBounds;

        IRealBounds IRealBounds5D.YBounds => YBounds;

        IRealBounds IRealBounds5D.ZBounds => ZBounds;

        IRealBounds IRealBounds5D.UBounds => UBounds;

        IRealBounds IRealBounds5D.VBounds => VBounds;

        #endregion
    }

    [FixedSize(16)]
    public class RegionBlock
    {
        [Offset(0)]
        public StringId Name { get; set; }

        [Offset(8)]
        public BlockCollection<PermutationBlock> Permutations { get; set; }

        public override string ToString() => Name;
    }

    [FixedSize(16)]
    public class PermutationBlock
    {
        [Offset(0)]
        public StringId Name { get; set; }

        [Offset(14)]
        public short SectionIndex { get; set; }

        public override string ToString() => Name;
    }

    [FixedSize(92)]
    public class SectionBlock
    {
        [Offset(0)]
        public short WeightType { get; set; }

        [Offset(4)]
        [StoreType(typeof(ushort))]
        public int VertexCount { get; set; }

        [Offset(6)]
        [StoreType(typeof(ushort))]
        public int FaceCount { get; set; }

        [Offset(20)]
        public byte Bones { get; set; }

        [Offset(56)]
        public DataPointer RawPointer { get; set; }

        [Offset(60)]
        public int RawSize { get; set; }

        [Offset(68)]
        public int DataSize { get; set; }

        [Offset(72)]
        public BlockCollection<SectionResourceBlock> Resources { get; set; }
    }

    [FixedSize(16)]
    public class SectionResourceBlock
    {
        [Offset(4)]
        public short Type0 { get; set; }

        [Offset(6)]
        public short Type1 { get; set; }

        [Offset(8)]
        public int Size { get; set; }

        [Offset(12)]
        public int Offset { get; set; }
    }

    [FixedSize(96)]
    public class NodeBlock : IGeometryNode
    {
        [Offset(0)]
        public StringId Name { get; set; }

        [Offset(4)]
        public short ParentIndex { get; set; }

        [Offset(6)]
        public short FirstChildIndex { get; set; }

        [Offset(8)]
        public short NextSiblingIndex { get; set; }

        [Offset(12)]
        public RealVector3D Position { get; set; }

        [Offset(24)]
        public RealVector4D Rotation { get; set; }

        [Offset(40)]
        public float TransformScale { get; set; }

        [Offset(44)]
        public Matrix4x4 TransformMatrix { get; set; }

        [Offset(92)]
        public float DistanceFromParent { get; set; }

        public override string ToString() => Name;

        #region INode

        string IGeometryNode.Name => Name;

        IRealVector3D IGeometryNode.Position => Position;

        IRealVector4D IGeometryNode.Rotation => Rotation;

        #endregion
    }

    [FixedSize(12)]
    public class MarkerGroupBlock : IGeometryMarkerGroup
    {
        [Offset(0)]
        public StringId Name { get; set; }

        [Offset(4)]
        public BlockCollection<MarkerBlock> Markers { get; set; }

        public override string ToString() => Name;

        #region IGeometryMarkerGroup

        string IGeometryMarkerGroup.Name => Name;

        IReadOnlyList<IGeometryMarker> IGeometryMarkerGroup.Markers => Markers;

        #endregion
    }

    [FixedSize(36)]
    public class MarkerBlock : IGeometryMarker
    {
        [Offset(0)]
        public byte RegionIndex { get; set; }

        [Offset(1)]
        public byte PermutationIndex { get; set; }

        [Offset(2)]
        public byte NodeIndex { get; set; }

        [Offset(4)]
        public RealVector3D Position { get; set; }

        [Offset(16)]
        public RealVector4D Rotation { get; set; }

        [Offset(32)]
        public float Scale { get; set; }

        #region IGeometryMarker

        IRealVector3D IGeometryMarker.Position => Position;

        IRealVector4D IGeometryMarker.Rotation => Rotation;

        #endregion
    }

    [FixedSize(32)]
    public class ShaderBlock
    {
        [Offset(12)]
        public TagReference ShaderReference { get; set; }
    }
}
