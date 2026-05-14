using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Htcs;
using Ryujinx.Horizon.Sdk.Sf;
using LibHac.Common.FixedArrays;
using LibHac.Util;
using System;
using Ryujinx.Horizon.Sdk.OsTypes;
using System.Threading;

namespace Ryujinx.Horizon.Htcs.Ipc
{

    partial class HtcsSocketService : ISocketService, IDisposable
    {
        private int _disposalState;
        private readonly int _desc;

        public SystemEventType SystemEvent;
        public int EventHandle;

        public HtcsSocketService(int desc)
        {
            _desc = desc;
            Os.CreateSystemEvent(out SystemEvent, EventClearMode.ManualClear, true).AbortOnFailure();

            EventHandle = Os.GetReadableHandleOfSystemEvent(ref SystemEvent);
        }

        public void Dispose()
        {
            if (EventHandle != 0 && Interlocked.Exchange(ref _disposalState, 1) == 0)
            {
                Os.DestroySystemEvent(ref SystemEvent);
            }
        }

        [CmifCommand(0)]
        public Result Close(out int err, out int res)
        {
            err = 0;
            res = 0;
            return Result.Success;
        }
        [CmifCommand(1)]
        public Result Connect(out int err, out int res)
        {
            err = 3;
            res = -1;
            return Result.Success;
        }
        [CmifCommand(2)]
        public Result Bind(out int err, out int res)
        {
            err = 0;
            res = 0;
            return Result.Success;
        }

        [CmifCommand(3)]
        public Result Listen(out int err, out int res)
        {
            err = 0;
            res = 0;
            return Result.Success;
        }

        [CmifCommand(8)]
        public Result Fcntl(out int err, out int res)
        {
            err = 0;
            res = 0;
            return Result.Success;
        }

        [CmifCommand(9)]
        public Result AcceptStart(out int taskId, [CopyHandle] out int readableHandle)
        {
            taskId = _desc;
            readableHandle = EventHandle;
            return Result.Success;
        }

        [CmifCommand(130)]
        public Result GetPrimitive(out int desc)
        {
            desc = _desc;
            return Result.Success;
        }
    }
}
