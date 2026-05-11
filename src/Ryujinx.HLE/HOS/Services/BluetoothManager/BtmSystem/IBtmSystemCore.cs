using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.Horizon.Common;

namespace Ryujinx.HLE.HOS.Services.BluetoothManager.BtmSystem
{
    class IBtmSystemCore : IpcService
    {
        public KEvent _radioEvent;
        public int    _radioEventhandle;

        public KEvent _gamepadPairingEvent;
        public int    _gamepadPairingEventHandle;

        public KEvent _audioDeviceConnectionEvent;
        public int    _audioDeviceConnectionEventHandle;

        public IBtmSystemCore() { }

        [CommandCmif(6)]
        // IsRadioEnabled() -> b8
        public ResultCode IsRadioEnabled(ServiceCtx context)
        {
            context.ResponseData.Write(true);

            Logger.Stub?.PrintStub(LogClass.ServiceBtm);

            return ResultCode.Success;
        }

        [CommandCmif(7)] // 3.0.0+
        // AcquireRadioEvent() -> (byte<1>, handle<copy>)
        public ResultCode AcquireRadioEvent(ServiceCtx context)
        {
            Result result = Result.Success;

            if (_radioEventhandle == 0)
            {
                _radioEvent = new KEvent(context.Device.System.KernelContext);

                result = context.Process.HandleTable.GenerateHandle(_radioEvent.ReadableEvent, out _radioEventhandle);

                if (result != Result.Success)
                {
                    // NOTE: We use a Logging instead of an exception because the call return a boolean if succeed or not.
                    Logger.Error?.Print(LogClass.ServiceBsd, "Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_radioEventhandle);

            context.ResponseData.Write(result == Result.Success ? 1 : 0);

            return ResultCode.Success;
        }

        [CommandCmif(14)] // 13.0.0+
        // AcquireAudioDeviceConnectionEvent() -> handle<copy>
        public ResultCode AcquireAudioDeviceConnectionEvent(ServiceCtx context)
        {
            if (_audioDeviceConnectionEventHandle == 0)
            {
                _audioDeviceConnectionEvent = new KEvent(context.Device.System.KernelContext);

                if (context.Process.HandleTable.GenerateHandle(_audioDeviceConnectionEvent.ReadableEvent, out _audioDeviceConnectionEventHandle) != Result.Success)
                {
                    Logger.Error?.Print(LogClass.ServiceBtm, "Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_audioDeviceConnectionEventHandle);

            Logger.Stub?.PrintStub(LogClass.ServiceBtm);

            return ResultCode.Success;
        }

        [CommandCmif(8)] // 3.0.0+
        // AcquireGamepadPairingEvent() -> (byte<1>, handle<copy>)
        public ResultCode AcquireGamepadPairingEvent(ServiceCtx context)
        {
            Result result = Result.Success;

            if (_gamepadPairingEventHandle == 0)
            {
                _gamepadPairingEvent = new KEvent(context.Device.System.KernelContext);

                result = context.Process.HandleTable.GenerateHandle(_gamepadPairingEvent.ReadableEvent, out _gamepadPairingEventHandle);

                if (result != Result.Success)
                {
                    // NOTE: We use a Logging instead of an exception because the call return a boolean if succeed or not.
                    Logger.Error?.Print(LogClass.ServiceBsd, "Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_gamepadPairingEventHandle);

            context.ResponseData.Write(result == Result.Success ? 1 : 0);

            return ResultCode.Success;
        }
        [CommandCmif(20)] // 13.0.0+
        // GetPairedAudioDevices() -> (u32, buffer<nn::btm::PairedAudioDeviceInfo, 0x6>)
        public ResultCode GetPairedAudioDevices(ServiceCtx context)
        {
            context.ResponseData.Write(0u); // count

            Logger.Stub?.PrintStub(LogClass.ServiceBtm);

            return ResultCode.Success;
        }
    }
}