using Ryujinx.Horizon.Ptm;

namespace Ryujinx.Horizon.Htc
{
    class HtcMain : IService
    {
        public static void Main(ServiceTable serviceTable)
        {
            HtcIpcServer ipcServer = new();

            ipcServer.Initialize();

            serviceTable.SignalServiceReady();

            ipcServer.ServiceRequests();
            ipcServer.Shutdown();
        }
    }
}
