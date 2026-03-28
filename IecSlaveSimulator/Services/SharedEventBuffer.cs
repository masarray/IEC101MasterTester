using System.Collections.Generic;
using System.Linq;
using IecSlaveSimulator.Models;

namespace IecSlaveSimulator.Services
{
    public sealed class SharedEventBuffer
    {
        private readonly object _sync = new object();
        private readonly Queue<SharedBufferEvent> _events = new Queue<SharedBufferEvent>();
        private readonly int _capacity;
        private long _nextSequenceNumber = 1;

        public SharedEventBuffer(int capacity = 5000)
        {
            _capacity = capacity < 100 ? 100 : capacity;
        }

        public int Count
        {
            get
            {
                lock (_sync)
                {
                    return _events.Count;
                }
            }
        }

        public void Enqueue(SharedBufferEvent entry)
        {
            if (entry == null)
            {
                return;
            }

            lock (_sync)
            {
                if (entry.SequenceNumber <= 0)
                {
                    entry.SequenceNumber = _nextSequenceNumber++;
                }

                _events.Enqueue(entry);
                while (_events.Count > _capacity)
                {
                    _events.Dequeue();
                }
            }
        }

        public bool TryDequeue(out SharedBufferEvent entry)
        {
            lock (_sync)
            {
                if (_events.Count == 0)
                {
                    entry = null;
                    return false;
                }

                entry = _events.Dequeue();
                return true;
            }
        }

        public bool TryPeek(out SharedBufferEvent entry)
        {
            lock (_sync)
            {
                if (_events.Count == 0)
                {
                    entry = null;
                    return false;
                }

                entry = _events.Peek();
                return true;
            }
        }

        public IReadOnlyList<SharedBufferEvent> Snapshot(int maxCount)
        {
            lock (_sync)
            {
                int take = maxCount <= 0 ? _events.Count : maxCount;
                return _events.Reverse().Take(take).ToList();
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _events.Clear();
                _nextSequenceNumber = 1;
            }
        }
    }
}
