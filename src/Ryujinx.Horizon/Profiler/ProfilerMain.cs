using Ryujinx.Horizon.Ptm;

namespace Ryujinx.Horizon.Profiler
{
    class ProfilerMain : IService
    {
        public static void Main(ServiceTable serviceTable)
        {
            ProfilerIpcServer ipcServer = new();

            ipcServer.Initialize();

            serviceTable.SignalServiceReady();

            ipcServer.ServiceRequests();
            ipcServer.Shutdown();
        }
    }
}
