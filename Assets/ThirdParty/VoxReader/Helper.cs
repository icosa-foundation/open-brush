using System;
using System.Collections.Generic;
using System.Linq;
using VoxReader.Exceptions;
using VoxReader.Interfaces;

namespace VoxReader
{
    internal static class Helper
    {
        internal static char[] GetCharArray(byte[] data, int startIndex, int length)
        {
            var array = new char[length];

            for (int i = 0; i < array.Length; i++)
            {
                array[i] = (char)data[i + startIndex];
            }

            return array;
        }

        public static IEnumerable<IModel> ExtractModels(IChunk mainChunk, IPalette palette)
        {
            var sizeChunks = mainChunk.GetChildren<ISizeChunk>();
            var voxelChunks = mainChunk.GetChildren<IVoxelChunk>();

            if (sizeChunks.Length != voxelChunks.Length)
                throw new InvalidDataException("Can not extract models, because the number of SIZE chunks does not match the number of XYZI chunks!");

            var shapeNodeChunks = mainChunk.GetChildren<IShapeNodeChunk>();
            var transformNodeChunks = mainChunk.GetChildren<ITransformNodeChunk>();
            var groupNodeChunks = mainChunk.GetChildren<IGroupNodeChunk>();

            var transformNodesThatHaveAShapeNode = new Dictionary<ITransformNodeChunk, IShapeNodeChunk>();
            foreach (ITransformNodeChunk transformNodeChunk in transformNodeChunks)
            {
                foreach (IShapeNodeChunk shapeNodeChunk in shapeNodeChunks)
                {
                    if (transformNodeChunk.ChildNodeId != shapeNodeChunk.NodeId)
                        continue;

                    transformNodesThatHaveAShapeNode.Add(transformNodeChunk, shapeNodeChunk);
                    break;
                }
            }

            // Create inverse index map for mapped color index on voxel
            var indexMapChunk = mainChunk.GetChild<IIndexMapChunk>();
            var inverseIndexMap = new Dictionary<int, int>();
            for (int i = 0; i < palette.RawColors.Length - 1; i++)
            {
                inverseIndexMap.Add(GetMappedColorIndex(indexMapChunk, i), i);
            }

            if (mainChunk.Children.Length == 3) // If a vox file was exported as .vox instead of saved as a .vox project it only contains a size, voxel and palette chunk
            {
                Vector3 size = sizeChunks[0].Size;
                var voxels = voxelChunks[0].Voxels.Select(voxel => new Voxel(voxel.Position, voxel.Position, palette.RawColors[voxel.ColorIndex - 1], inverseIndexMap[voxel.ColorIndex - 1])).ToArray();
                yield return new Model(0, null, voxels, false, new Vector3(), new Vector3(), Matrix3.Identity, Matrix3.Identity, size);
                yield break;
            }

            var processedModelIds = new HashSet<int>();

            foreach (var keyValuePair in transformNodesThatHaveAShapeNode)
            {
                ITransformNodeChunk transformNodeChunk = keyValuePair.Key;
                IShapeNodeChunk shapeNodeChunk = keyValuePair.Value;

                int[] ids = shapeNodeChunk.Models;

                foreach (int id in ids)
                {
                    string name = transformNodeChunk.Name;
                    Vector3 localSize = sizeChunks[id].Size;
                    Vector3 globalPos = GetGlobalTranslation(transformNodeChunk);
                    Matrix3 globalRotation = GetGlobalRotation(transformNodeChunk);

                    var voxels = voxelChunks[id].Voxels.Select(voxel =>
                    {
                        return new Voxel(
                            voxel.Position,
                            ApplyTransformationToVoxel(voxel.Position, globalPos, globalRotation, localSize),
                            palette.RawColors[voxel.ColorIndex - 1], inverseIndexMap[voxel.ColorIndex - 1]);
                    }).ToArray();

                    Vector3 min = ApplyTransformationToVoxel(new Vector3(0, 0, 0), globalPos, globalRotation, localSize);
                    Vector3 max = ApplyTransformationToVoxel(localSize - new Vector3(1, 1, 1), globalPos, globalRotation, localSize);

                    Vector3 globalMin = new Vector3(Math.Min(max.X, min.X), Math.Min(max.Y, min.Y), Math.Min(max.Z, min.Z));
                    Vector3 globalMax = new Vector3(Math.Max(max.X, min.X), Math.Max(max.Y, min.Y), Math.Max(max.Z, min.Z));

                    globalPos = globalMin + (globalMax - globalMin + new Vector3(1, 1, 1)) / 2;

                    // Create new model
                    var model = new Model(id, name, voxels, !processedModelIds.Add(id),
                        globalPos,
                        transformNodeChunk.Frames[0].Translation,
                        globalRotation,
                        transformNodeChunk.Frames[0].Rotation,
                        sizeChunks[id].Size);
                    yield return model;
                }
            }

            Vector3 GetGlobalTranslation(ITransformNodeChunk target)
            {
                Vector3 position = target.Frames[0].Translation;
                while (TryGetParentTransformNodeChunk(target, out ITransformNodeChunk parent))
                {
                    position = parent.Frames[0].Rotation * position + parent.Frames[0].Translation;
                    target = parent;
                }

                return position;
            }

            Matrix3 GetGlobalRotation(ITransformNodeChunk target)
            {
                Matrix3 rotation = target.Frames[0].Rotation;
                while (TryGetParentTransformNodeChunk(target, out ITransformNodeChunk parent))
                {
                    rotation = parent.Frames[0].Rotation * rotation;
                    target = parent;
                }

                return rotation;
            }

            Vector3 ApplyTransformationToVoxel(Vector3 voxelPos, Vector3 globalPivot, Matrix3 globalRot, Vector3 size)
            {
                return globalPivot + globalRot.RotateIndex(voxelPos - size / 2);
            }

            bool TryGetParentTransformNodeChunk(ITransformNodeChunk target, out ITransformNodeChunk parent)
            {
                //TODO: performance here is questionable; might need an additional scene structure to query the parent efficiently
                foreach (IGroupNodeChunk groupNodeChunk in groupNodeChunks)
                {
                    foreach (int parentGroupNodeChunkChildId in groupNodeChunk.ChildrenNodes)
                    {
                        if (parentGroupNodeChunkChildId != target.NodeId)
                            continue;

                        foreach (ITransformNodeChunk transformNodeChunk in transformNodeChunks)
                        {
                            if (transformNodeChunk.ChildNodeId != groupNodeChunk.NodeId)
                                continue;

                            parent = transformNodeChunk;
                            return true;
                        }
                    }
                }

                parent = null;
                return false;
            }
        }

        internal static int GetMappedColorIndex(IIndexMapChunk indexMapChunk, int rawIndex)
        {
            if (indexMapChunk == null)
                return rawIndex;

            return indexMapChunk.ColorIndices[rawIndex] - 1;
        }
    }
}