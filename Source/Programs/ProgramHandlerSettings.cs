namespace RP0.Programs
{
    public class ProgramHandlerSettings
    {
        [Persistent]
        public float repToConfidence = 5;

        [Persistent]
        public float sciToConfidence = 2;

        [Persistent]
        public DoubleCurve paymentCurve = new DoubleCurve();
    }
}
