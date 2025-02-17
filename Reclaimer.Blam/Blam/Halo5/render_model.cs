﻿using Adjutant.Geometry;
using Adjutant.Spatial;
using Reclaimer.Blam.Utilities;
using Reclaimer.Geometry;
using Reclaimer.Geometry.Vectors;
using Reclaimer.IO;
using System.Numerics;

namespace Reclaimer.Blam.Halo5
{
    public class render_model : ContentTagDefinition, IRenderGeometry
    {
        public render_model(ModuleItem item, MetadataHeader header)
            : base(item, header)
        { }

        [Offset(32)]
        public BlockCollection<RegionBlock> Regions { get; set; }

        [Offset(60)]
        public int InstancedGeometrySectionIndex { get; set; }

        [Offset(64)]
        public BlockCollection<GeometryInstanceBlock> GeometryInstances { get; set; }

        [Offset(96)]
        public BlockCollection<NodeBlock> Nodes { get; set; }

        [Offset(152)]
        public BlockCollection<MarkerGroupBlock> MarkerGroups { get; set; }

        [Offset(180)]
        public BlockCollection<MaterialBlock> Materials { get; set; }

        [Offset(272)]
        public BlockCollection<SectionBlock> Sections { get; set; }

        [Offset(328)]
        public BlockCollection<BoundingBoxBlock> BoundingBoxes { get; set; }

        [Offset(356)]
        public BlockCollection<NodeMapBlock> NodeMaps { get; set; }

        public override string ToString() => Item.FileName;

        #region IRenderGeometry

        int IRenderGeometry.LodCount => Sections.Max(s => s.SectionLods.Count);

        public IGeometryModel ReadGeometry(int lod)
        {
            Exceptions.ThrowIfIndexOutOfRange(lod, ((IRenderGeometry)this).LodCount);

            var model = new GeometryModel(Item.FileName) { CoordinateSystem = CoordinateSystem.Default };

            model.Nodes.AddRange(Nodes);
            model.MarkerGroups.AddRange(MarkerGroups);
            model.Bounds.AddRange(BoundingBoxes);
            model.Materials.AddRange(Halo5Common.GetMaterials(Materials));

            foreach (var region in Regions)
            {
                var gRegion = new GeometryRegion { Name = region.Name };
                gRegion.Permutations.AddRange(region.Permutations.Where(p => p.SectionIndex >= 0).Select(p =>
                    new GeometryPermutation
                    {
                        Name = p.Name,
                        MeshIndex = p.SectionIndex,
                        MeshCount = p.SectionCount
                    }));

                if (gRegion.Permutations.Any())
                    model.Regions.Add(gRegion);
            }

            Func<int, int, int> mapNodeFunc = null;
            model.Meshes.AddRange(Halo5Common.GetMeshes(Module, Item, Sections, lod, s => 0, mapNodeFunc));

            CreateInstanceMeshes(model, lod);

            return model;
        }

        private void CreateInstanceMeshes(GeometryModel model, int lod)
        {
            if (InstancedGeometrySectionIndex < 0)
                return;

            /* 
             * The render_model geometry instances have all their mesh data
             * in the same section and each instance has its own subset.
             * This function separates the subsets into separate sections
             * to make things easier for the model rendering and exporting 
             */

            var gRegion = new GeometryRegion { Name = "Instances" };
            gRegion.Permutations.AddRange(GeometryInstances.Select(i =>
                new GeometryPermutation
                {
                    Name = i.Name,
                    Transform = i.Transform,
                    TransformScale = i.TransformScale,
                    MeshIndex = InstancedGeometrySectionIndex + GeometryInstances.IndexOf(i),
                    MeshCount = 1
                }));

            model.Regions.Add(gRegion);

            var sourceMesh = model.Meshes[InstancedGeometrySectionIndex];
            model.Meshes.Remove(sourceMesh);

            var section = Sections[InstancedGeometrySectionIndex];
            var localLod = Math.Min(lod, section.SectionLods.Count - 1);
            for (var i = 0; i < GeometryInstances.Count; i++)
            {
                var subset = section.SectionLods[localLod].Subsets[i];
                var mesh = new GeometryMesh
                {
                    NodeIndex = (byte)GeometryInstances[i].NodeIndex,
                    BoundsIndex = 0
                };

                var strip = sourceMesh.IndexBuffer.GetSubset(subset.IndexStart, subset.IndexLength);

                var min = strip.Min();
                var max = strip.Max();
                var len = max - min + 1;

                mesh.IndexBuffer = IndexBuffer.FromCollection(strip.Select(j => j - min), sourceMesh.IndexFormat);
                mesh.VertexBuffer = sourceMesh.VertexBuffer.Slice(min, len);

                var submesh = section.SectionLods[localLod].Submeshes[subset.SubmeshIndex];
                mesh.Submeshes.Add(new GeometrySubmesh
                {
                    MaterialIndex = submesh.ShaderIndex,
                    IndexStart = 0,
                    IndexLength = mesh.IndexBuffer.Count
                });

                model.Meshes.Add(mesh);
            }
        }

        public IEnumerable<IBitmap> GetAllBitmaps() => Halo5Common.GetBitmaps(Materials);

        public IEnumerable<IBitmap> GetBitmaps(IEnumerable<int> shaderIndexes) => Halo5Common.GetBitmaps(Materials, shaderIndexes);

        #endregion
    }

    [FixedSize(80)]
    public struct VertexBufferInfo
    {
        [Offset(4)]
        public int VertexCount { get; set; }
    }

    [FixedSize(72)]
    public struct IndexBufferInfo
    {
        [Offset(4)]
        public int IndexCount { get; set; }
    }

    [FixedSize(32)]
    [DebuggerDisplay($"{{{nameof(Name)},nq}}")]
    public class RegionBlock
    {
        [Offset(0)]
        public StringHash Name { get; set; }

        [Offset(4)]
        public BlockCollection<PermutationBlock> Permutations { get; set; }
    }

    [FixedSize(28)]
    [DebuggerDisplay($"{{{nameof(Name)},nq}}")]
    public class PermutationBlock
    {
        [Offset(0)]
        public StringHash Name { get; set; }

        [Offset(4)]
        public short SectionIndex { get; set; }

        [Offset(6)]
        public short SectionCount { get; set; }
    }

    [FixedSize(60)]
    [DebuggerDisplay($"{{{nameof(Name)},nq}}")]
    public class GeometryInstanceBlock
    {
        [Offset(0)]
        public StringHash Name { get; set; }

        [Offset(4)]
        public int NodeIndex { get; set; }

        [Offset(8)]
        public float TransformScale { get; set; }

        [Offset(12)]
        public Matrix4x4 Transform { get; set; }
    }

    [FixedSize(124)]
    [DebuggerDisplay($"{{{nameof(Name)},nq}}")]
    public class NodeBlock : IGeometryNode
    {
        [Offset(0)]
        public StringHash Name { get; set; }

        [Offset(4)]
        public short ParentIndex { get; set; }

        [Offset(6)]
        public short FirstChildIndex { get; set; }

        [Offset(8)]
        public short NextSiblingIndex { get; set; }

        [Offset(12)]
        public RealVector3 Position { get; set; }

        [Offset(24)]
        public RealVector4 Rotation { get; set; }

        [Offset(40)]
        public Matrix4x4 Transform { get; set; }

        [Offset(88)]
        public float TransformScale { get; set; }

        [Offset(92)]
        public float DistanceFromParent { get; set; }

        #region IGeometryNode

        string IGeometryNode.Name => Name;

        IVector3 IGeometryNode.Position => Position;

        IVector4 IGeometryNode.Rotation => Rotation;

        Matrix4x4 IGeometryNode.OffsetTransform => Transform;

        #endregion
    }

    [FixedSize(32)]
    [DebuggerDisplay($"{{{nameof(Name)},nq}}")]
    public class MarkerGroupBlock : IGeometryMarkerGroup
    {
        [Offset(0)]
        public StringHash Name { get; set; }

        [Offset(4)]
        public BlockCollection<MarkerBlock> Markers { get; set; }

        #region IGeometryMarkerGroup

        string IGeometryMarkerGroup.Name => Name;

        IReadOnlyList<IGeometryMarker> IGeometryMarkerGroup.Markers => Markers;

        #endregion
    }

    [FixedSize(56)]
    public class MarkerBlock : IGeometryMarker
    {
        [Offset(0)]
        public byte RegionIndex { get; set; }

        [Offset(4)]
        public int PermutationIndex { get; set; }

        [Offset(8)]
        public byte NodeIndex { get; set; }

        [Offset(12)]
        public RealVector3 Position { get; set; }

        [Offset(24)]
        public RealVector4 Rotation { get; set; }

        [Offset(40)]
        public float Scale { get; set; }

        [Offset(44)]
        public RealVector3 Direction { get; set; }

        public override string ToString() => Position.ToString();

        #region IGeometryMarker

        byte IGeometryMarker.PermutationIndex => (byte)PermutationIndex;

        IVector3 IGeometryMarker.Position => Position;

        IVector4 IGeometryMarker.Rotation => Rotation;

        #endregion
    }

    [FixedSize(32)]
    [DebuggerDisplay($"{{{nameof(MaterialReference)},nq}}")]
    public class MaterialBlock
    {
        [Offset(0)]
        public TagReference MaterialReference { get; set; }
    }

    [FixedSize(128)]
    public class SectionBlock
    {
        [Offset(0)]
        public BlockCollection<SectionLodBlock> SectionLods { get; set; }

        [Offset(30)]
        public byte NodeIndex { get; set; }

        [Offset(31)]
        public byte VertexFormat { get; set; }

        [Offset(33)]
        [StoreType(typeof(byte))]
        public IndexFormat IndexFormat { get; set; }
    }

    [FixedSize(140)]
    public class SectionLodBlock
    {
        [Offset(56)]
        public BlockCollection<SubmeshBlock> Submeshes { get; set; }

        [Offset(84)]
        public BlockCollection<SubsetBlock> Subsets { get; set; }

        [Offset(112)]
        public short VertexBufferIndex { get; set; }

        [Offset(134)]
        public short IndexBufferIndex { get; set; }
    }

    [FixedSize(24)]
    public class SubmeshBlock
    {
        [Offset(0)]
        public short ShaderIndex { get; set; }

        [Offset(4)]
        public int IndexStart { get; set; }

        [Offset(8)]
        public int IndexLength { get; set; }

        [Offset(12)]
        public ushort SubsetIndex { get; set; }

        [Offset(14)]
        public ushort SubsetCount { get; set; }

        [Offset(20)]
        public ushort VertexCount { get; set; }
    }

    [FixedSize(16)]
    public class SubsetBlock
    {
        [Offset(0)]
        public int IndexStart { get; set; }

        [Offset(4)]
        public int IndexLength { get; set; }

        [Offset(8)]
        public ushort SubmeshIndex { get; set; }

        [Offset(10)]
        public ushort VertexCount { get; set; }
    }

    [FixedSize(52)]
    public class BoundingBoxBlock : IRealBounds5D
    {
        //short flags, short padding

        [Offset(4)]
        public RealBounds XBounds { get; set; }

        [Offset(12)]
        public RealBounds YBounds { get; set; }

        [Offset(20)]
        public RealBounds ZBounds { get; set; }

        [Offset(28)]
        public RealBounds UBounds { get; set; }

        [Offset(36)]
        public RealBounds VBounds { get; set; }
    }

    [FixedSize(28)]
    public class NodeMapBlock
    {
        [Offset(0)]
        public BlockCollection<byte> Indices { get; set; }
    }
}
