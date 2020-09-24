namespace RTCV.UI.Components.EngineConfig.Engines
{
    internal partial class VectorEngine : EngineConfigControl
    {
        internal VectorEngine(CorruptionEngineForm parent)
        {
            InitializeComponent();

            cbVectorLimiterList.SelectedIndexChanged += parent.UpdateVectorLimiterList;
        }
    }
}
