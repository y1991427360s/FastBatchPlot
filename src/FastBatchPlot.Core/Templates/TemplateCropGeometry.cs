using System;
using System.Linq;
using FastBatchPlot.Core.Common;

namespace FastBatchPlot.Core.Templates
{
    public static class TemplateCropGeometry
    {
        public static TemplateRegion Copy(TemplateRegion r)=>new TemplateRegion{X1=r.X1,Y1=r.Y1,X2=r.X2,Y2=r.Y2};
        public static void Validate(TemplateRegion r)
        {
            if(r==null||!new[]{r.X1,r.Y1,r.X2,r.Y2}.All(TitleTemplateService.Finite)||r.X2<=r.X1||r.Y2<=r.Y1)
                throw new ArgumentException("打印范围坐标须有限且宽高大于零。");
        }
        // 四角均已转换至WCS；不把斜矩形扩大为包围盒冒充精确裁切。
        public static Rect2D FromCorners(Point2D a,Point2D b,Point2D c,Point2D d)
        {
            var points=new[]{a,b,c,d};
            if(points.Any(p=>!TitleTemplateService.Finite(p.X)||!TitleTemplateService.Finite(p.Y)))throw new ArgumentException("裁切坐标无效。");
            double ax=b.X-a.X,ay=b.Y-a.Y,bx=d.X-a.X,by=d.Y-a.Y;
            double lengthA=Math.Sqrt(ax*ax+ay*ay),lengthB=Math.Sqrt(bx*bx+by*by);
            if(lengthA<=1e-9||lengthB<=1e-9||!TitleTemplateService.Finite(lengthA+lengthB))throw new ArgumentException("裁切范围退化。");
            double eps=Math.Max(lengthA,lengthB)*1e-8;
            bool aligned=(Math.Abs(ay)<=eps&&Math.Abs(bx)<=eps)||(Math.Abs(ax)<=eps&&Math.Abs(by)<=eps);
            if(!aligned||Math.Abs(c.X-(a.X+ax+bx))>eps||Math.Abs(c.Y-(a.Y+ay+by))>eps)
                throw new NotSupportedException("当前打印仅支持与WCS坐标轴平行的模板裁切范围，请旋转图框后再应用。");
            return new Rect2D(points.Min(p=>p.X),points.Min(p=>p.Y),points.Max(p=>p.X),points.Max(p=>p.Y));
        }
        public static bool Same(Rect2D a,Rect2D b)=>Math.Abs(a.MinX-b.MinX)<1e-6&&Math.Abs(a.MinY-b.MinY)<1e-6&&Math.Abs(a.MaxX-b.MaxX)<1e-6&&Math.Abs(a.MaxY-b.MaxY)<1e-6;
    }
}
