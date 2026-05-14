using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;

namespace Ryujinx.HLE.HOS.Services.Htcs
{
    [Service("htcs")]
    class IHtcsManager : IpcService
    {
        // HTCS error codes (nn::htcs module 4)
        private const int HtcsErrDisconnected = -1;

        public IHtcsManager(ServiceCtx context) { }

        // IHtcsManager command IDs:
        // 0:   Socket(out s32 errorCode, out s32 socketDescriptor)
        // 1:   Close(s32 descriptor, out s32 errorCode, out s32 result)
        // 2:   Connect(s32 descriptor, SockAddrHtcs address, out s32 errorCode, out s32 result)
        // 3:   Bind(s32 descriptor, SockAddrHtcs address, out s32 errorCode, out s32 result)
        // 4:   Listen(s32 descriptor, s32 backlogCount, out s32 errorCode, out s32 result)
        // 5:   Accept(s32 descriptor, out s32 errorCode, out s32 result, out SockAddrHtcs address)
        // 6:   Recv(s32 descriptor, s32 flags, out s32 errorCode, out s64 receivedSize, buffer<bytes> outData)
        // 7:   Send(s32 descriptor, s32 flags, buffer<bytes> inData, out s32 errorCode, out s64 sentSize)
        // 8:   Shutdown(s32 descriptor, s32 how, out s32 errorCode, out s32 result)
        // 9:   Fcntl(s32 descriptor, s32 command, s32 value, out s32 errorCode, out s32 result)
        // 10:  GetPeerNameAny(out HtcsPeerName peerName)
        // 11:  GetDefaultHostName(out HtcsPeerName peerName)
        // 12:  CreateSocketOld(out s32 errorCode, out object<ISocket>)
        // 13:  CreateSocket(bool enableDisconnectionEmulation, out s32 errorCode, out object<ISocket>)
        // 100: RegisterProcessId(pid)
        // 101: MonitorManager(pid)

        [CommandCmif(0)]
        // Socket(out s32 errorCode, out s32 socketDescriptor)
        public ResultCode Socket(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHtcs);

            // Return error code = -1 (disconnected), descriptor = -1
            context.ResponseData.Write(HtcsErrDisconnected); // errorCode
            context.ResponseData.Write(-1);                   // socketDescriptor

            return ResultCode.Success;
        }

        [CommandCmif(1)]
        // Close(s32 descriptor, out s32 errorCode, out s32 result)
        public ResultCode Close(ServiceCtx context)
        {
            int descriptor = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor });

            context.ResponseData.Write(0);  // errorCode
            context.ResponseData.Write(0);  // result

            return ResultCode.Success;
        }

        [CommandCmif(2)]
        // Connect(s32 descriptor, SockAddrHtcs address, out s32 errorCode, out s32 result)
        public ResultCode Connect(ServiceCtx context)
        {
            // SockAddrHtcs is 68 bytes (2 byte family + 32 byte peer name + 32 byte port name + 2 padding)
            byte[] address = context.RequestData.ReadBytes(68);
            int descriptor = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor });

            context.ResponseData.Write(HtcsErrDisconnected); // errorCode
            context.ResponseData.Write(-1);                   // result

            return ResultCode.Success;
        }

        [CommandCmif(3)]
        // Bind(s32 descriptor, SockAddrHtcs address, out s32 errorCode, out s32 result)
        public ResultCode Bind(ServiceCtx context)
        {
            byte[] address = context.RequestData.ReadBytes(68);
            int descriptor = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor });

            context.ResponseData.Write(HtcsErrDisconnected); // errorCode
            context.ResponseData.Write(-1);                   // result

            return ResultCode.Success;
        }

        [CommandCmif(4)]
        // Listen(s32 descriptor, s32 backlogCount, out s32 errorCode, out s32 result)
        public ResultCode Listen(ServiceCtx context)
        {
            int descriptor = context.RequestData.ReadInt32();
            int backlogCount = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor, backlogCount });

            context.ResponseData.Write(HtcsErrDisconnected); // errorCode
            context.ResponseData.Write(-1);                   // result

            return ResultCode.Success;
        }

        [CommandCmif(5)]
        // Accept(s32 descriptor, out s32 errorCode, out s32 result, out SockAddrHtcs address)
        public ResultCode Accept(ServiceCtx context)
        {
            int descriptor = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor });

            // Out raw struct is: SockAddrHtcs (68 bytes) + errorCode (4) + result (4) = 76 bytes
            // Write SockAddrHtcs (68 bytes zeroed)
            context.ResponseData.Write(new byte[68]);
            context.ResponseData.Write(HtcsErrDisconnected); // errorCode
            context.ResponseData.Write(-1);                   // result

            return ResultCode.Success;
        }

        [CommandCmif(6)]
        // Recv(s32 descriptor, s32 flags, out s32 errorCode, out s64 receivedSize, buffer<bytes>)
        public ResultCode Recv(ServiceCtx context)
        {
            int descriptor = context.RequestData.ReadInt32();
            int flags = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor, flags });

            context.ResponseData.Write(HtcsErrDisconnected);  // errorCode
            context.ResponseData.Write(0);                     // padding for alignment
            context.ResponseData.Write(0L);                    // receivedSize (s64)

            return ResultCode.Success;
        }

        [CommandCmif(7)]
        // Send(s32 descriptor, s32 flags, buffer<bytes>, out s32 errorCode, out s64 sentSize)
        public ResultCode Send(ServiceCtx context)
        {
            int descriptor = context.RequestData.ReadInt32();
            int flags = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor, flags });

            context.ResponseData.Write(HtcsErrDisconnected);  // errorCode
            context.ResponseData.Write(0);                     // padding for alignment
            context.ResponseData.Write(0L);                    // sentSize (s64)

            return ResultCode.Success;
        }

        [CommandCmif(8)]
        // Shutdown(s32 descriptor, s32 how, out s32 errorCode, out s32 result)
        public ResultCode Shutdown(ServiceCtx context)
        {
            int descriptor = context.RequestData.ReadInt32();
            int how = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor, how });

            context.ResponseData.Write(0);  // errorCode
            context.ResponseData.Write(0);  // result

            return ResultCode.Success;
        }

        [CommandCmif(9)]
        // Fcntl(s32 descriptor, s32 command, s32 value, out s32 errorCode, out s32 result)
        public ResultCode Fcntl(ServiceCtx context)
        {
            int descriptor = context.RequestData.ReadInt32();
            int command = context.RequestData.ReadInt32();
            int value = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor, command, value });

            context.ResponseData.Write(0);  // errorCode
            context.ResponseData.Write(0);  // result

            return ResultCode.Success;
        }

        [CommandCmif(10)]
        // GetPeerNameAny(out HtcsPeerName peerName)
        public ResultCode GetPeerNameAny(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHtcs);

            // HtcsPeerName is 32 bytes
            context.ResponseData.Write(new byte[32]);

            return ResultCode.Success;
        }

        [CommandCmif(11)]
        // GetDefaultHostName(out HtcsPeerName peerName)
        public ResultCode GetDefaultHostName(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHtcs);

            // HtcsPeerName is 32 bytes
            context.ResponseData.Write(new byte[32]);

            return ResultCode.Success;
        }

        [CommandCmif(12)]
        // CreateSocketOld(out s32 errorCode, out object<ISocket>)
        public ResultCode CreateSocketOld(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHtcs);

            context.ResponseData.Write(HtcsErrDisconnected); // errorCode

            MakeObject(context, new ISocket());

            return ResultCode.Success;
        }

        [CommandCmif(13)]
        // CreateSocket(bool enableDisconnectionEmulation, out s32 errorCode, out object<ISocket>)
        public ResultCode CreateSocket(ServiceCtx context)
        {
            bool enableDisconnectionEmulation = context.RequestData.ReadBoolean();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { enableDisconnectionEmulation });

            context.ResponseData.Write(HtcsErrDisconnected); // errorCode

            MakeObject(context, new ISocket());

            return ResultCode.Success;
        }

        [CommandCmif(100)]
        // RegisterProcessId(pid)
        public ResultCode RegisterProcessId(ServiceCtx context)
        {
            long processId = context.RequestData.ReadInt64();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { processId });

            return ResultCode.Success;
        }

        [CommandCmif(101)]
        // MonitorManager(pid)
        public ResultCode MonitorManager(ServiceCtx context)
        {
            long processId = context.RequestData.ReadInt64();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { processId });

            return ResultCode.Success;
        }
    }
}
