using System;
using System.Collections.Generic;

namespace ChaosArcadeTower.Core.Events
{
    public class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (!_handlers.ContainsKey(type))
                _handlers[type] = new List<Delegate>();
            _handlers[type].Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var list))
                list.Remove(handler);
        }

        public void Publish<T>(T evt) where T : struct
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
                return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                ((Action<T>)list[i]).Invoke(evt);
            }
        }

        public void Clear()
        {
            _handlers.Clear();
        }
    }
}
