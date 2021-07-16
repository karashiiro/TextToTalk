using System;
using System.Threading;

namespace TextToTalk.Backends.Polly
{
    public class RepeatingAction : IDisposable
    {
        private readonly Thread actionThread;
        private readonly TimeSpan delay;
        private readonly Action action;

        private bool active;

        public RepeatingAction(Action action, TimeSpan delay)
        {
            this.action = action;
            this.delay = delay;

            this.active = true;
            this.actionThread = new Thread(DoActionLoop);
            this.actionThread.Start();
        }

        private void DoActionLoop()
        {
            while (active)
            {
                this.action();
                Thread.Sleep(this.delay);
            }
        }

        public void Dispose()
        {
            this.active = false;
            this.actionThread.Join();
        }
    }
}