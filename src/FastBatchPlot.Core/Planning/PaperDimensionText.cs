using System;
using System.Globalization;

namespace FastBatchPlot.Core.Planning
{
    public static class PaperDimensionText
    {
        public static bool TryParse(string text, out double width, out double height)
        {
            width=height=0;
            if(text==null)return false;
            string[] parts=text.Trim().Replace('x','×').Replace('X','×').Replace('*','×').Split('×');
            if(parts.Length!=2)return false;
            const NumberStyles style=NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingWhite|NumberStyles.AllowTrailingWhite;
            if(!double.TryParse(parts[0],style,CultureInfo.InvariantCulture,out width)||!double.TryParse(parts[1],style,CultureInfo.InvariantCulture,out height))return false;
            return Valid(width)&&Valid(height);
        }
        private static bool Valid(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value)&&value>=1&&value<=100000&&Math.Abs(value-Math.Round(value,3))<1e-8;
        public static string Format(double width,double height)=>width.ToString("0.###",CultureInfo.InvariantCulture)+" × "+height.ToString("0.###",CultureInfo.InvariantCulture);
    }
}
