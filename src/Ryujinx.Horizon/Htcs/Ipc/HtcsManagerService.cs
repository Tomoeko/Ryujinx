using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Htcs;
using Ryujinx.Horizon.Sdk.Sf;
using LibHac.Common.FixedArrays;
using LibHac.Util;
using System;
using Ryujinx.Horizon.Sdk.OsTypes;
using System.Threading;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System.Collections.Generic;

namespace Ryujinx.Horizon.Htcs.Ipc
{
    struct HtcsPeerName
    {
        public Array32< char> Value;
    }

    partial class HtcsManagerService : IHtcsManagerService, IDisposable
    {
        private int _disposalState;
        private int _taskId;
        private int _socketId;

        public SystemEventType SystemEvent;
        public int EventHandle;
        private readonly List<SystemEventType> _SelectEvents;
        private readonly List<int> _FinalizedEvents;

        public HtcsManagerService()
        {
            Os.CreateSystemEvent(out SystemEvent, EventClearMode.ManualClear, true).AbortOnFailure();

            EventHandle = Os.GetReadableHandleOfSystemEvent(ref SystemEvent);
            _taskId = 0;
            _socketId = 3;
            _SelectEvents = new List<SystemEventType>();
            _FinalizedEvents = new List<int>();
        }

        public void Dispose()
        {
            if (EventHandle != 0 && Interlocked.Exchange(ref _disposalState, 1) == 0)
            {
                Os.DestroySystemEvent(ref SystemEvent);
            }
        }

        [CmifCommand(10)]
        public Result GetPeerNameAny(out HtcsPeerName pn)
        {
            pn = new HtcsPeerName { };
            return Result.Success;
        }
        [CmifCommand(11)]
        public Result GetDefaultHostName(out HtcsPeerName pn)
        {
            pn = new HtcsPeerName { };
            return Result.Success;
        }
        [CmifCommand(13)]
        public Result CreateSocket(out int err, out ISocketService srv, bool enableDisconnectionEmulation)
        {
            err = 0;
            srv = new HtcsSocketService(_socketId++);
            return Result.Success;
        }

        [CmifCommand(100)]
        public Result Initialize([ClientProcessId] ulong clientProcessId)
        {

            return Result.Success;
        }

        [CmifCommand(101)]
        public Result MonitorManager([ClientProcessId] ulong clientProcessId)
        {

            return Result.Success;
        }

        [CmifCommand(130)]
        public Result StartSelect(out int taskId, [CopyHandle] out int readableHandle, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<uint> read_handles, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<uint> write_handles, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<uint> exception_handles, long tv_sec, long tv_usec)
        {
            taskId = _taskId++;
            readableHandle = EventHandle;
            Os.SignalSystemEvent(ref SystemEvent);
            if (tv_sec != 0 || tv_usec != 0)
            {
                readableHandle = 0;
            }

            return Result.Success;
        }

        [CmifCommand(131)]
        public Result EndSelect(out int err, out int count, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] ReadOnlySpan<uint> read_handles, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] ReadOnlySpan<uint> write_handles, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] ReadOnlySpan<uint> exception_handles, int taskId)
        {
            err = 0;
            count = 0;
            Os.ClearSystemEvent(ref SystemEvent);

            return Result.Success;
        }

    }
}
