﻿using ContractConfigurator.ExpressionParser;
using RP0;
using System;
using System.Reflection;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// Used for registering some custom CC functions specific to RP-1.
    /// Needs to inherit from BaseParser just to have access to the protected static RegisterGlobalFunction() method.
    /// </summary>
    public class CustomExpressionParserRegistrer : BaseParser, IExpressionParserRegistrer
    {
        public override MethodInfo methodParseStatementInner => throw new NotImplementedException();

        public override MethodInfo methodGetRval => throw new NotImplementedException();

        public override MethodInfo methodApplyBooleanOperator => throw new NotImplementedException();

        public override MethodInfo methodParseStatement => throw new NotImplementedException();

        public override MethodInfo methodParseMethod => throw new NotImplementedException();

        public override MethodInfo methodCompleteIdentifierParsing => throw new NotImplementedException();

        public override MethodInfo method_ConvertType => throw new NotImplementedException();

        static CustomExpressionParserRegistrer()
        {
            RegisterMethods();
            RegisterRP0Hooks();
        }

        public override void ExecuteAndStoreExpression(string key, string expression, DataNode dataNode)
        {
            throw new NotImplementedException();
        }

        public override object ParseExpressionGeneric(string key, string expression, DataNode dataNode)
        {
            throw new NotImplementedException();
        }

        public void RegisterExpressionParsers()
        {
            // Nothing as of yet
        }

        private static void RegisterMethods()
        {
            Debug.Log("[RP0] CustomExpressionParserRegistrer registering methods");
            RegisterGlobalFunction(new Function<float>("RP1DeadlineMult", GetDeadlineMult, false));
            RegisterGlobalFunction(new Function<int>("RP1CommsPayload", GetCommsPayload, false));
            RegisterGlobalFunction(new Function<int>("RP1WeatherPayload", GetWeatherPayload, false));
            RegisterGlobalFunction(new Function<bool>("RP1YesPlanes", GetPlaneContractsEnabled, false));
        }

        private static void RegisterRP0Hooks()
        {
            ContractGUI.WithdrawContractAction = WithdrawContract;
        }

        private static float GetDeadlineMult()
        {
            return HighLogic.CurrentGame?.Parameters.CustomParams<RP0Settings>()?.ContractDeadlineMult ?? 1;
        }

        private static int GetCommsPayload()
        {
            return ContractGUI.CommsPayload;
        }

        private static int GetWeatherPayload()
        {
            return ContractGUI.WeatherPayload;
        }

        private static bool GetPlaneContractsEnabled()
        {
            return HighLogic.CurrentGame?.Parameters.CustomParams<RP0Settings>()?.PlaneContractsEnabled ?? true;
        }

        private static bool WithdrawContract(string contractName)
        {
            if (ContractPreLoader.Instance == null) return false;
            foreach (ConfiguredContract cc in ContractPreLoader.Instance.PendingContracts())
            {
                string name = cc?.contractType?.name;
                if (name == contractName)
                {
                    cc.Withdraw();
                    return true;
                }
            }

            return false;
        }
    }
}
