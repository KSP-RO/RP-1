namespace RP0.Programs
{
    public class ProgramHandlerSettings : IConfigNode
    {
        [Persistent]
        public FloatCurve paymentCurve = new FloatCurve();

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);

            var fc = new FloatCurve();
            fc.Load(node.GetNode("paymentCurve"));
            paymentCurve = fc;
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
