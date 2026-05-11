using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.Horizon.Common;
using System;

namespace Ryujinx.HLE.HOS.Services.Am.AppletAE.AllSystemAppletProxiesService.SystemAppletProxy
{
    class ICradleFirmwareUpdater : IpcService
    {
        public ICradleFirmwareUpdater(ServiceCtx context) { }

        [CommandCmif(0)]
        // StartUpdate()
        public ResultCode StartUpdate(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [CommandCmif(1)]
        // FinishUpdate()
        public ResultCode FinishUpdate(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [CommandCmif(2)]
        // GetCradleDeviceInfo() -> unknown
        public ResultCode GetCradleDeviceInfo(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        private int _cradleDeviceInfoChangeEventHandle;

        [CommandCmif(3)]
        // GetCradleDeviceInfoChangeEvent() -> handle<copy>
        public ResultCode GetCradleDeviceInfoChangeEvent(ServiceCtx context)
        {
            if (_cradleDeviceInfoChangeEventHandle == 0)
            {
                KEvent evnt = new KEvent(context.Device.System.KernelContext);

                if (context.Process.HandleTable.GenerateHandle(evnt.ReadableEvent, out _cradleDeviceInfoChangeEventHandle) != Result.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_cradleDeviceInfoChangeEventHandle);

            return ResultCode.Success;
        }
    }
}
