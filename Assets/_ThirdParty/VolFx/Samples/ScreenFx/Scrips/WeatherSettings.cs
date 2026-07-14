using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

//  ScreenFx © NullTale - https://x.com/NullTale
namespace ScreenFx
{
    //#pragma warning disable CS0618
    //[VolumeComponentMenuForRenderPipeline("VolFx/Weather (ScreenFxSample, Delete Me)", typeof(UniversalRenderPipeline))]
    [Serializable, VolumeComponentMenu("VolFx/")]
    internal class WeatherSettings : VolumeComponent
    {
        public ClampedFloatParameter _snow   = new ClampedFloatParameter(0, 0, 1);
        public ClampedFloatParameter _sun    = new ClampedFloatParameter(0, 0, 1);
        public ClampedFloatParameter _clouds = new ClampedFloatParameter(0, 0, 1);
    }
}