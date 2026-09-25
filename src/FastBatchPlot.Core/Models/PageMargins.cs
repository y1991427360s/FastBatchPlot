using System;
using System.Runtime.Serialization;

namespace FastBatchPlot.Core.Models
{
    public enum MarginMode { Uniform=0, ExpandPaper=1, ShrinkContent=2 }
    [DataContract]
    public sealed class PageMargins
    {
        [DataMember] public MarginMode Mode {get;set;}
        [DataMember] public double Left {get;set;}
        [DataMember] public double Right {get;set;}
        [DataMember] public double Top {get;set;}
        [DataMember] public double Bottom {get;set;}
        public PageMargins Copy()=>new PageMargins{Mode=Mode,Left=Left,Right=Right,Top=Top,Bottom=Bottom};
        public void Validate()
        {
            if(!Enum.IsDefined(typeof(MarginMode),Mode))throw new ArgumentException("未知留白模式。");
            foreach(double v in new[]{Left,Right,Top,Bottom})
                if(double.IsNaN(v)||double.IsInfinity(v)||v<0||v>100)throw new ArgumentException("四边留白必须为 0 到 100 毫米。");
        }
    }
}
