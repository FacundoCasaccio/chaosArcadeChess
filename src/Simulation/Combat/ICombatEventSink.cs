using System.Collections.Generic;
using ChaosArcadeTower.Domain.Combat;

namespace ChaosArcadeTower.Simulation.Combat
{
    public interface ICombatEventSink
    {
        void Push(CombatEvent evt);
    }

    public class ListCombatEventSink : ICombatEventSink
    {
        private readonly List<CombatEvent> _events = new();

        public List<CombatEvent> EventList => _events;

        public void Push(CombatEvent evt) => _events.Add(evt);
    }
}
