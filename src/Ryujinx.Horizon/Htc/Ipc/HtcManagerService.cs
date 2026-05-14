using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Htc;
using Ryujinx.Horizon.Sdk.Sf;
using LibHac.Common.FixedArrays;
using LibHac.Util;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;
using System.Xml.Linq;
using System.Text;
using Ryujinx.Horizon.Sdk.OsTypes;
using System.Collections.Generic;
using System.Threading;

namespace Ryujinx.Horizon.Htc.Ipc
{

    partial class HtcManagerService : IHtcManagerService, IDisposable
    {
        private int _disposalState;
        public SystemEventType SystemEvent;
        public int EventHandle;

        public HtcManagerService()
        {
            Os.CreateSystemEvent(out SystemEvent, EventClearMode.ManualClear, true).AbortOnFailure();

            EventHandle = Os.GetReadableHandleOfSystemEvent(ref SystemEvent);
        }

        public void Dispose()
        {
            //if (EventHandle != 0 && Interlocked.Exchange(ref _disposalState, 1) == 0)
            //{
            //    Os.DestroySystemEvent(ref SystemEvent);
            //}
        }


        [CmifCommand(0)]
        public Result GetEnvironmentVariable(out int size, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<byte> outBuf, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] Span<byte> name)
        {
            var str = Encoding.ASCII.GetString(name.ToArray());

            switch (str)
            {
                case "AppData\0":
                    size = Encoding.ASCII.GetByteCount("C:\\AppData\0");
                    Encoding.ASCII.GetBytes("C:\\AppData\0").CopyTo(outBuf);
                    return Result.Success;
                default:
                    size = 0;
                    return new Result(18, 2);
            }

        }


        [CmifCommand(1)]
        public Result GetEnvironmentVariableLength(out int size, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] Span<byte> name)
        {
            var str = Encoding.ASCII.GetString(name.ToArray());
            switch (str)
            {
                case "NINTENDO_SDK_ROOT\0":
                    size = 0;
                    return Result.Success;
                default:
                    size = 0;
                    return new Result(18, 2);
            }
        }

        [CmifCommand(2)]
        public Result BindHostConnectionEvent([CopyHandle] out int readableHandle)
        {
            readableHandle = EventHandle;
            Os.SignalSystemEvent(ref SystemEvent);

            return Result.Success;
        }


    }
}
