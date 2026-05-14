using LibHac.Diag;
using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Arp.Detail;
using Ryujinx.Horizon.Sdk.OsTypes;
using Ryujinx.Horizon.Sdk.Profiler;
using Ryujinx.Horizon.Sdk.Sf;
using System.Threading;
using System;
using LibHac.Bcat;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using Ryujinx.Common.Logging;

namespace Ryujinx.Horizon.Profiler.Ipc
{
    partial class ProfilerImpl : IProfiler, IDisposable
    {
        private int _disposalState;

        public SystemEventType SystemEvent;
        public int EventHandle;
        public uint AffinityMask;
        public uint Flags;
        public uint PerformanceCounterGroup;
        public uint SampleRate;

        private bool _Profiling = false;

        public ProfilerImpl()
        {
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

        [CmifCommand(1)]
        public Result GetSystemEvent([CopyHandle] out int readableHandle)
        {
            readableHandle = EventHandle;
            //Os.SignalSystemEvent(ref SystemEvent);

            return Result.Success;
        }

        [CmifCommand(4)]
        public Result LibraryInitialize(uint sdkVersion, ulong processId)
        {
            return Result.Success;
        }

        [CmifCommand(5)]
        public Result LibraryFinalize(ulong processId)
        {
            return Result.Success;
        }

        [CmifCommand(6)]
        public Result GetProfilerStatus(out int status)
        {
            status = _Profiling ? 2 : 1; // ProfilerStat_Profiling or ProfilerStatus_Active
            return Result.Success;
        }

        [CmifCommand(9)]
        public Result GetIpcEvent([Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<uint> outInfo)
        {
            outInfo[0] = 0; // Status
            outInfo[2] = _Profiling ? 2u : 1u;
            Os.ClearSystemEvent(ref SystemEvent);
            return Result.Success;
        }

        [CmifCommand(11)]
        public Result SendMessageToPcInBuffer(uint id, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] Span<byte> buffer, bool callback)
        {
            return Result.Success;
        }

        [CmifCommand(14)]
        public Result GetProfilerSettings([Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<uint> outBuf)
        {
            outBuf[0] = 0; // Version
            outBuf[1] = Flags | 0x100; // Flags | OutOfProcessProfiling
            outBuf[2] = 1000000000; // RequestedTimeBetweenSamplesInNanoseconds
            for (var i = 3; i < outBuf.Length; i++)
                outBuf[i] = 0; // All other fields
            return Result.Success;
        }

        [CmifCommand(15)]
        public Result SetProfilerSettings(uint affinityMask, uint flags, uint pcG, uint sampleRate)
        {
            AffinityMask = affinityMask;
            Flags = flags;
            PerformanceCounterGroup = pcG;
            SampleRate = sampleRate;
            return Result.Success;
        }

        [CmifCommand(23)]
        public Result SendIpcMessage(uint message, ulong data)
        {
            switch (message)
            {
                case 0: // AllDataSent:
                    break;
                case 1: // CoreMask:
                    break;
                case 2: // StartProfiling:
                    _Profiling = true;
                    Os.SignalSystemEvent(ref SystemEvent);
                    break;
                case 3: // StopProfiling:
                    _Profiling = false;
                    Os.SignalSystemEvent(ref SystemEvent);
                    break;
                case 4: // ForceAttachToInProcess:
                    break;
            }
            return Result.Success;
        }

        [CmifCommand(24)]
        public Result Command24(uint a, ulong b)
        {
            return Result.Success;
        }

        [CmifCommand(25)]
        public Result GetStreamOutPortName([Buffer(HipcBufferFlags.Out | HipcBufferFlags.AutoSelect)] Span<char> entriesBuffer)
        {
            "ProfilerDebug\0".CopyTo(entriesBuffer);
            return Result.Success;
        }


    }
}
