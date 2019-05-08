﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.OLE.Interop;

namespace Microsoft.SSHDebugPS.Docker
{
    [ComVisible(true)]
    //{91BDF293-E6A0-49C4-B033-6F36CFC4FF98}
    [Guid("91BDF293-E6A0-49C4-B033-6F36CFC4FF98")]
    public class DockerPortPicker : IDebugPortPicker
    {
        private VisualStudio.OLE.Interop.IServiceProvider _serviceProvider = null;
        int IDebugPortPicker.DisplayPortPicker(IntPtr hwndParentDialog, out string pbstrPortId)
        {
            // If this is null, then the PortPicker handler shows an error. Set to empty by default
            pbstrPortId = "";
            var window = new ContainerPicker();

            // Register parent
            WindowInteropHelper helper = new WindowInteropHelper(window);
            helper.Owner = hwndParentDialog;
         
            if (window.ShowModal() == true)
            {
                pbstrPortId = "duck";
            }

            return VSConstants.S_OK;
        }

        int IDebugPortPicker.SetSite(VisualStudio.OLE.Interop.IServiceProvider pSP)
        {
            _serviceProvider = pSP;
            return VSConstants.S_OK;
        }
    }
}
