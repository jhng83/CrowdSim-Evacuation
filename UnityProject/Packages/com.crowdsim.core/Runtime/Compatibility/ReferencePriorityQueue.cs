#nullable enable
using System;
using System.Collections.Generic;

namespace CrowdSim.Core.Compatibility
{
    /// <summary>
    /// Quaternary min-heap with the reference queue's strict sibling comparisons and
    /// parent tie behavior. It deliberately does not add a stable insertion-order tie-breaker.
    /// </summary>
    public sealed class ReferencePriorityQueue<TElement, TPriority>
    {
        private (TElement Element, TPriority Priority)[] _nodes = Array.Empty<(TElement, TPriority)>();
        private readonly IComparer<TPriority> _comparer = Comparer<TPriority>.Default;
        public int Count { get; private set; }

        public void Enqueue(TElement element, TPriority priority)
        {
            if (Count == _nodes.Length) Array.Resize(ref _nodes, Math.Max(4, _nodes.Length * 2));
            int index = Count++;
            while (index > 0)
            {
                int parent = (index - 1) / 4;
                if (_comparer.Compare(priority, _nodes[parent].Priority) >= 0) break;
                _nodes[index] = _nodes[parent];
                index = parent;
            }
            _nodes[index] = (element, priority);
        }

        public bool TryDequeue(out TElement element, out TPriority priority)
        {
            if (Count == 0)
            {
                element = default!;
                priority = default!;
                return false;
            }
            (element, priority) = _nodes[0];
            int lastIndex = --Count;
            var last = _nodes[lastIndex];
            _nodes[lastIndex] = default;
            if (lastIndex == 0) return true;
            int index = 0;
            while (index * 4 + 1 < Count)
            {
                int child = index * 4 + 1;
                int end = Math.Min(child + 4, Count);
                for (int sibling = child + 1; sibling < end; sibling++)
                    if (_comparer.Compare(_nodes[sibling].Priority, _nodes[child].Priority) < 0) child = sibling;
                if (_comparer.Compare(last.Priority, _nodes[child].Priority) <= 0) break;
                _nodes[index] = _nodes[child];
                index = child;
            }
            _nodes[index] = last;
            return true;
        }

        public TElement Dequeue()
        {
            if (!TryDequeue(out TElement element, out _)) throw new InvalidOperationException("Queue is empty.");
            return element;
        }

        public void Clear()
        {
            Array.Clear(_nodes, 0, Count);
            Count = 0;
        }
    }
}
