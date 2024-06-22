using ROUtils.DataTypes;

namespace RP0.Programs
{
    public class ProgramHandlerSettings
    {
        [Persistent]
        public float repToConfidence = 5;

        [Persistent]
        public FloatCurve scienceToConfidence = new FloatCurve();

        [Persistent]
        public PersistentDictionaryValueTypeKey<string, DoubleCurve> paymentCurves = new PersistentDictionaryValueTypeKey<string, DoubleCurve>();

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
