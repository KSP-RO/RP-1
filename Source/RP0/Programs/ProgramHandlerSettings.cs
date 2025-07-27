using ROUtils;
using ROUtils.DataTypes;

namespace RP0.Programs
{
    public class ProgramHandlerSettings
    {
        [Persistent]
        public float repToConfidence = 5;

        [Persistent]
        public HermiteCurve scienceToConfidence = new HermiteCurve();

        [Persistent]
        public PersistentDictionaryValueTypeKey<string, HermiteCurve> paymentCurves = new PersistentDictionaryValueTypeKey<string, HermiteCurve>();

        [Persistent]
        public string defaultFundingCurve;

        public HermiteCurve FundingCurve(string key)
        {
            if (!string.IsNullOrEmpty(key) && paymentCurves.TryGetValue(key, out var curve))
                return curve;

            return paymentCurves[defaultFundingCurve];
        }
    }
}
