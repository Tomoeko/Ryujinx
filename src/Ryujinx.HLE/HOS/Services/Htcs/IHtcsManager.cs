using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel.Threading;

namespace Ryujinx.HLE.HOS.Services.Htcs
{
    [Service("htcs")]
    class IHtcsManager : IpcService
    {
        // On real devkit hardware without a host PC connected:
        // - Socket/Bind/Listen succeed normally
        // - Accept blocks the calling guest thread forever (it never returns)
        // - The server thread is NOT blocked — only the client thread waits
        //
        // We replicate this by setting SuppressReply on Accept/Recv calls.
        // This prevents the ServerBase from sending an IPC reply, so the guest
        // thread stays blocked in SendSyncRequest at the kernel level. The HLE
        // dispatch thread continues processing other service requests normally.

        private int _nextDescriptor = 1;

        public IHtcsManager(ServiceCtx context) { }

        [CommandCmif(0)]
        // Socket(out s32 errorCode, out s32 socketDescriptor)
        public ResultCode Socket(ServiceCtx context)
        {
            int descriptor = _nextDescriptor++;

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor });

            context.ResponseData.Write(0);           // errorCode = success
            context.ResponseData.Write(descriptor);  // socketDescriptor

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
            byte[] address = context.RequestData.ReadBytes(68);
            int descriptor = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor });

            // Connect returns disconnected — no host to connect to
            context.ResponseData.Write(-1); // errorCode = disconnected
            context.ResponseData.Write(-1); // result

            return ResultCode.Success;
        }

        [CommandCmif(3)]
        // Bind(s32 descriptor, SockAddrHtcs address, out s32 errorCode, out s32 result)
        public ResultCode Bind(ServiceCtx context)
        {
            byte[] address = context.RequestData.ReadBytes(68);
            int descriptor = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor });

            // Bind succeeds on real hardware
            context.ResponseData.Write(0);  // errorCode = success
            context.ResponseData.Write(0);  // result = success

            return ResultCode.Success;
        }

        [CommandCmif(4)]
        // Listen(s32 descriptor, s32 backlogCount, out s32 errorCode, out s32 result)
        public ResultCode Listen(ServiceCtx context)
        {
            int descriptor = context.RequestData.ReadInt32();
            int backlogCount = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor, backlogCount });

            // Listen succeeds on real hardware
            context.ResponseData.Write(0);  // errorCode = success
            context.ResponseData.Write(0);  // result = success

            return ResultCode.Success;
        }

        [CommandCmif(5)]
        // Accept(s32 descriptor, out s32 errorCode, out s32 result, out SockAddrHtcs address)
        public ResultCode Accept(ServiceCtx context)
        {
            int descriptor = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor });

            // Suppress the IPC reply. The guest thread will remain blocked in
            // SendSyncRequest at the kernel level — exactly like real hardware
            // when no host PC is connected. The HLE dispatch thread is NOT blocked.
            context.SuppressReply = true;

            return ResultCode.Success;
        }

        [CommandCmif(6)]
        // Recv(s32 descriptor, s32 flags, out s32 errorCode, out s64 receivedSize, buffer<bytes>)
        public ResultCode Recv(ServiceCtx context)
        {
            int descriptor = context.RequestData.ReadInt32();
            int flags = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor, flags });

            // Recv also blocks forever when no host is connected
            context.SuppressReply = true;

            return ResultCode.Success;
        }

        [CommandCmif(7)]
        // Send(s32 descriptor, s32 flags, buffer<bytes>, out s32 errorCode, out s64 sentSize)
        public ResultCode Send(ServiceCtx context)
        {
            int descriptor = context.RequestData.ReadInt32();
            int flags = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor, flags });

            context.ResponseData.Write(-1);  // errorCode = disconnected
            context.ResponseData.Write(0);   // padding
            context.ResponseData.Write(0L);  // sentSize

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

            context.ResponseData.Write(0); // errorCode = success

            MakeObject(context, new ISocket());

            return ResultCode.Success;
        }

        [CommandCmif(13)]
        // CreateSocket(bool enableDisconnectionEmulation, out s32 errorCode, out object<ISocket>)
        public ResultCode CreateSocket(ServiceCtx context)
        {
            bool enableDisconnectionEmulation = context.RequestData.ReadBoolean();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { enableDisconnectionEmulation });

            context.ResponseData.Write(0); // errorCode = success

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
