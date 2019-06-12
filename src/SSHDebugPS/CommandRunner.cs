// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SSHDebugPS.Docker;
using Microsoft.VisualStudio.Debugger.Interop.UnixPortSupplier;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.SSHDebugPS
{
    internal interface ICommandRunner : IDisposable
    {
        event EventHandler<string> OutputReceived;
        event EventHandler<int> Closed;
        event EventHandler<ErrorOccuredEventArgs> ErrorOccured;

        void Write(string text);
        void WriteLine(string text);
    }

    internal class ErrorOccuredEventArgs : EventArgs
    {
        public ErrorOccuredEventArgs(Exception e)
        {
            Exception = e;
        }

        public Exception Exception { get; }
    }

    interface ILocalCommandRunner : ICommandRunner
    {
        void Start();
        void Start(string command, string commandArgs);
    }

    /// <summary>
    /// Run a single command on Windows. This reads output as ReadLine. Run needs to be called to run the command.
    /// </summary>
    internal class LocalCommandRunner : ILocalCommandRunner
    {
        public static int BUFMAX = 4896;

        private ProcessStartInfo _processStartInfo;
        private System.Diagnostics.Process _process;
        private CancellationTokenSource _cancellationSource = new CancellationTokenSource();
        private System.Threading.Tasks.Task _outputReadLoopTask;
        private StreamWriter _stdInWriter;
        private StreamReader _stdOutReader;
        private bool _hasExited = false;

        protected object _lock = new object();

        public event EventHandler<string> OutputReceived;
        public event EventHandler<int> Closed;
        public event EventHandler<ErrorOccuredEventArgs> ErrorOccured;

        public LocalCommandRunner()
        { }

        public LocalCommandRunner(string command, string commandArgs)
        {
            CreateProcessStartInfo(command, commandArgs);
        }

        public void Start()
        {
            if (_process != null)
            {
                if (!_process.HasExited)
                {
                    Debug.Fail("Process is already running.");

                    throw new InvalidOperationException("Process already running");
                }
                CleanUpProcess();
            }

            if (_processStartInfo == null)
            {
                throw new InvalidOperationException("Unable to create process. Process start info does not exist");
            }

            lock (_lock)
            {
                _process = new System.Diagnostics.Process();
                _process.StartInfo = _processStartInfo;

                _process.Exited += OnProcessExited;
                _process.EnableRaisingEvents = true;

                _process.Start();
            }

            _stdInWriter = new StreamWriter(_process.StandardInput.BaseStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), BUFMAX);
            _stdOutReader = new StreamReader(_process.StandardOutput.BaseStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), false, BUFMAX);

            _outputReadLoopTask = System.Threading.Tasks.Task.Run(() => ReadLoop(_stdOutReader, _cancellationSource.Token, OnOutputReceived));

            _process.ErrorDataReceived += OnErrorOutput;
            _process.BeginErrorReadLine();
        }

        public void Start(string command, string commandArgs)
        {
            CreateProcessStartInfo(command, commandArgs);
            Start();
        }

        public void Write(string text)
        {
            lock (_lock)
            {
                if (IsRunning())
                {
                    _stdInWriter.Write(text);
                    _stdInWriter.Flush();
                }
            }
        }

        public void WriteLine(string text)
        {
            lock (_lock)
            {
                if (IsRunning())
                {
                    _stdInWriter.WriteLine(text);
                    _stdInWriter.Flush();
                }
            }
        }

        protected void ReportException(Exception ex)
        {
            ErrorOccured?.Invoke(this, new ErrorOccuredEventArgs(ex));
        }


        protected virtual void OnErrorOutput(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                ErrorOccured?.Invoke(this, new ErrorOccuredEventArgs(new Exception(e.Data)));
            }
        }

        protected virtual void OnProcessExited(object sender, EventArgs args)
        {
            lock (_lock)
            {
                // Make sure that all output has been written before exiting.
                if (!_hasExited && _outputReadLoopTask.IsCompleted)
                {
                    _hasExited = true;
                    Closed?.Invoke(this, _process.ExitCode);
                }
            }
        }

        protected virtual void OnOutputReceived(string line)
        {
            OutputReceived?.Invoke(this, line);
        }

        protected virtual void CreateProcessStartInfo(string command, string commandArgs)
        {
            if (_process != null)
            {
                if (!_process.HasExited)
                {
                    Debug.Fail("CreateProcessStartInfo called when there is already a process running.");
                }
                else
                {
                    CleanUpProcess();
                }
            }

            if (_processStartInfo != null)
            {
                Debug.Fail("ProcessStartInfo is already set.");
                _processStartInfo = null;
            }

            _processStartInfo = new ProcessStartInfo(command, commandArgs);

            _processStartInfo.RedirectStandardError = true;
            _processStartInfo.RedirectStandardInput = true;
            _processStartInfo.RedirectStandardOutput = true;

            _processStartInfo.UseShellExecute = false;
            _processStartInfo.CreateNoWindow = true;
        }

        protected virtual void ReadLoop(StreamReader reader, CancellationToken token, Action<string> action)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    char[] buffer = new char[BUFMAX];
                    Task<string> task = reader.ReadLineAsync();
                    task.Wait(token);

                    if (task.Result == null)
                    {
                        lock (_lock)
                        {
                            if (!_hasExited && _process.HasExited)
                            {
                                _hasExited = true;
                                Closed?.Invoke(this, _process.ExitCode);
                            }
                        }
                        return; // end of stream
                    }

                    action(task.Result);
                }
            }
            catch (Exception e)
            {
                ErrorOccured?.Invoke(this, new ErrorOccuredEventArgs(e));
                Dispose();
            }
        }

        protected bool IsRunning()
        {
            return _process != null && !_process.HasExited;
        }

        protected void CleanUpProcess()
        {
            if (_process != null)
            {
                lock (_lock)
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill();
                    }

                    _cancellationSource.Cancel();

                    _outputReadLoopTask = null;
                    _stdInWriter = null;
                    _stdOutReader = null;

                    // clean up event handlers.
                    _process = null;
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    CleanUpProcess();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }


        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~LocalCommandRunnerBase()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }

    /// <summary>
    /// Launches a local command where the output is read in a buffer.
    /// </summary>
    internal class RawLocalCommandRunner : LocalCommandRunner
    {
        public RawLocalCommandRunner(string command, string args) : base(command, args) { }

        protected override void ReadLoop(StreamReader reader, CancellationToken token, Action<string> action)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    char[] buffer = new char[BUFMAX];
                    Task<int> task = reader.ReadAsync(buffer, 0, buffer.Length);
                    task.Wait(token);

                    if (task.Result > 0)
                        action(new string(buffer, 0, task.Result));
                }
            }
            catch (Exception ex)
            {
                ReportException(ex);
                Dispose();
            }
        }
    }

    internal class DockerCommandRunner : LocalCommandRunner
    {
        public DockerCommandRunner(DockerTransportSettings settings)
        {
            // process the exe command, host name 
        }
    }

    /// <summary>
    ///  Shell that uses a remote Connection to send commands and receive input/output
    /// </summary>
    internal class RemoteCommandRunner : ICommandRunner, IDebugUnixShellCommandCallback
    {
        private IDebugUnixShellAsyncCommand _asyncCommand;
        private bool _isRunning;

        public RemoteCommandRunner(string command, string arguments, Connection remoteConnection)
        {
            string commandText = string.Concat(command, " ", arguments);
            remoteConnection.BeginExecuteAsyncCommand(commandText, runInShell: false, callback: this, asyncCommand: out _asyncCommand);
            _isRunning = true;
        }

        public event EventHandler<string> OutputReceived;
        public event EventHandler<int> Closed;
        public event EventHandler<ErrorOccuredEventArgs> ErrorOccured;

        public void Dispose()
        {
            if (_isRunning)
            {
                _asyncCommand.Abort();
                _isRunning = false;
            }
        }

        public void Write(string text)
        {
            EnsureRunning();
            _asyncCommand.Write(text);
        }

        public void WriteLine(string text)
        {
            EnsureRunning();
            _asyncCommand.WriteLine(text);
        }

        void IDebugUnixShellCommandCallback.OnOutputLine(string line)
        {
            OutputReceived?.Invoke(this, line);
        }

        void IDebugUnixShellCommandCallback.OnExit(string exitCode)
        {
            _isRunning = false;
            int code;
            if (!Int32.TryParse(exitCode, out code))
            {
                ErrorOccured?.Invoke(this, new ErrorOccuredEventArgs(new Exception(StringResources.Error_ExitCodeNotParseable)));
                code = -1;
            }
            Closed?.Invoke(this, code);
        }

        private void EnsureRunning()
        {
            if (!_isRunning)
                throw new InvalidOperationException(StringResources.Error_ShellNotRunning);
        }
    }
}
