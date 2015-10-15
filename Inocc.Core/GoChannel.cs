using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Inocc.Core
{
    public interface IClosableChannel
    {
        int Capacity { get; }
        int BufferCount { get; }
        void Close();
    }

    public interface ISender<T> : IClosableChannel
    {
        Task Send(T value);
    }

    public interface IReceiver<T> : IClosableChannel
    {
        Task<Tuple<T, bool>> Receive();
    }

    public class GoChannel<T> : ISender<T>, IReceiver<T>
    {
        public GoChannel(int capacity)
        {
            this.capacity = capacity;
            this.buffer = new Queue<T>(capacity);
        }

        private readonly object lockObj = new object();
        private readonly int capacity;
        private readonly Queue<T> buffer;

        private readonly Queue<TaskCompletionSource<Tuple<T, bool>>> receiveQueue = new Queue<TaskCompletionSource<Tuple<T, bool>>>();

        private delegate void SendAction(bool canceled);
        private readonly Queue<SendAction> sendQueue = new Queue<SendAction>();

        private bool isClosed;

        public int Capacity => this.capacity;
        public int BufferCount => this.buffer.Count;

        public void Close()
        {
            lock (this.lockObj)
            {
                if (this.isClosed)
                    throw new PanicException(GoString.FromString("close of closed channel"));
                this.isClosed = true;

                var i = this.receiveQueue.Count;
                while (--i >= 0)
                    this.receiveQueue.Dequeue().SetResult(Tuple.Create(default(T), false));

                i = this.sendQueue.Count;
                while (--i >= 0)
                    this.sendQueue.Dequeue().Invoke(true);
            }
        }

        private void DequeueSend()
        {
            if (this.sendQueue.Count > 0)
                this.sendQueue.Dequeue().Invoke(false);
        }

        public Task<Tuple<T, bool>> Receive()
        {
            var tcs = new TaskCompletionSource<Tuple<T, bool>>();
            lock (this.lockObj)
            {
                if (this.isClosed)
                    tcs.SetResult(Tuple.Create(default(T), false));
                else if (this.buffer.Count > 0)
                {
                    tcs.SetResult(Tuple.Create(this.buffer.Dequeue(), true));
                    this.DequeueSend();
                }
                else
                {
                    this.receiveQueue.Enqueue(tcs);
                    this.DequeueSend();
                }
            }
            return tcs.Task;
        }

        public Task Send(T value)
        {
            var tcs = new TaskCompletionSource<Unit>();
            lock (this.lockObj)
            {
                if (this.isClosed)
                    throw new PanicException(GoString.FromString("send on closed channel"));
                if (this.receiveQueue.Count > 0)
                {
                    this.receiveQueue.Dequeue().SetResult(Tuple.Create(value, true));
                    tcs.SetResult(new Unit());
                }
                if (this.buffer.Count < this.capacity)
                {
                    this.buffer.Enqueue(value);
                    tcs.SetResult(new Unit());
                }
                else
                {
                    this.sendQueue.Enqueue(closed =>
                    {
                        if (closed)
                            tcs.SetException(new PanicException(GoString.FromString("send on closed channel")));
                        else
                        {
                            if (this.buffer.Count < this.capacity)
                                this.buffer.Enqueue(value);
                            else
                                this.receiveQueue.Dequeue().SetResult(Tuple.Create(value, true));
                            tcs.SetResult(new Unit());
                        }
                    });
                }
            }
            return tcs.Task;
        }
    }

    public static class GoChannel
    {
        public static void Close(IClosableChannel c)
        {
            if (c == null)
                throw new PanicException(GoString.FromString("close of nil channel"));
            c.Close();
        }

        public static Task Send<T>(ISender<T> c, T value)
        {
            return c == null
                ? new TaskCompletionSource<Unit>().Task
                : c.Send(value);
        }

        public static Task<Tuple<T, bool>> Receive<T>(IReceiver<T> c)
        {
            return c == null
                ? new TaskCompletionSource<Tuple<T, bool>>().Task
                : c.Receive();
        }

        public static int Cap(IClosableChannel c)
        {
            return c == null ? 0 : c.Capacity;
        }

        public static int Len(IClosableChannel c)
        {
            return c == null ? 0 : c.BufferCount;
        }
    }
}
