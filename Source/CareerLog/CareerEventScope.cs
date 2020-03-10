using System;
using System.Collections.Generic;

namespace RP0
{
    public class CareerEventScope : IDisposable
    {
        private static readonly Stack<CareerEventScope> _scopes = new Stack<CareerEventScope>(1);

        public static CareerEventScope Current => _scopes.Count > 0 ? _scopes.Peek() : null;

        public CareerEventType EventType { get; private set; }

        public CareerEventScope(CareerEventType eventType)
        {
            EventType = eventType;
            _scopes.Push(this);
        }

        public void Dispose()
        {
            _scopes.Pop();
        }
    }

    public enum CareerEventType
    {
        Tooling, Maintenance
    }
}
