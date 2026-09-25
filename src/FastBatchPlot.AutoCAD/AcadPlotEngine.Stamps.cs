using System;
using FastBatchPlot.Core.Assets;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace FastBatchPlot.AutoCAD
{
    public partial class AcadPlotEngine
    {
        private static void InsertTemporaryStamp(Database db,Transaction tr,BlockTableRecord space,
            StampAsset stamp,StampPlacement position,string imagePath,out RasterImageDef? definition)
        {
            string token=Guid.NewGuid().ToString("N");
            var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForWrite);
            var layer=new LayerTableRecord {Name="FBP_TEMP_STAMP_"+token,IsPlottable=true,IsOff=false,IsFrozen=false,IsLocked=false};
            var layerId=layers.Add(layer);tr.AddNewlyCreatedDBObject(layer,true);
            var dictionaryId=RasterImageDef.GetImageDictionary(db);
            DBDictionary dictionary;
            if(dictionaryId.IsNull)
            {
                var nod=(DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForWrite);
                dictionary=new DBDictionary();nod.SetAt("ACAD_IMAGE_DICT",dictionary);tr.AddNewlyCreatedDBObject(dictionary,true);
            }
            else dictionary=(DBDictionary)tr.GetObject(dictionaryId,OpenMode.ForWrite);
            definition=new RasterImageDef();
            dictionary.SetAt("FBP_"+token,definition);tr.AddNewlyCreatedDBObject(definition,true);
            definition.SourceFileName=imagePath;definition.Load();
            if(!definition.IsLoaded || Math.Abs(definition.Size.X-stamp.PixelWidth)>0.01 || Math.Abs(definition.Size.Y-stamp.PixelHeight)>0.01)
                throw new InvalidOperationException("CAD 未完整加载印章 PNG 或返回的尺寸不一致。");
            using(var raster=new RasterImage())
            {
                raster.SetDatabaseDefaults(db);raster.LayerId=layerId;raster.ImageDefId=definition.ObjectId;
                raster.Visible=true;raster.Transparency=new Autodesk.AutoCAD.Colors.Transparency(255);
                raster.ShowImage=true;raster.ImageTransparency=true;raster.IsClipped=false;
                raster.Orientation=new CoordinateSystem3d(new Point3d(position.X,position.Y,position.Z),
                    new Vector3d(position.Ux,position.Uy,0),new Vector3d(position.Vx,position.Vy,0));
                var actual=raster.Orientation;
                if((actual.Origin-new Point3d(position.X,position.Y,position.Z)).Length>1e-7 ||
                    (actual.Xaxis-new Vector3d(position.Ux,position.Uy,0)).Length>1e-7 ||
                    (actual.Yaxis-new Vector3d(position.Vx,position.Vy,0)).Length>1e-7)
                    throw new InvalidOperationException("CAD 未接受印章定位或方向，请检查图框变换。");
                tr.GetObject(space.ObjectId,OpenMode.ForWrite);
                space.AppendEntity(raster);tr.AddNewlyCreatedDBObject(raster,true);
                var order=(DrawOrderTable)tr.GetObject(space.DrawOrderTableId,OpenMode.ForWrite);
                order.MoveToTop(new ObjectIdCollection(new[]{raster.ObjectId}));
            }
            // 不改全局 raster reactor 开关；一次打印后的全部对象都随事务回滚。

        }
    }
}
