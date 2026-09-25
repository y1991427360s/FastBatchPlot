using System;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Planning;

namespace FastBatchPlot.Core.Assets
{
    /// <summary>不可变 WCS 图片位置，U/V 表示整张图片边向量，含图框旋转和镜像。</summary>
    public sealed class StampPlacement
    {
        public double X {get;} public double Y {get;} public double Z {get;}
        public double Ux {get;} public double Uy {get;} public double Vx {get;} public double Vy {get;}
        private readonly double regionX,regionY,regionUx,regionUy,regionVx,regionVy;
        private StampPlacement(double x,double y,double z,double ux,double uy,double vx,double vy,
            double rx,double ry,double rux,double ruy,double rvx,double rvy)
        {X=x;Y=y;Z=z;Ux=ux;Uy=uy;Vx=vx;Vy=vy;regionX=rx;regionY=ry;regionUx=rux;regionUy=ruy;regionVx=rvx;regionVy=rvy;}
        public static StampPlacement Fit(double x,double y,double z,double ux,double uy,double vx,double vy,int pixelWidth,int pixelHeight)
        {
            foreach(var n in new[]{x,y,z,ux,uy,vx,vy}) if(double.IsNaN(n)||double.IsInfinity(n)) throw new ArgumentException("印章区域坐标无效。");
            double w=Math.Sqrt(ux*ux+uy*uy), h=Math.Sqrt(vx*vx+vy*vy);
            if(w<1e-9||h<1e-9||double.IsInfinity(w)||double.IsInfinity(h)||pixelWidth<1||pixelHeight<1)
                throw new ArgumentException("印章区域和像素尺寸须大于零。");
            if(Math.Abs(ux/w*vx/h+uy/w*vy/h)>1e-8) throw new NotSupportedException("印章区域发生剪切，不能保持图片比例。");
            double factor=Math.Min(w/pixelWidth,h/pixelHeight);
            return Place(x,y,z,ux,uy,vx,vy,factor*pixelWidth,factor*pixelHeight);
        }
        /// <summary>固定尺寸按最终打印比例换算，负留白调整比例后仍保持指定纸面毫米数。</summary>
        public StampPlacement ForOutput(StampAsset asset,PlotPlan plan)
        {
            var details=asset.Details;details?.Validate();
            if(details==null||details.Sizing==StampSizing.FitRegion)return this;
            if(double.IsNaN(plan.ScaleDenominator)||double.IsInfinity(plan.ScaleDenominator)||plan.ScaleDenominator<=0)
                throw new ArgumentException("印章定位需要有效的最终打印比例。");
            return Place(regionX,regionY,Z,regionUx,regionUy,regionVx,regionVy,
                details.WidthMm*plan.ScaleDenominator,details.HeightMm*plan.ScaleDenominator);
        }
        private static StampPlacement Place(double x,double y,double z,double ux,double uy,double vx,double vy,double width,double height)
        {
            double w=Math.Sqrt(ux*ux+uy*uy),h=Math.Sqrt(vx*vx+vy*vy);
            if(double.IsNaN(width)||double.IsNaN(height)||double.IsInfinity(width)||double.IsInfinity(height)||width<=0||height<=0)
                throw new ArgumentException("印章尺寸超出有效范围。");
            if(width>w+1e-7||height>h+1e-7)
                throw new InvalidOperationException("指定的印章纸面尺寸超出模板印章区域，未自动缩小；请扩大区域或调整尺寸。");
            double a=width/w,b=height/h;
            var placed=new StampPlacement(x+(1-a)*ux/2+(1-b)*vx/2,y+(1-a)*uy/2+(1-b)*vy/2,z,
                a*ux,a*uy,b*vx,b*vy,x,y,ux,uy,vx,vy);
            foreach(var n in new[]{placed.X,placed.Y,placed.Ux,placed.Uy,placed.Vx,placed.Vy})
                if(double.IsNaN(n)||double.IsInfinity(n)) throw new ArgumentException("印章定位超出有效范围。");
            var bounds=placed.Bounds;
            if(bounds.Width<=0 || bounds.Height<=0 || double.IsInfinity(bounds.MinX)||double.IsInfinity(bounds.MinY)||double.IsInfinity(bounds.MaxX)||double.IsInfinity(bounds.MaxY))
                throw new ArgumentException("印章范围小于当前坐标精度或超出有效数值范围。");
            return placed;
        }
        public Rect2D Bounds
        {
            get {return new Rect2D(X+Math.Min(0,Ux)+Math.Min(0,Vx),Y+Math.Min(0,Uy)+Math.Min(0,Vy),
                X+Math.Max(0,Ux)+Math.Max(0,Vx),Y+Math.Max(0,Uy)+Math.Max(0,Vy));}
        }
        public void EnsureWithin(PlotPlan plan)
        {
            var b=Bounds;
            if(b.MinX<plan.MinX-1e-7||b.MinY<plan.MinY-1e-7||b.MaxX>plan.MaxX+1e-7||b.MaxY>plan.MaxY+1e-7)
                throw new InvalidOperationException("印章超出图纸打印范围，请调整模板印章区域或打印范围。");
        }
        public bool Same(StampPlacement other)
        {
            var a=new[]{X,Y,Z,Ux,Uy,Vx,Vy}; var b=new[]{other.X,other.Y,other.Z,other.Ux,other.Uy,other.Vx,other.Vy};
            for(int i=0;i<a.Length;i++) if(Math.Abs(a[i]-b[i])>1e-7) return false;
            return true;
        }
    }
}
