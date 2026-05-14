using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Htc;
using Ryujinx.Horizon.Sdk.Sf;
using LibHac.Common.FixedArrays;
using LibHac.Util;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;
using System.Xml.Linq;
using System.Text;
using Ryujinx.Horizon.Sdk.Htcs;

namespace Ryujinx.Horizon.Htc.Ipc
{

    partial class HtcTargetEnvironmentManagerService : IHtcTargetEnvironmentManagerService
    {
        [CmifCommand(0)]
        public Result GetServiceInterface(out IHtcTargetEnvironmentService srv, [ClientProcessId] ulong clientProcessId)
        {
            srv = new HtcTargetEnvironmentService();


            return Result.Success;
        }


    }
}
