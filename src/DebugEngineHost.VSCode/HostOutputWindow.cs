// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.DebugEngineHost
{
    public static class HostOutputWindow
    {
        private static Action<string> s_launchErrorCallback;

        private static Action<IEnumerable<string>, bool, Action<int?>, Action<string>> s_runInTerminalCallback;

        public static void RegisterLaunchErrorCallback(Action<string> launchErrorCallback)
        {
            Debug.Assert(launchErrorCallback != null, "Bogus arguments to InitializeLaunchErrorCallback");
            s_launchErrorCallback = launchErrorCallback;
        }

        public static void WriteLaunchError(string outputMessage)
        {
            if (s_launchErrorCallback != null)
            {
                s_launchErrorCallback(outputMessage);
            }
        }

        public static bool TryRunInTerminal(IEnumerable<string> commandArgs, bool useExternalConsole, Action<int?> success, Action<string> error)
        {
            if (s_runInTerminalCallback != null)
            {
                s_runInTerminalCallback(commandArgs, useExternalConsole, success, error);
                return true;
            }
            return false;
        }

        public static void RegisterRunInTerminalCallback(Action<IEnumerable<string>, bool, Action<int?>, Action<string>> runInTerminalCallback)
        {
            Debug.Assert(runInTerminalCallback != null, "Callback should not be null.");
            s_runInTerminalCallback = runInTerminalCallback;
        }
    }
}
