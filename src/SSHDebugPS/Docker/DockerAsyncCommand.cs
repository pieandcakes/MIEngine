
using System;
using Microsoft.VisualStudio.Debugger.Interop.UnixPortSupplier;

namespace Microsoft.SSHDebugPS.Docker
{
    internal class DockerAsyncCommand : IDebugUnixShellAsyncCommand
    {
        private ICommandRunner _runner;
        private IDebugUnixShellCommandCallback _callback;
        public DockerAsyncCommand(ICommandRunner runner, IDebugUnixShellCommandCallback callback)
        {
            _callback = callback;
            _runner = runner;
            _runner.OutputReceived += Runner_OutputReceived;
            _runner.ErrorOccured += Runner_ErrorOccured;
            _runner.Closed += Runner_Closed;

            _runner.Start();
        }

        private void Runner_Closed(object sender, int e)
        {
            _callback.OnExit(e.ToString());
        }

        private void Runner_ErrorOccured(object sender, ErrorOccuredEventArgs e)
        {
            //throw error?
        }

        private void Runner_OutputReceived(object sender, string e)
        {
            if (!string.IsNullOrEmpty(e))
                _callback.OnOutputLine(e);
        }

        public void Write(string text)
        {
            _runner.Write(text);
        }

        public void WriteLine(string text)
        {
            _runner.WriteLine(text);
        }

        public void Abort()
        {
            Close();
        }

        public void Close()
        {
            if (_runner != null)
            {
                _runner.Dispose();
                _runner.OutputReceived -= Runner_OutputReceived;
                _runner.ErrorOccured -= Runner_ErrorOccured;
                _runner.Closed -= Runner_Closed;
                _runner = null;
            }
            _callback = null;
        }
    }
}
