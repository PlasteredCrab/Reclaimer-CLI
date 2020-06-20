﻿using Adjutant.Blam.Halo5;
using Adjutant.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Endian;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Reclaimer.Plugins.MetaViewer.Halo5
{
    public class StructureValue : MetaValue
    {
        public int BlockSize { get; }

        private int blockCount;
        public int BlockCount
        {
            get { return blockCount; }
            set { SetProperty(ref blockCount, value); }
        }

        private long blockAddress;
        public long BlockAddress
        {
            get { return blockAddress; }
            set { SetProperty(ref blockAddress, value); }
        }

        private int blockIndex;
        public int BlockIndex
        {
            get { return blockIndex; }
            set
            {
                value = Math.Min(Math.Max(0, value), BlockCount - 1);

                if (SetProperty(ref blockIndex, value))
                    RefreshChildren();
            }
        }

        private IEnumerable<string> blockLabels;
        public IEnumerable<string> BlockLabels
        {
            get { return blockLabels; }
            set { SetProperty(ref blockLabels, value); }
        }

        private bool isExpanded;
        public bool IsExpanded
        {
            get { return isExpanded; }
            set { SetProperty(ref isExpanded, value); }
        }

        public bool HasChildren => Children.Any();
        public ObservableCollection<MetaValue> Children { get; }

        public StructureValue(XmlNode node, ModuleItem item, MetadataHeader header, EndianReader reader, long baseAddress, int offset)
            : base(node, item, header, reader, baseAddress, offset)
        {
            BlockSize = node.ChildNodes.OfType<XmlNode>().Sum(n => CalculateSize(n));
            Children = new ObservableCollection<MetaValue>();
            IsExpanded = true;
            ReadValue(reader);
        }

        public override void ReadValue(EndianReader reader)
        {
            IsEnabled = true;

            try
            {
                Children.Clear();
                reader.Seek(ValueAddress + 16, SeekOrigin.Begin);

                BlockCount = reader.ReadInt32();

                if (BlockCount <= 0 || BlockAddress + BlockCount * BlockSize > reader.BaseStream.Length)
                {
                    IsEnabled = false;
                    return;
                }

                var currentBlock = header.DataBlocks.First(b => header.GetSectionOffset(b.Section) + b.Offset - header.Header.HeaderSize == BaseAddress);
                var structdef = header.StructureDefinitions.First(s => s.FieldBlock == header.DataBlocks.IndexOf(currentBlock) && s.FieldOffset == Offset);

                var block = header.DataBlocks[structdef.TargetIndex];
                BlockAddress = header.GetSectionOffset(block.Section) + block.Offset - header.Header.HeaderSize;

                blockIndex = 0;
                var offset = 0;
                foreach (XmlNode n in node.ChildNodes)
                {
                    Children.Add(MetaValue.GetValue(n, item, header, reader, BlockAddress, offset));
                    offset += CalculateSize(n);
                }

                RaisePropertyChanged(nameof(BlockIndex));
                RaisePropertyChanged(nameof(HasChildren));

                var entryOffset = GetIntAttribute(node, "entryName", "entryOffset", "label");
                var entry = Children.FirstOrDefault(c => c.Offset == entryOffset);

                BlockLabels = Enumerable.Range(0, Math.Min(BlockCount, 100)).Select(i => $"Block {i:D2}");
            }
            catch { IsEnabled = false; }
        }

        public override void WriteValue(EndianWriter writer)
        {
            throw new NotImplementedException();
        }

        private void RefreshChildren()
        {
            if (BlockCount <= 0)
                return;

            using (var itemReader = item.CreateReader())
            using (var reader = itemReader.CreateVirtualReader(header.Header.HeaderSize))
            {
                foreach (var c in Children)
                {
                    c.BaseAddress = BlockAddress + BlockIndex * BlockSize;
                    c.ReadValue(reader);
                }
            }
        }
    }
}
