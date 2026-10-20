// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace BotPlusAutocadTool.Services
{
    internal static class CopyService
    {
        internal static int Copy(Transaction transaction, BlockTableRecord destination, IEnumerable<Entity> sources, Vector3d offset)
        {
            var sourceIds = new ObjectIdCollection();
            foreach (var source in sources)
                if (!source.IsErased) sourceIds.Add(source.ObjectId);
            var mapping = new IdMapping();
            destination.Database.DeepCloneObjects(sourceIds, destination.ObjectId, mapping, false);
            var transform = Matrix3d.Displacement(offset);
            int count = 0;
            foreach (IdPair pair in mapping)
            {
                if (!pair.IsPrimary || !pair.IsCloned) continue;
                if (transaction.GetObject(pair.Value, OpenMode.ForWrite) is not Entity clone) continue;
                clone.TransformBy(transform);
                count++;
            }
            return count;
        }
    }
}
