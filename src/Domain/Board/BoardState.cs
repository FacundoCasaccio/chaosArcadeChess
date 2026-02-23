using System.Collections.Generic;
using System.Linq;
using ChaosArcadeTower.Domain.Pieces;

namespace ChaosArcadeTower.Domain.Board
{
    public class BoardState
    {
        public const int ACTIVE_SLOTS = 5;
        public const int DEFAULT_RESERVE_SLOTS = 2;

        public PieceInstance?[] ActiveSlots { get; }
        public List<PieceInstance> Reserve { get; }
        public int MaxReserve { get; set; }

        public BoardState()
        {
            ActiveSlots = new PieceInstance?[ACTIVE_SLOTS];
            Reserve = new List<PieceInstance>();
            MaxReserve = DEFAULT_RESERVE_SLOTS;
        }

        public PieceInstance? GetSlot(int index)
        {
            if (index < 0 || index >= ACTIVE_SLOTS) return null;
            return ActiveSlots[index];
        }

        public void SetSlot(int index, PieceInstance? piece)
        {
            if (index >= 0 && index < ACTIVE_SLOTS)
                ActiveSlots[index] = piece;
        }

        public void SwapSlots(int a, int b)
        {
            if (a < 0 || a >= ACTIVE_SLOTS || b < 0 || b >= ACTIVE_SLOTS) return;
            (ActiveSlots[a], ActiveSlots[b]) = (ActiveSlots[b], ActiveSlots[a]);
        }

        public bool SwapWithReserve(int slotIndex, int reserveIndex)
        {
            if (slotIndex < 0 || slotIndex >= ACTIVE_SLOTS) return false;
            if (reserveIndex < 0 || reserveIndex >= Reserve.Count) return false;

            var fromBoard = ActiveSlots[slotIndex];
            var fromReserve = Reserve[reserveIndex];
            ActiveSlots[slotIndex] = fromReserve;
            if (fromBoard != null)
                Reserve[reserveIndex] = fromBoard;
            else
                Reserve.RemoveAt(reserveIndex);
            return true;
        }

        public List<PieceInstance> GetAlivePieces()
        {
            return ActiveSlots.Where(p => p != null && !p.IsDead).Cast<PieceInstance>().ToList();
        }

        public int CountAlive() => ActiveSlots.Count(p => p != null && !p.IsDead);

        public List<PieceInstance> GetAllPieces()
        {
            var all = new List<PieceInstance>();
            foreach (var p in ActiveSlots)
                if (p != null) all.Add(p);
            all.AddRange(Reserve);
            return all;
        }

        public BoardState DeepClone()
        {
            var clone = new BoardState { MaxReserve = MaxReserve };
            for (int i = 0; i < ACTIVE_SLOTS; i++)
                clone.ActiveSlots[i] = ActiveSlots[i]?.DeepClone();
            foreach (var r in Reserve)
                clone.Reserve.Add(r.DeepClone());
            return clone;
        }
    }
}
