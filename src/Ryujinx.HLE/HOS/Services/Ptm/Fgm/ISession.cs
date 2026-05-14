using Ryujinx.Common.Logging;

namespace Ryujinx.HLE.HOS.Services.Ptm.Fgm
{
    [Service("fgm")]   // 9.0.0+
    [Service("fgm:0")] // 9.0.0+
    [Service("fgm:9")] // 9.0.0+
    class ISession : IpcService
    {
        public ISession(ServiceCtx context) { }

        [CommandCmif(0)]
        // Initialize() -> object<nn::fgm::sf::IRequest>
        public ResultCode Initialize(ServiceCtx context)
        {
            MakeObject(context, new IRequest(context));

            Logger.Stub?.PrintStub(LogClass.ServicePtm);

            return ResultCode.Success;
        }
    }
}
