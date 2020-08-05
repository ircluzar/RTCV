namespace RTCV.CorruptCore
{
    #pragma warning disable CA1815 //BlastInfo won't be used in comparison
    public struct BlastInfo
    {
        public int precision;
        public long[] domainSizes;
        public CorruptionEngine engine;
        public int alignment;
        public long intensity;
        public string[] selectedDomains;
    }
}
