// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.SSHDebugPS.SSH;
using Microsoft.SSHDebugPS.Utilities;
using Microsoft.VisualStudio.PlatformUI;

namespace Microsoft.SSHDebugPS.Docker
{
    public class DockerHelper
    {
        private const string dockerPSCommand = "ps";
        // --no-trunc avoids parameter truncation
        private const string dockerPSArgs = "--no-trunc --format \"{{json .}}\"";
        public static IEnumerable<IContainerInstance> GetLocalDockerContainers()
        {
            List<DockerContainerInstance> containers = new List<DockerContainerInstance>();

            // TODO: hook up hostname field
            var settings = new DockerCommandSettings(string.Empty, false);
            settings.SetCommand(dockerPSCommand, dockerPSArgs);

            LocalCommandRunner commandRunner = new LocalCommandRunner(settings, false);

            StringBuilder errorSB = new StringBuilder();
            int? exitCode = null;

            try
            {
                ManualResetEvent resetEvent = new ManualResetEvent(false);
                commandRunner.ErrorOccured += ((sender, args) =>
                {
                    resetEvent.Set();
                });

                commandRunner.Closed += ((sender, args) =>
                {
                    exitCode = args;
                    resetEvent.Set();
                });

                commandRunner.OutputReceived += ((sender, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args))
                    {
                        if (args.Trim()[0] != '{')
                        {
                            // output isn't json, command Error
                            //string errorMessage = UIResources.CommandExecutionErrorFormat.FormatCurrentCultureWithArgs("{0} {1}".FormatInvariantWithArgs(settings.Command, settings.CommandArgs), args);
                            //throw new CommandFailedException(errorMessage);
                            errorSB.AppendLine(args);
                        }
                        else
                        {
                            var containerInstance = DockerContainerInstance.Create(args);
                            if (containerInstance != null)
                                containers.Add(containerInstance);
                        }
                    }
                });

                commandRunner.Start();

                int retry = 10;
                // TODO: Switch to cancellable wait (retry 10 times)
                while (!resetEvent.WaitOne(4000) && retry > 0)
                {
                    retry--;
                }

                // might need to throw an exception here too??
                if (exitCode != 0)
                {
                    Debug.Fail(FormattableString.Invariant($"Exit Code: {exitCode}"));
                    return null;
                }

                if (errorSB.Length > 0)
                {
                    throw new CommandFailedException(errorSB.ToString());
                }

                return containers;
            }
            catch (Win32Exception ex)
            {
                // docker doesn't exist 
                string errorMessage = UIResources.CommandExecutionErrorFormat.FormatCurrentCultureWithArgs(settings.CommandArgs, ex.Message);
                throw new CommandFailedException(errorMessage, ex);
            }
        }

        internal static IEnumerable<IContainerInstance> GetRemoteDockerContainers(IConnection connection)
        {
            SSHConnection sshConnection = connection as SSHConnection;
            List<string> outputLines = new List<string>();
            StringBuilder errorSB = new StringBuilder();
            if (sshConnection == null)
            {
                return null;
            }

            List<DockerContainerInstance> containers = new List<DockerContainerInstance>();

            //TODO: Hook up hostname
            var settings = new DockerCommandSettings(string.Empty, false);
            settings.SetCommand(dockerPSCommand, dockerPSArgs);

            RemoteCommandRunner commandRunner = new RemoteCommandRunner(settings, sshConnection);

            ManualResetEvent resetEvent = new ManualResetEvent(false);
            int exitCode = 0;
            commandRunner.ErrorOccured += ((sender, args) =>
            {
                errorSB.Append(args);
            });

            commandRunner.Closed += ((sender, args) =>
            {
                exitCode = args;
                resetEvent.Set();
            });

            commandRunner.OutputReceived += ((sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args))
                {
                    // If it isn't json, assume its an error message
                    if (args.Trim()[0] != '{')
                    {
                        errorSB.Append(args);
                    }

                    // Unix line endings are '\n' so split on that for json items.
                    foreach (var item in args.Split('\n').ToList())
                    {
                        if (!string.IsNullOrWhiteSpace(item))
                            outputLines.Add(item);
                    }
                }
            });

            commandRunner.Start();

            resetEvent.WaitOne();

            if (exitCode != 0)
            {
                // if the exit code is not zero, then the output we received possibly is the error message
                string exceptionMessage = string.Format(CultureInfo.CurrentCulture,
                    UIResources.CommandExecutionErrorWithExitCodeFormat,
                    "{0} {1}".FormatInvariantWithArgs(settings.Command, settings.CommandArgs),
                    exitCode,
                    errorSB.ToString());

                throw new CommandFailedException(exceptionMessage);
            }

            foreach (var item in outputLines)
            {
                containers.Add(DockerContainerInstance.Create(item));
            }

            return containers;
        }
    }
}
