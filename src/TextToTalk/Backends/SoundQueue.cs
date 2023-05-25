using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TextToTalk.Backends
{
    public abstract class SoundQueue<TQueueItem> : IDisposable where TQueueItem : SoundQueueItem
    {
        // This used to be a ConcurrentQueue<T>, but was changed so that items could be
        // queried and removed from the middle.
        private readonly IList<TQueueItem> queuedSounds;

        private readonly Thread soundThread;

        private TQueueItem? currentItem;
        private bool active;

        protected SoundQueue()
        {
            this.queuedSounds = new List<TQueueItem>();
            this.active = true;
            this.soundThread = new Thread(PlaySoundLoop);
            this.soundThread.Start();
        }

        private void PlaySoundLoop()
        {
            while (active)
            {
                this.currentItem = TryDequeue();
                if (this.currentItem == null)
                {
                    Thread.Sleep(100);
                    continue;
                }

                try
                {
                    OnSoundLoop(this.currentItem);
                }
                catch (Exception e)
                {
                    DetailedLog.Error(e, "Error occurred during sound loop.");
                }

                this.currentItem.Dispose();
                this.currentItem = null;
            }
        }

        public void CancelAllSounds()
        {
            lock (this.queuedSounds)
            {
                while (this.queuedSounds.Count > 0)
                {
                    SafeRemoveAt(0);
                }
            }

            OnSoundCancelled();
        }

        public void CancelFromSource(TextSource source)
        {
            lock (this.queuedSounds)
            {
                for (var i = this.queuedSounds.Count - 1; i >= 0; i--)
                {
                    if (this.queuedSounds[i].Source == source)
                    {
                        SafeRemoveAt(i);
                    }
                }
            }

            if (this.currentItem?.Source == source)
            {
                OnSoundCancelled();
            }
        }

        public TextSource GetCurrentlySpokenTextSource()
        {
            return this.currentItem?.Source ?? TextSource.None;
        }

        protected void AddQueueItem(TQueueItem item)
        {
            lock (this.queuedSounds)
            {
                this.queuedSounds.Add(item);
            }
        }

        /// <summary>
        /// Called each time an item is pulled from the queue.
        /// </summary>
        /// <param name="nextItem">The next item to be played.</param>
        protected abstract void OnSoundLoop(TQueueItem nextItem);

        /// <summary>
        /// Called whenever the current item playback is cancelled.
        /// </summary>
        protected abstract void OnSoundCancelled();

        private TQueueItem? TryDequeue()
        {
            TQueueItem? nextItem;
            lock (this.queuedSounds)
            {
                nextItem = this.queuedSounds.FirstOrDefault();
                if (nextItem != null)
                {
                    this.queuedSounds.RemoveAt(0);
                    // Not discarded like the others
                }
            }

            return nextItem;
        }

        /// <summary>
        /// Safely remove the item at the specified index from the sound queue. Always
        /// call this method in a synchronized block.
        /// </summary>
        /// <param name="index">The index of the item to remove.</param>
        private void SafeRemoveAt(int index)
        {
            // We dispose after removing to avoid edge cases in which the item is disposed
            // and then pulled from the sound thread, before we remove it.

            // ReSharper disable InconsistentlySynchronizedField
            var soundItem = this.queuedSounds[index];
            this.queuedSounds.RemoveAt(index);
            soundItem.Dispose();
            // ReSharper restore InconsistentlySynchronizedField
        }

        protected virtual void Dispose(bool disposing)
        {
            // ReSharper disable once InvertIf
            if (disposing)
            {
                this.active = false;
                CancelAllSounds();
                this.soundThread.Join();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}