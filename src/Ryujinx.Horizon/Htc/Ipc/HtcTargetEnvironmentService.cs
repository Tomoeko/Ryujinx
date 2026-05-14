using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Htc;
using Ryujinx.Horizon.Sdk.Sf;
using LibHac.Common.FixedArrays;
using LibHac.Util;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;
using System.Xml.Linq;
using System.Text;

namespace Ryujinx.Horizon.Htc.Ipc
{
    struct HtcTenvVariableName
    {
        public Array64<byte> Value;
    }

    partial class HtcTargetEnvironmentService : IHtcTargetEnvironmentService
    {
        [CmifCommand(0)]
        public Result GetVariable(out long size, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<byte> outBuf, HtcTenvVariableName name)
        {
            var str = Encoding.ASCII.GetString(name.Value.ItemsRo);

            if (str.StartsWith("AssertionFailureAbort\0"))
            {
                size = Encoding.ASCII.GetByteCount("true\0");
                Encoding.ASCII.GetBytes("true\0").CopyTo(outBuf);
                return Result.Success;
            }
            else if (str.StartsWith("ForceExitWhenAbort\0"))
            {
                size = Encoding.ASCII.GetByteCount("true\0");
                Encoding.ASCII.GetBytes("true\0").CopyTo(outBuf);
                return Result.Success;
            }
            else if (str.StartsWith("RomLaunchMode\0"))
            {
                //size = Encoding.ASCII.GetByteCount("Launcher\0");
                //Encoding.ASCII.GetBytes("Launcher\0").CopyTo(outBuf);
                //return Result.Success;
            }

            size = 0;
            return new Result(18, 2);

        }


        [CmifCommand(1)]
        public Result GetVariableLength(out long size, HtcTenvVariableName name)
        {
            var str = Encoding.ASCII.GetString(name.Value.ItemsRo);
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
        public Result WaitUntilVariableAvailable()
        {
            return Result.Success;
        }


    }
}
