namespace RTCV.CorruptCore
{
    using System;
    using System.Linq;
    using System.Windows.Forms;
    using RTCV.Common.CustomExtensions;
    using RTCV.NetCore;
    using BlastRadiusAlgorithm = System.Func<RTCV.CorruptCore.BlastInfo, RTCV.CorruptCore.BlastLayer>;

    public static class BlastRadius
    {
        public static BlastRadiusAlgorithm GetFromString(string str)
        {
            return str switch
            {
                "SPREAD" => Spread,
                "CHUNK" => Chunk,
                "BURST" => Burst,
                "NORMALIZED" => Normalized,
                "PROPORTIONAL" => Proportional,
                "EVEN" => Even,
                _ => null
            };
        }

        public static string ToString(this BlastRadiusAlgorithm algorithm)
        {
            if (algorithm == Spread) {
                return "SPREAD";
            } else if (algorithm == Chunk) {
                return "CHUNK";
            } else if (algorithm == Burst) {
                return "BURST";
            } else if (algorithm == Normalized) {
                return "NORMALIZED";
            } else if (algorithm == Proportional) {
                return "PROPORTIONAL";
            } else if (algorithm == Even) {
                return "EVEN";
            }

            return "NONE";
        }

        private static BlastUnit[] GetBlastUnits(string domain, long address, int precision, int alignment, CorruptionEngine engine)
        {
            try
            {
                //Will generate a blast unit depending on which Corruption Engine is currently set.
                //Some engines like Distortion may not return an Unit depending on the current state on things.

                BlastUnit[] bus = null;
                BlastUnit bu = null;

                switch (engine)
                {
                    case CorruptionEngine.NIGHTMARE:
                        bu = RTC_NightmareEngine.GenerateUnit(domain, address, precision, alignment);
                        break;
                    case CorruptionEngine.HELLGENIE:
                        bu = RTC_HellgenieEngine.GenerateUnit(domain, address, precision, alignment);
                        break;
                    case CorruptionEngine.DISTORTION:
                        bu = RTC_DistortionEngine.GenerateUnit(domain, address, precision, alignment);
                        break;
                    case CorruptionEngine.FREEZE:
                        bu = RTC_FreezeEngine.GenerateUnit(domain, address, precision, alignment);
                        break;
                    case CorruptionEngine.PIPE:
                        bu = RTC_PipeEngine.GenerateUnit(domain, address, precision, alignment);
                        break;
                    case CorruptionEngine.VECTOR:
                        bu = RTC_VectorEngine.GenerateUnit(domain, address, alignment);
                        break;
                    case CorruptionEngine.CLUSTER:
                        bus = RTC_ClusterEngine.GenerateUnit(domain, address, alignment);
                        break;
                    case CorruptionEngine.CUSTOM:
                        bu = RTC_CustomEngine.GenerateUnit(domain, address, precision, alignment);
                        break;
                    case CorruptionEngine.NONE:
                        return null;
                }

                if (bu != null) //upgrade single blastunits to array
                    bus = new BlastUnit[] { bu };

                return bus;
            }
            catch (Exception ex)
            {
                if (CloudDebug.ShowErrorDialog(ex, true) == DialogResult.Abort)
                {
                    throw new AbortEverythingException();
                }

                return null;
            }
        }

        //Randomly spreads all corruption bytes to all selected domains
        public static BlastLayer Spread(BlastInfo blastInfo)
        {
            var bl = new BlastLayer();
            for (var i = 0; i < blastInfo.intensity; i++)
            {
                var r = RtcCore.RND.Next(blastInfo.selectedDomains.Length);
                var domain = blastInfo.selectedDomains[r];
                var maxAddress = blastInfo.domainSizes[r];
                var randomAddress = RtcCore.RND.NextLong(0, maxAddress - blastInfo.precision);

                var bus = GetBlastUnits(domain, randomAddress, blastInfo.precision, blastInfo.alignment, blastInfo.engine);
                if (bus != null)
                {
                    bl.Layer.AddRange(bus);
                }
            }

            return bl;
        }

        //Randomly spreads the corruptions bytes in one randomly selected domain
        public static BlastLayer Chunk(BlastInfo blastInfo)
        {
            var bl = new BlastLayer();
            var r = RtcCore.RND.Next(blastInfo.selectedDomains.Length);
            var domain = blastInfo.selectedDomains[r];
            var maxAddress = blastInfo.domainSizes[r];

            for (var i = 0; i < blastInfo.intensity; i++)
            {
                var randomAddress = RtcCore.RND.NextLong(0, maxAddress - blastInfo.precision);

                var bus = GetBlastUnits(domain, randomAddress, blastInfo.precision, blastInfo.alignment, blastInfo.engine);
                if (bus != null)
                {
                    bl.Layer.AddRange(bus);
                }
            }

            return bl;
        }

        // 10 shots of 10% chunk
        public static BlastLayer Burst(BlastInfo blastInfo)
        {
            var bl = new BlastLayer();
            for (var j = 0; j < 10; j++)
            {
                var r = RtcCore.RND.Next(blastInfo.selectedDomains.Length);
                var domain = blastInfo.selectedDomains[r];
                var maxAddress = blastInfo.domainSizes[r];

                for (var i = 0; i < (int)((double)blastInfo.intensity / 10); i++)
                {
                    var randomAddress = RtcCore.RND.NextLong(0, maxAddress - blastInfo.precision);

                    var bus = GetBlastUnits(domain, randomAddress, blastInfo.precision, blastInfo.alignment, blastInfo.engine);
                    if (bus != null)
                    {
                        bl.Layer.AddRange(bus);
                    }
                }
            }

            return bl;
        }

        // Blasts based on the size of the largest selected domain. Intensity =  Intensity / (domainSize[largestdomain]/domainSize[currentdomain])
        public static BlastLayer Normalized(BlastInfo blastInfo)
        {
            var bl = new BlastLayer();

            //Find the smallest domain and base our normalization around it
            //Domains aren't IComparable so I used keys
            var domainSize = new long[blastInfo.selectedDomains.Length];
            for (var i = 0; i < blastInfo.selectedDomains.Length; i++)
            {
                var domain = blastInfo.selectedDomains[i];
                domainSize[i] = MemoryDomains.GetInterface(domain)
                    .Size;
            }

            //Sort the arrays
            Array.Sort(domainSize, blastInfo.selectedDomains);

            for (var i = 0; i < blastInfo.selectedDomains.Length; i++)
            {
                var domain = blastInfo.selectedDomains[i];

                //Get the intensity divider. The size of the largest domain divided by the size of the current domain
                var normalized = (domainSize[blastInfo.selectedDomains.Length - 1] / (domainSize[i]));

                for (var j = 0; j < (blastInfo.intensity / normalized); j++)
                {
                    var maxAddress = domainSize[i];
                    var randomAddress = RtcCore.RND.NextLong(0, maxAddress - blastInfo.precision);
                    var bus = GetBlastUnits(domain, randomAddress, blastInfo.precision, blastInfo.alignment, blastInfo.engine);
                    if (bus != null)
                    {
                        bl.Layer.AddRange(bus);
                    }
                }
            }

            return bl;
        }

        //Blasts proportionally based on the total size of all selected domains
        public static BlastLayer Proportional(BlastInfo blastInfo)
        {
            var bl = new BlastLayer();
            var totalSize = blastInfo.domainSizes.Sum(); //Gets the total size of all selected domains

            var normalizedIntensity = new long[blastInfo.selectedDomains.Length]; //matches the index of selectedDomains
            for (var i = 0; i < blastInfo.selectedDomains.Length; i++)
            {   //calculates the proportionnal normalized Intensity based on total selected domains size
                var proportion = blastInfo.domainSizes[i] / (double)totalSize;
                normalizedIntensity[i] = Convert.ToInt64(blastInfo.intensity * proportion);
            }

            for (var i = 0; i < blastInfo.selectedDomains.Length; i++)
            {
                var domain = blastInfo.selectedDomains[i];

                for (var j = 0; j < normalizedIntensity[i]; j++)
                {
                    var maxAddress = blastInfo.domainSizes[i];
                    var randomAddress = RtcCore.RND.NextLong(0, maxAddress - blastInfo.precision);

                    var bus = GetBlastUnits(domain, randomAddress, blastInfo.precision, blastInfo.alignment, blastInfo.engine);
                    if (bus != null)
                    {
                        bl.Layer.AddRange(bus);
                    }
                }
            }

            return bl;
        }

        //Evenly distributes the blasts through all selected domains
        public static BlastLayer Even(BlastInfo blastInfo)
        {
            var bl = new BlastLayer();

            for (var i = 0; i < blastInfo.selectedDomains.Length; i++)
            {
                var domain = blastInfo.selectedDomains[i];

                for (var j = 0; j < (blastInfo.intensity / blastInfo.selectedDomains.Length); j++)
                {
                    var maxAddress = blastInfo.domainSizes[i];
                    var randomAddress = RtcCore.RND.NextLong(0, maxAddress - blastInfo.precision);

                    var bus = GetBlastUnits(domain, randomAddress, blastInfo.precision, blastInfo.alignment, blastInfo.engine);
                    if (bus != null)
                    {
                        bl.Layer.AddRange(bus);
                    }
                }
            }

            return bl;
        }
    }
}
