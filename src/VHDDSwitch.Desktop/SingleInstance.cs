using System;
using System.Threading;
using System.Windows.Forms;
using System.Security.Principal;

namespace VHDDSwitch.Desktop
{
    // Local session and user scoped. Demo mode cannot activate a real-storage UI.
    internal sealed class SingleInstance : IDisposable
    {
        readonly Mutex mutex;
        readonly EventWaitHandle activation;
        RegisteredWaitHandle listener;
        internal bool IsPrimary { get; private set; }

        internal SingleInstance(bool demo)
        {
            string name = @"Local\VHDDSwitch." + WindowsIdentity.GetCurrent().User.Value + (demo ? ".Demo" : ".App");
            activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
            mutex = new Mutex(false, name + ".Instance");
            try { IsPrimary = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { IsPrimary = true; }
            if (!IsPrimary) activation.Set();
        }
        internal void Listen(Form window, Action restore)
        {
            // Create the UI handle before dispatching requests. An activation sent
            // while startup is still running remains signaled until this listener.
            var handle = window.Handle;
            listener = ThreadPool.RegisterWaitForSingleObject(activation, delegate(object state, bool timeout)
            {
                try { if (!window.IsDisposed) window.BeginInvoke(restore); }
                catch (InvalidOperationException) { /* Window is shutting down. */ }
            }, null, Timeout.Infinite, false);
        }
        public void Dispose()
        {
            if (listener != null) listener.Unregister(null);
            if (IsPrimary) mutex.ReleaseMutex();
            mutex.Dispose(); activation.Dispose();
        }
    }
}
