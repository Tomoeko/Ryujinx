using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Htcs
{
    [Service("htcs")]
    class IHtcsManager : IpcService
    {
        // On real devkit hardware without a host PC connected:
        // - Socket/Bind/Listen succeed normally (the socket layer works)
        // - Accept blocks forever (waiting for a host connection)
        // - CreateSocket succeeds and returns a valid ISocket
        //
        // We must replicate this behavior. Returning errors from Socket/Bind/Listen
        // causes the game's hostio thread to take error paths that affect shared
        // state (e.g., the DebugMenu Lua item buffer overflows).

        private int _nextDescriptor = 1;

        public IHtcsManager(ServiceCtx context) { }

        [CommandCmif(0)]
        // Socket(out s32 errorCode, out s32 socketDescriptor)
        public ResultCode Socket(ServiceCtx context)
        {
            int descriptor = Interlocked.Increment(ref _nextDescriptor);

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

            // Connect would block on real hw waiting for peer; return disconnected
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

            // Bind always succeeds on real hardware
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

            // Listen always succeeds on real hardware
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

            // On real hardware, Accept blocks forever waiting for a host connection.
            // We simulate this by sleeping indefinitely. The guest thread will be
            // parked here, matching real devkit behavior when no host is connected.
            // The thread will be cleaned up when the process terminates.
            Thread.Sleep(Timeout.Infinite);

            // Unreachable, but needed for compilation
            context.ResponseData.Write(new byte[68]); // SockAddrHtcs
            context.ResponseData.Write(-1);            // errorCode
            context.ResponseData.Write(-1);            // result

            return ResultCode.Success;
        }

        [CommandCmif(6)]
        // Recv(s32 descriptor, s32 flags, out s32 errorCode, out s64 receivedSize, buffer<bytes>)
        public ResultCode Recv(ServiceCtx context)
        {
            int descriptor = context.RequestData.ReadInt32();
            int flags = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { descriptor, flags });

            // Block like Accept — recv on a hostio socket blocks until data arrives
            Thread.Sleep(Timeout.Infinite);

            context.ResponseData.Write(-1);  // errorCode
            context.ResponseData.Write(0);   // padding
            context.ResponseData.Write(0L);  // receivedSize

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
