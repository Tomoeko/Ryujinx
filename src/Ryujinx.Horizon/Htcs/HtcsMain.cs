using Ryujinx.Horizon.Ptm;

namespace Ryujinx.Horizon.Htcs
{
    class HtcsMain : IService
    {
        public static void Main(ServiceTable serviceTable)
        {
            HtcsIpcServer ipcServer = new();

            ipcServer.Initialize();

            serviceTable.SignalServiceReady();

            ipcServer.ServiceRequests();
            ipcServer.Shutdown();
        }
    }
}
