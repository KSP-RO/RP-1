namespace RP0
{
    public static class ContractUtils
    {
        public static bool ContractIsRecord(ContractConfigurator.ConfiguredContract cc)
        {
            return cc.contractType.group.name == "Records" || cc.contractType.group.name == "HumanRecords";
        }
    }
}
