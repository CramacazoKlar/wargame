using System;
namespace CampaignDemo
{
    // Shared mapping for mouse wheel, slider and reset. No Unity dependencies.
    public struct MapZoomRange
    {
        public readonly float MinimumSize,MaximumSize;
        public float MiddleSize { get { return MinimumSize+(MaximumSize-MinimumSize)*0.5f; } }
        public MapZoomRange(float minimum,float maximum)
        {
            if(float.IsNaN(minimum)||float.IsInfinity(minimum)) minimum=2;
            if(float.IsNaN(maximum)||float.IsInfinity(maximum)) maximum=14;
            MinimumSize=Math.Max(0.01f,minimum); MaximumSize=Math.Max(MinimumSize,maximum);
        }
        public float ClampSize(float size)
        { if(float.IsNaN(size)||float.IsInfinity(size)) return MiddleSize; return Math.Max(MinimumSize,Math.Min(MaximumSize,size)); }
        // Slider 0 = zoomed out; 1 = zoomed in.
        public float SizeFromSlider(float value)
        {
            if(float.IsNaN(value)||float.IsInfinity(value)) value=0.5f;
            value=Math.Max(0,Math.Min(1,value)); return MaximumSize-(MaximumSize-MinimumSize)*value;
        }
        public float SliderFromSize(float size)
        { float width=MaximumSize-MinimumSize; return width<=0?0.5f:(MaximumSize-ClampSize(size))/width; }
        public float Scroll(float size,float wheelDelta,float step)
        {
            if(float.IsNaN(wheelDelta)||float.IsInfinity(wheelDelta)) return ClampSize(size);
            if(float.IsNaN(step)||float.IsInfinity(step)) step=0.5f;
            return ClampSize(size-wheelDelta*Math.Max(0,step));
        }
    }
}
