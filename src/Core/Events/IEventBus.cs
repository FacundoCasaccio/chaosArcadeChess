using System;

namespace ChaosArcadeTower.Core.Events
{
    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler) where T : struct;
        void Unsubscribe<T>(Action<T> handler) where T : struct;
        void Publish<T>(T evt) where T : struct;
        void Clear();
    }
}
