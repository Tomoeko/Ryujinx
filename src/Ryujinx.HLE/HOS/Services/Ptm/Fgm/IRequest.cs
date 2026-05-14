using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel.Threading;

namespace Ryujinx.HLE.HOS.Services.Ptm.Fgm
{
    class IRequest : IpcService
    {
        private KEvent _event;
        private int _eventHandle;

        public IRequest(ServiceCtx context)
        {
            _event = new KEvent(context.Device.System.KernelContext);
        }

        [CommandCmif(0)]
        // Initialize(nn::fgm::Module module_id, pid) -> handle<copy, event>
        public ResultCode Initialize(ServiceCtx context)
        {
            // Module ID and PID are inputs — we just stub them
            int moduleId = context.RequestData.ReadInt32();

            if (_eventHandle == 0)
            {
                context.Process.HandleTable.GenerateHandle(_event.ReadableEvent, out _eventHandle);
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_eventHandle);

            Logger.Stub?.PrintStub(LogClass.ServicePtm, new { moduleId });

            return ResultCode.Success;
        }

        [CommandCmif(1)]
        // Set(nn::fgm::Setting min, nn::fgm::Setting max)
        public ResultCode Set(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServicePtm);

            return ResultCode.Success;
        }

        [CommandCmif(2)]
        // Get() -> u32
        public ResultCode Get(ServiceCtx context)
        {
            context.ResponseData.Write(0u); // Return default setting value

            Logger.Stub?.PrintStub(LogClass.ServicePtm);

            return ResultCode.Success;
        }

        [CommandCmif(3)]
        // Cancel()
        public ResultCode Cancel(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServicePtm);

            return ResultCode.Success;
        }
    }
}
