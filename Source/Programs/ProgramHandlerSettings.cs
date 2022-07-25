using RP0.DataTypes;

namespace RP0.Programs
{
    public class ProgramHandlerSettings
    {
        [Persistent]
        public float repToConfidence = 5;

        [Persistent]
        public float sciToConfidence = 2;

        [Persistent]
        public PersistentDictionaryString<DoubleCurve> paymentCurves = new PersistentDictionaryString<DoubleCurve>();

        [Persistent]
        public string defaultFundingCurve;

        public DoubleCurve FundingCurve(string key)
        {
            if (!string.IsNullOrEmpty(key) && paymentCurves.TryGetValue(key, out var curve))
                return curve;

            return paymentCurves[defaultFundingCurve];
        }
    }
}
