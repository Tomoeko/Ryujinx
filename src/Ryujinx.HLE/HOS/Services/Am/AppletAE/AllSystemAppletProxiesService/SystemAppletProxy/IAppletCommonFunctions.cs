using Ryujinx.Common.Logging;

namespace Ryujinx.HLE.HOS.Services.Am.AppletAE.AllSystemAppletProxiesService.SystemAppletProxy
{
    class IAppletCommonFunctions : IpcService
    {
        public IAppletCommonFunctions() { }


        [CommandCmif(51)] // 3.0.0+
        // SetVrModeEnabled(bool enabled)
        public ResultCode SetVrModeEnabled(ServiceCtx context)
        {
            bool enabled = context.RequestData.ReadBoolean();

            Logger.Stub?.PrintStub(LogClass.ServiceAm, new { enabled });

            return ResultCode.Success;
        }

        [CommandCmif(70)] // 11.0.0+
        // SetCpuBoostRequestPriority(s32 request_priority)
        public ResultCode SetCpuBoostRequestPriority(ServiceCtx context) {
            int requestPrority = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }
    }
}
