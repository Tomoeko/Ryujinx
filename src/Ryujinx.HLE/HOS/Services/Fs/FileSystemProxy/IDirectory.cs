using LibHac;
using LibHac.Common;
using LibHac.Sf;
using Ryujinx.Common.Logging;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Fs.FileSystemProxy
{
    class IDirectory : DisposableIpcService
    {
        private SharedRef<LibHac.FsSrv.Sf.IDirectory> _baseDirectory;

        public IDirectory(ref SharedRef<LibHac.FsSrv.Sf.IDirectory> directory)
        {
            _baseDirectory = SharedRef<LibHac.FsSrv.Sf.IDirectory>.CreateMove(ref directory);
        }

        [CommandCmif(0)]
        // Read() -> (u64 count, buffer<nn::fssrv::sf::IDirectoryEntry, 6, 0> entries)
        public ResultCode Read(ServiceCtx context)
        {
            ulong bufferAddress = context.Request.ReceiveBuff[0].Position;
            ulong bufferLen = context.Request.ReceiveBuff[0].Size;

            using var region = context.Memory.GetWritableRegion(bufferAddress, (int)bufferLen, true);
            Result result = _baseDirectory.Get.Read(out long entriesRead, new OutBuffer(region.Memory.Span));

            // Diagnostic: log each returned directory entry
            if (result.IsSuccess() && entriesRead > 0)
            {
                // DirectoryEntry is 0x310 bytes: name[769] + pad[3] + type[1] + pad[3] + fileSize[8]
                const int EntrySize = 0x310;
                var span = region.Memory.Span;
                for (int i = 0; i < (int)entriesRead && (i + 1) * EntrySize <= span.Length; i++)
                {
                    var entrySpan = span.Slice(i * EntrySize, EntrySize);
                    // Read name (null-terminated within 769 bytes)
                    int nameEnd = entrySpan.Slice(0, 769).IndexOf((byte)0);
                    if (nameEnd < 0) nameEnd = 769;
                    string entryName = Encoding.UTF8.GetString(entrySpan.Slice(0, nameEnd));
                    // Read type at offset 0x304
                    int entryType = entrySpan[0x304];
                    // Read fileSize at offset 0x308
                    long fileSize = MemoryMarshal.Read<long>(entrySpan.Slice(0x308));
                    Logger.Warning?.Print(LogClass.ServiceFs,
                        $"ReadDirectory entry[{i}]: name=\"{entryName}\" type={entryType} (0=Dir,1=File) fileSize={fileSize}");
                }
            }

            context.ResponseData.Write(entriesRead);

            return (ResultCode)result.Value;
        }

        [CommandCmif(1)]
        // GetEntryCount() -> u64
        public ResultCode GetEntryCount(ServiceCtx context)
        {
            Result result = _baseDirectory.Get.GetEntryCount(out long entryCount);

            context.ResponseData.Write(entryCount);

            return (ResultCode)result.Value;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                _baseDirectory.Destroy();
            }
        }
    }
}
