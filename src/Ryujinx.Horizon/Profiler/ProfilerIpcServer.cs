using Ryujinx.Horizon.Sdk.Sf.Hipc;
using Ryujinx.Horizon.Sdk.Sm;
using Ryujinx.Horizon.Profiler.Ipc;

namespace Ryujinx.Horizon.Profiler
{
    class ProfilerIpcServer
    {
        private const int TotalMaxSessionsCount = 63;

        private const int PointerBufferSize = 0x400;
        private const int MaxDomains = 16;
        private const int MaxDomainObjects = 100;
        private const int MaxPortsCount = 1;

        private static readonly ManagerOptions _options = new(PointerBufferSize, MaxDomains, MaxDomainObjects, false);

        private SmApi _sm;
        private ServerManager _serverManager;

        public void Initialize()
        {
            HeapAllocator allocator = new();

            _sm = new SmApi();
            _sm.Initialize().AbortOnFailure();

            _serverManager = new ServerManager(allocator, _sm, MaxPortsCount, _options, TotalMaxSessionsCount);

            _serverManager.RegisterObjectForServer(new ProfilerImpl(), ServiceName.Encode("banana"), 63);
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
