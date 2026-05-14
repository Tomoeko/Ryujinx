using Ryujinx.Horizon.Sdk.Sf.Hipc;
using Ryujinx.Horizon.Sdk.Sm;
using Ryujinx.Horizon.Htc.Ipc;

namespace Ryujinx.Horizon.Htc
{
    class HtcIpcServer
    {
        private const int TotalMaxSessionsCount = 62;

        private const int PointerBufferSize = 0x400;
        private const int MaxDomains = 16;
        private const int MaxDomainObjects = 100;
        private const int MaxPortsCount = 2;

        private static readonly ManagerOptions _options = new(PointerBufferSize, MaxDomains, MaxDomainObjects, false);

        private SmApi _sm;
        private ServerManager _serverManager;

        public void Initialize()
        {
            HeapAllocator allocator = new();

            _sm = new SmApi();
            _sm.Initialize().AbortOnFailure();

            _serverManager = new ServerManager(allocator, _sm, MaxPortsCount, _options, TotalMaxSessionsCount);

            _serverManager.RegisterObjectForServer(new HtcManagerService(), ServiceName.Encode("htc"), TotalMaxSessionsCount);
            _serverManager.RegisterObjectForServer(new HtcTargetEnvironmentManagerService(), ServiceName.Encode("htc:tenv"), TotalMaxSessionsCount);
        }

        public void ServiceRequests()
        {
            _serverManager.ServiceRequests();
        }

        public void Shutdown()
        {
            _serverManager.Dispose();
            _sm.Dispose();
        }
    }
}
