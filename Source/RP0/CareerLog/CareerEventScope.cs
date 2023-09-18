using System;
using System.Collections.Generic;

namespace RP0
{
    public class CareerEventScope : IDisposable
    {
        private static readonly Stack<CareerEventScope> _scopes = new Stack<CareerEventScope>(1);
        private static uint _ignoreScopeCount = 0;

        public static CareerEventScope Current => _scopes.Count > 0 ? _scopes.Peek() : null;

        public static bool ShouldIgnore => _ignoreScopeCount > 0;

        public CareerEventType EventType { get; private set; }

        public CareerEventScope(CareerEventType eventType)
        {
            EventType = eventType;
            _scopes.Push(this);
            if (eventType == CareerEventType.Ignore) _ignoreScopeCount++;
        }

        public void Dispose()
        {
            var scope = _scopes.Pop();
            if (scope.EventType == CareerEventType.Ignore) _ignoreScopeCount--;
        }
    }

    public enum CareerEventType
    {
        Ignore, Tooling, Maintenance
    }
}
