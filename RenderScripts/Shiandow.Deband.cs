﻿using System;
using System.Collections.Generic;
using System.Drawing;

namespace Mpdn.RenderScript
{
    namespace Shiandow.Deband
    {
        public class Deband : RenderChain
        {
            private const float DEFAULT_THRESHOLD = 0.5f;
            private const float DEFAULT_MARGIN = 1.0f;

            public int maxbitdepth { get; set; }
            public float threshold { get; set; }
            public float margin { get; set; }
            public bool advancedMode { get; set; }

            public Deband()
            {
                maxbitdepth = 8;
                threshold = DEFAULT_THRESHOLD;
                margin = DEFAULT_MARGIN;
                advancedMode = false;
            }

            public override IFilter CreateFilter(IResizeableFilter sourceFilter)
            {
                var upscaler = new Scaler.Custom(new GaussianBlur(0.75), ScalerTaps.Four, false);
                var downscaler = new Scaler.HwBilinear(); // Good enough (?)

                int bits = 8;
                switch (Renderer.InputFormat)
                {
                    case FrameBufferInputFormat.P010: bits = 10; break;
                    case FrameBufferInputFormat.Y410: bits = 10; break;
                    case FrameBufferInputFormat.P016: bits = 16; break;
                    case FrameBufferInputFormat.Y416: bits = 16; break;
                    case FrameBufferInputFormat.Rgb24: return sourceFilter;
                    case FrameBufferInputFormat.Rgb32: return sourceFilter;
                }
                if (bits > maxbitdepth) return sourceFilter;

                var inputsize = sourceFilter.OutputSize;
                var size = inputsize;
                var current = sourceFilter.ConvertToYuv();

                var factor = 2.0;
                var downscaled = new Stack<IFilter>();
                downscaled.Push(current);

                // Generate downscaled images
                for (int i = 0; i < 8; i++)
                {
                    size = new Size((int)Math.Floor(size.Width / factor), (int)Math.Floor(size.Height / factor));
                    if (size.Width == 0 || size.Height == 0) break;

                    current = new ResizeFilter(current, size, upscaler, downscaler);
                    downscaled.Push(current);
                }

                var deband = downscaled.Pop();
                while (downscaled.Count > 0)
                {
                    deband = new ShaderFilter(CompileShader("Deband.hlsl"), true,
                        new[]
                        {
                            (1 << bits) - 1, 
                            advancedMode ? threshold : DEFAULT_THRESHOLD,
                            advancedMode ? margin : DEFAULT_MARGIN
                        }, 
                        downscaled.Pop(), deband);
                }

                return deband.ConvertToRgb();
            }

            private class GaussianBlur : ICustomLinearScaler
            {
                private double m_Sigma;

                public GaussianBlur(double sigma)
                {
                    m_Sigma = sigma;
                }

                public Guid Guid
                {
                    get { return Guid.Empty; }
                }

                public string Name
                {
                    get { return ""; }
                }

                public bool AllowDeRing
                {
                    get { return false; }
                }

                public ScalerTaps MaxTapCount
                {
                    get { return ScalerTaps.Eight; }
                }

                public float GetWeight(float n, int width)
                {
                    return (float)GaussianKernel(n);
                }

                private double GaussianKernel(double x)
                {
                    var sigma = m_Sigma;
                    return Math.Exp(-(x * x / (2 * sigma * sigma)));
                }
            }
        }

        public class DebandUi : ConfigurableRenderChainUi<Deband, DebandConfigDialog>
        {
            protected override string ConfigFileName
            {
                get { return "Shiandow.Deband"; }
            }

            protected override RenderScriptDescriptor ScriptDescriptor
            {
                get
                {
                    return new RenderScriptDescriptor
                    {
                        Guid = new Guid("EE3B46F7-00BB-4299-9B3F-058BCC3F591C"),
                        Name = "Deband",
                        Description = "Removes banding",
                        Copyright = "Deband by Shiandow",
                    };
                }
            }
        }
    }
}
