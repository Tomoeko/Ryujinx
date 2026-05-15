using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Htcs
{
    [Service("htc:tenv")]
    class IHtcTenvServiceManager : IpcService
    {
        // htc:tenv - Host Target Communication: Target Environment service
        // Provides environment variables from the host PC to the target.
        //
        // nn::htc::tenv::IServiceManager
        //   Cmd 0: GetServiceInterface(pid) -> IService
        //
        // Result codes (Module 18 = htc):
        //   ResultConnectionFailure = (18 | (1 << 9))  = 0x0212
        //   ResultNotFound          = (18 | (2 << 9))  = 0x0412
        //   ResultNotEnoughBuffer   = (18 | (3 << 9))  = 0x0612

        public IHtcTenvServiceManager(ServiceCtx context) { }

        [CommandCmif(0)]
        // GetServiceInterface(pid) -> object<IService>
        public ResultCode GetServiceInterface(ServiceCtx context)
        {
            long processId = context.RequestData.ReadInt64();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { processId });

            MakeObject(context, new IHtcTenvService());

            return ResultCode.Success;
        }
    }

    class IHtcTenvService : IpcService
    {
        // IService for htc:tenv
        //
        // nn::htc::tenv::IService
        //   Cmd 0: GetVariable(VariableName[64], buffer<out>) -> int64 outSize
        //   Cmd 1: GetVariableLength(VariableName[64]) -> int64 outSize
        //   Cmd 2: WaitUntilVariableAvailable(int64 timeoutMs)
        //
        // On real devkit hardware, the tenv service reads variables from a
        // "definition file" (.tdf) that is managed by Target Manager.
        // Key variable used by ARMS: SEAD_NIN_SAVE_DIR
        //
        // When no host PC is connected, GetVariable returns ResultConnectionFailure.
        // However, on real hardware the definition file path is registered by the
        // htc system service at boot, so variables can still be read from the
        // cached file even without an active host connection.
        //
        // For emulation, we return ResultConnectionFailure for all variables.
        // The game's sead framework handles this gracefully and falls back to
        // default paths.

        // htc module = 18
        // ResultConnectionFailure: Module 18, Description 1
        private const int HtcResultConnectionFailure = (18 | (1 << 9)); // 0x0212

        public IHtcTenvService() { }

        [CommandCmif(0)]
        // GetVariable(VariableName[64], buffer<out>) -> int64 outSize
        public ResultCode GetVariable(ServiceCtx context)
        {
            byte[] nameBytes = context.RequestData.ReadBytes(64);
            string variableName = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { variableName });

            // Write outSize = 0 (no data returned)
            context.ResponseData.Write(0L);

            // Return connection failure - no host PC connected
            return (ResultCode)HtcResultConnectionFailure;
        }

        [CommandCmif(1)]
        // GetVariableLength(VariableName[64]) -> int64 outSize
        public ResultCode GetVariableLength(ServiceCtx context)
        {
            byte[] nameBytes = context.RequestData.ReadBytes(64);
            string variableName = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { variableName });

            // Write outSize = 0
            context.ResponseData.Write(0L);

            return (ResultCode)HtcResultConnectionFailure;
        }

        [CommandCmif(2)]
        // WaitUntilVariableAvailable(int64 timeoutMs)
        public ResultCode WaitUntilVariableAvailable(ServiceCtx context)
        {
            long timeoutMs = context.RequestData.ReadInt64();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { timeoutMs });

            // Variables will never become available — no host connection
            return (ResultCode)HtcResultConnectionFailure;
        }
    }
}
