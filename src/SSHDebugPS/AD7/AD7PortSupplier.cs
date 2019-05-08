﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.SSHDebugPS
{ 
    internal abstract class AD7PortSupplier : IDebugPortSupplier2, IDebugPortSupplier3, IDebugPortSupplierDescription2
    {
        protected abstract Guid Id { get; }
        protected abstract string Name { get; }
        protected abstract string Description { get; }

        public AD7PortSupplier()
        { }

        public abstract int AddPort(IDebugPortRequest2 request, out IDebugPort2 port);

        public int CanAddPort()
        {
            return HR.S_OK;
        }

        public virtual int GetPortSupplierId(out Guid guidPortSupplier)
        {
            guidPortSupplier = Id;
            return HR.S_OK;
        }

        public int GetPortSupplierName(out string name)
        {
            name = Name;
            return HR.S_OK;
        }

        public virtual int CanPersistPorts()
        {
            return HR.S_FALSE;
        }

        int IDebugPortSupplierDescription2.GetDescription(enum_PORT_SUPPLIER_DESCRIPTION_FLAGS[] flags, out string text)
        {
            text = Description;
            return HR.S_OK;
        }

        public virtual int EnumPersistedPorts(BSTR_ARRAY portNames, out IEnumDebugPorts2 portEnum)
        {
            throw new NotImplementedException();
        }

        public virtual int EnumPorts(out IEnumDebugPorts2 ppEnum)
        {
            throw new NotImplementedException();
        }

        public int GetPort(ref Guid guidPort, out IDebugPort2 ppPort)
        {
            throw new NotImplementedException();
        }

        public int RemovePort(IDebugPort2 pPort)
        {
            throw new NotImplementedException();
        }
    }
}
