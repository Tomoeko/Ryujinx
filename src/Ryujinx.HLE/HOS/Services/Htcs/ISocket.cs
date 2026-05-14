using Ryujinx.Common.Logging;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Htcs
{
    class ISocket : IpcService
    {
        // On real hardware, socket operations work normally.
        // Only Accept/Recv block waiting for a host connection.

        public ISocket() { }

        [CommandCmif(0)]
        // Close(out s32 errorCode, out s32 result)
        public ResultCode Close(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHtcs);

            context.ResponseData.Write(0);  // errorCode
            context.ResponseData.Write(0);  // result

            return ResultCode.Success;
        }

        [CommandCmif(1)]
        // Connect(SockAddrHtcs address, out s32 errorCode, out s32 result)
        public ResultCode Connect(ServiceCtx context)
        {
            byte[] address = context.RequestData.ReadBytes(68);

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs);

            // Connect blocks until peer is available; return disconnected
            context.ResponseData.Write(-1);  // errorCode = disconnected
            context.ResponseData.Write(-1);  // result

            return ResultCode.Success;
        }

        [CommandCmif(2)]
        // Bind(SockAddrHtcs address, out s32 errorCode, out s32 result)
        public ResultCode Bind(ServiceCtx context)
        {
            byte[] address = context.RequestData.ReadBytes(68);

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs);

            // Bind succeeds on real hardware
            context.ResponseData.Write(0);  // errorCode = success
            context.ResponseData.Write(0);  // result = success

            return ResultCode.Success;
        }

        [CommandCmif(3)]
        // Listen(s32 backlogCount, out s32 errorCode, out s32 result)
        public ResultCode Listen(ServiceCtx context)
        {
            int backlogCount = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { backlogCount });

            // Listen succeeds on real hardware
            context.ResponseData.Write(0);  // errorCode = success
            context.ResponseData.Write(0);  // result = success

            return ResultCode.Success;
        }

        [CommandCmif(4)]
        // Accept(out s32 errorCode, out object<ISocket>, out SockAddrHtcs address)
        public ResultCode Accept(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHtcs);

            // On real hardware, Accept blocks forever waiting for a host connection.
            // The hostio thread parks here until a Target Manager connects.
            Thread.Sleep(Timeout.Infinite);

            // Unreachable
            context.ResponseData.Write(-1);           // errorCode
            context.ResponseData.Write(new byte[68]); // SockAddrHtcs
            MakeObject(context, new ISocket());

            return ResultCode.Success;
        }

        [CommandCmif(5)]
        // Recv(s32 flags, out s32 errorCode, out s64 receivedSize, buffer<bytes>)
        public ResultCode Recv(ServiceCtx context)
        {
            int flags = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { flags });

            // Recv blocks waiting for data from host
            Thread.Sleep(Timeout.Infinite);

            context.ResponseData.Write(-1);  // errorCode
            context.ResponseData.Write(0);   // padding
            context.ResponseData.Write(0L);  // receivedSize

            return ResultCode.Success;
        }

        [CommandCmif(6)]
        // Send(s32 flags, buffer<bytes>, out s32 errorCode, out s64 sentSize)
        public ResultCode Send(ServiceCtx context)
        {
            int flags = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { flags });

            context.ResponseData.Write(-1);  // errorCode = disconnected
            context.ResponseData.Write(0);   // padding
            context.ResponseData.Write(0L);  // sentSize

            return ResultCode.Success;
        }

        [CommandCmif(7)]
        // Shutdown(s32 how, out s32 errorCode, out s32 result)
        public ResultCode Shutdown(ServiceCtx context)
        {
            int how = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { how });

            context.ResponseData.Write(0);  // errorCode
            context.ResponseData.Write(0);  // result

            return ResultCode.Success;
        }

        [CommandCmif(8)]
        // Fcntl(s32 command, s32 value, out s32 errorCode, out s32 result)
        public ResultCode Fcntl(ServiceCtx context)
        {
            int command = context.RequestData.ReadInt32();
            int value = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceHtcs, new { command, value });

            context.ResponseData.Write(0);  // errorCode
            context.ResponseData.Write(0);  // result

            return ResultCode.Success;
        }
    }
}
