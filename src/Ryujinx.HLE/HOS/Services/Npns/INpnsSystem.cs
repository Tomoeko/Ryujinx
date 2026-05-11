namespace Ryujinx.HLE.HOS.Services.Npns
{
    using Ryujinx.HLE.HOS.Ipc;
    using Ryujinx.Horizon.Common;
    using Ryujinx.HLE.HOS.Kernel.Threading;
    using System;

    [Service("npns:s")]
    class INpnsSystem : IpcService
    {
        private KEvent _receiveEvent;

        public INpnsSystem(ServiceCtx context) { }

        [CommandCmif(5)]
        // GetReceiveEvent() -> handle<copy>
        public ResultCode GetReceiveEvent(ServiceCtx context)
        {
            if (_receiveEvent == null)
            {
                _receiveEvent = new KEvent(context.Device.System.KernelContext);
            }

            if (context.Process.HandleTable.GenerateHandle(_receiveEvent.ReadableEvent, out int handle) != Result.Success)
            {
                throw new InvalidOperationException("Out of handles!");
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(handle);

            return ResultCode.Success;
        }

        private KEvent _stateChangeEvent;

        [CommandCmif(8)]
        // GetStateChangeEvent() -> handle<copy>
        public ResultCode GetStateChangeEvent(ServiceCtx context)
        {
            if (_stateChangeEvent == null)
            {
                _stateChangeEvent = new KEvent(context.Device.System.KernelContext);
            }

            if (context.Process.HandleTable.GenerateHandle(_stateChangeEvent.ReadableEvent, out int handle) != Result.Success)
            {
                throw new InvalidOperationException("Out of handles!");
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(handle);

            return ResultCode.Success;
        }

        [CommandCmif(103)]
        // GetState() -> u32
        public ResultCode GetState(ServiceCtx context)
        {
            context.ResponseData.Write(0u);

            return ResultCode.Success;
        }

        [CommandCmif(106)] // 18.0.0+
        // GetLastNotifiedTime() -> u64
        public ResultCode GetLastNotifiedTime(ServiceCtx context)
        {
            context.ResponseData.Write(0L);

            return ResultCode.Success;
        }

        [CommandCmif(107)] // 18.0.0+
        // SetLastNotifiedTime(u64)
        public ResultCode SetLastNotifiedTime(ServiceCtx context)
        {
            context.RequestData.ReadUInt64();

            return ResultCode.Success;
        }
    }
}
