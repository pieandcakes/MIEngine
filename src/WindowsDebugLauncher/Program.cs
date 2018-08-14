using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Text;

namespace WindowsDebugLauncher
{
    internal class Program
    {
        private static int Main(string[] argv)
        {
            string dbgStdInPipe;
            string dbgStdOutPipe;
            string[] dbgExe = new string[argv.Length];
            int arrayCounter = 0;

            Debug.Assert(argv.Count() > 4, "WindowsDebugLauncher.exe takes a minimum of 4 parameters");

            foreach (var a in argv)
            {
                if (String.IsNullOrEmpty(a))
                {
                    continue;
                }
                switch (a)
                {
                    case "-h":
                    case "-?":
                    case "/?":
                    case "--help":
                        Console.WriteLine("WindowsDebugLauncher: Launching debuggers for use with MIEngine in a separate process.");
                        return 1;
                    default:
                        if (a.StartsWith("--stdin=", StringComparison.Ordinal))
                        {
                            dbgStdInPipe = a.Substring("--stdin=".Length);
                            if (!dbgStdInPipe.StartsWith("\\\\.\\"))
                            {
                                Console.Error.WriteLine("WindowsDebugLauncher: stdin format not specified correctly");
                                return -1;
                            }
                        }
                        else if (a.StartsWith("--stdout=", StringComparison.Ordinal))
                        {
                            dbgStdOutPipe = a.Substring("--stdout=".Length);
                            if(!dbgStdOutPipe.StartsWith("\\\\.\\"))
                            {
                                Console.Error.WriteLine("WindowsDebugLauncher: stdout format not specified correctly");
                                return -1;
                            }
                        }
                        else
                        {
                            if (arrayCounter >= dbgExe.Count())
                            {
                                Console.Error.WriteLine("WindowsDebugLauncher: array out of bounds. Expected:{0} Counter:{1}", dbgExe.Count(), arrayCounter);
                            }

                            dbgExe[arrayCounter] = a;
                            arrayCounter++;
                        }
                        break;
                }
            }
            return 0;
        }

        private void StartPipeConnection(string stdIn, string stdOut, string[] dbgCmd)
        {

            NamedPipeClientStream dbgStdIn = new NamedPipeClientStream(stdIn);
            NamedPipeClientStream dbgStdOut = new NamedPipeClientStream(stdOut);

            

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = dbgCmd[0];
            info.Arguments = dbgCmd.ToString();

            Process proc = new Process();
            proc.StartInfo = info;
            proc.StandardInput

        }
    }
}
