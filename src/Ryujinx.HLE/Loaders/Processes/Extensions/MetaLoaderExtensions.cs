using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Loader;
using LibHac.Util;
using Ryujinx.Common;
using System;

namespace Ryujinx.HLE.Loaders.Processes.Extensions
{
    public static class MetaLoaderExtensions
    {
        public static ulong GetProgramId(this MetaLoader metaLoader)
        {
            metaLoader.GetNpdm(out var npdm).ThrowIfFailure();

            return npdm.Aci.ProgramId.Value;
        }

        public static string GetProgramName(this MetaLoader metaLoader)
        {
            metaLoader.GetNpdm(out var npdm).ThrowIfFailure();

            return StringUtils.Utf8ZToString(npdm.Meta.ProgramName);
        }

        public static bool IsProgram64Bit(this MetaLoader metaLoader)
        {
            metaLoader.GetNpdm(out var npdm).ThrowIfFailure();

            return (npdm.Meta.Flags & 1) != 0;
        }

        public static void LoadDefault(this MetaLoader metaLoader)
        {
            byte[] npdmBuffer = EmbeddedResources.Read("Ryujinx.HLE/Homebrew.npdm");

            metaLoader.Load(npdmBuffer).ThrowIfFailure();
        }

        public static void LoadFromFile(this MetaLoader metaLoader, IFileSystem fileSystem, string path = "")
        {
            if (string.IsNullOrEmpty(path))
            {
                path = ProcessConst.MainNpdmPath;
            }

            using var npdmFile = new UniqueRef<IFile>();

            fileSystem.OpenFile(ref npdmFile.Ref, path.ToU8Span(), OpenMode.Read).ThrowIfFailure();

            npdmFile.Get.GetSize(out long fileSize).ThrowIfFailure();

            Span<byte> npdmBuffer = new byte[fileSize];

            npdmFile.Get.Read(out _, 0, npdmBuffer).ThrowIfFailure();

            // Patch NPDM to have full filesystem permission
            // This fixes proto ability to mount host filesystem
            var acidOffset = BitConverter.ToInt32(npdmBuffer[0x78..0x7C]);
            var aciOffset = BitConverter.ToInt32(npdmBuffer[0x70..0x74]);
            if (BitConverter.ToUInt32(npdmBuffer[(acidOffset+0x200)..(acidOffset+0x204)]) == 0x44494341)
            {
                var o = BitConverter.ToInt32(npdmBuffer[(acidOffset + 0x220)..(acidOffset + 0x224)]);
                if (npdmBuffer[acidOffset+o] == 1)
                {
                    for (var i = 0; i < 8; ++i)
                        npdmBuffer[acidOffset + o + 4 + i] = 0xFF;
                }
            }
            if (BitConverter.ToUInt32(npdmBuffer[(aciOffset)..(aciOffset + 0x4)]) == 0x30494341)
            {
                var o = BitConverter.ToInt32(npdmBuffer[(aciOffset + 0x20)..(aciOffset + 0x24)]);
                if (npdmBuffer[aciOffset + o] == 1)
                {
                    for (var i = 0; i < 8; ++i)
                        npdmBuffer[aciOffset + o + 4 + i] = 0xFF;
                }
            }

            metaLoader.Load(npdmBuffer).ThrowIfFailure();
        }
    }
}
