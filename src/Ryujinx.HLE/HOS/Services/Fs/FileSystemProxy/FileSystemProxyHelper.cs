using LibHac;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Spl;
using LibHac.Tools.Es;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Path = System.IO.Path;
using System.Text;
using System.Linq;
using System.Security.Cryptography;

namespace Ryujinx.HLE.HOS.Services.Fs.FileSystemProxy
{
    static class FileSystemProxyHelper
    {
        public static ResultCode OpenNsp(ServiceCtx context, string pfsPath, out IFileSystem openedFileSystem)
        {
            openedFileSystem = null;

            try
            {
                LocalStorage storage = new(pfsPath, FileAccess.Read, FileMode.Open);
                var pfs = new PartitionFileSystem();
                using SharedRef<LibHac.Fs.Fsa.IFileSystem> nsp = new(pfs);
                pfs.Initialize(storage).ThrowIfFailure();

                ImportTitleKeysFromNsp(nsp.Get, context.Device.System.KeySet);

                using SharedRef<LibHac.FsSrv.Sf.IFileSystem> adapter = FileSystemInterfaceAdapter.CreateShared(ref nsp.Ref, true);

                openedFileSystem = new IFileSystem(ref adapter.Ref);
            }
            catch (HorizonResultException ex)
            {
                return (ResultCode)ex.ResultValue.Value;
            }

            return ResultCode.Success;
        }

        public static ResultCode OpenNcaFs(ServiceCtx context, string ncaPath, LibHac.Fs.IStorage ncaStorage, out IFileSystem openedFileSystem)
        {
            openedFileSystem = null;

            try
            {
                Nca nca = new(context.Device.System.KeySet, ncaStorage);

                if (!nca.SectionExists(NcaSectionType.Data))
                {
                    return ResultCode.PartitionNotFound;
                }

                LibHac.Fs.Fsa.IFileSystem fileSystem = nca.OpenFileSystem(NcaSectionType.Data, context.Device.System.FsIntegrityCheckLevel);
                using var sharedFs = new SharedRef<LibHac.Fs.Fsa.IFileSystem>(fileSystem);

                using SharedRef<LibHac.FsSrv.Sf.IFileSystem> adapter = FileSystemInterfaceAdapter.CreateShared(ref sharedFs.Ref, true);

                openedFileSystem = new IFileSystem(ref adapter.Ref);
            }
            catch (HorizonResultException ex)
            {
                return (ResultCode)ex.ResultValue.Value;
            }

            return ResultCode.Success;
        }

        public static ResultCode OpenFileSystemFromInternalFile(ServiceCtx context, string fullPath, out IFileSystem openedFileSystem)
        {
            openedFileSystem = null;

            DirectoryInfo archivePath = new DirectoryInfo(fullPath).Parent;

            while (string.IsNullOrWhiteSpace(archivePath.Extension))
            {
                archivePath = archivePath.Parent;
            }

            if (archivePath.Extension == ".nsp" && File.Exists(archivePath.FullName))
            {
                FileStream pfsFile = new(
                    archivePath.FullName.TrimEnd(Path.DirectorySeparatorChar),
                    FileMode.Open,
                    FileAccess.Read);

                try
                {
                    PartitionFileSystem nsp = new();
                    nsp.Initialize(pfsFile.AsStorage()).ThrowIfFailure();

                    ImportTitleKeysFromNsp(nsp, context.Device.System.KeySet);

                    string filename = fullPath.Replace(archivePath.FullName, string.Empty).TrimStart('\\');

                    using var ncaFile = new UniqueRef<LibHac.Fs.Fsa.IFile>();

                    Result result = nsp.OpenFile(ref ncaFile.Ref, filename.ToU8Span(), OpenMode.Read);
                    if (result.IsFailure())
                    {
                        return (ResultCode)result.Value;
                    }

                    return OpenNcaFs(context, fullPath, ncaFile.Release().AsStorage(), out openedFileSystem);
                }
                catch (HorizonResultException ex)
                {
                    return (ResultCode)ex.ResultValue.Value;
                }
            }

            return ResultCode.PathDoesNotExist;
        }

        public static void ImportTitleKeysFromNsp(LibHac.Fs.Fsa.IFileSystem nsp, KeySet keySet)
        {
            foreach (DirectoryEntryEx ticketEntry in nsp.EnumerateEntries("/", "*.tik"))
            {
                using var ticketFile = new UniqueRef<LibHac.Fs.Fsa.IFile>();

                Result result = nsp.OpenFile(ref ticketFile.Ref, ticketEntry.FullPath.ToU8Span(), OpenMode.Read);

                if (result.IsSuccess())
                {
                    Ticket ticket = new(ticketFile.Get.AsStream());
                    var titleKey = ticket.GetTitleKey(keySet);

                    if (titleKey != null)
                    {
                        keySet.ExternalKeySet.Add(new RightsId(ticket.RightsId), new AccessKey(titleKey));
                    }
                }
            }

            foreach (DirectoryEntryEx ticketEntry in nsp.EnumerateEntries("/", "*.tikenc"))
            {
                using var ticketFile = new UniqueRef<LibHac.Fs.Fsa.IFile>();

                Result result = nsp.OpenFile(ref ticketFile.Ref, ticketEntry.FullPath.ToU8Span(), OpenMode.Read);

                if (result.IsSuccess())
                {
                    var tikEncStream = ticketFile.Get.AsStream();
                    var tikEncBytes = new byte[tikEncStream.Length];
                    tikEncStream.Read(tikEncBytes);

                    var ticketBytes = new byte[tikEncBytes.Length - 0x80];

                    if (DecryptTicket(ticketBytes, tikEncBytes))
                    {
                        using (var ms = new MemoryStream(ticketBytes, 0x20, ticketBytes.Length - 0x20))
                        {
                            Ticket ticket = new(ms);
                            var titleKey = ticket.GetTitleKey(keySet);

                            if (titleKey != null)
                            {
                                keySet.ExternalKeySet.Add(new RightsId(ticket.RightsId), new AccessKey(titleKey));
                            }
                        }
                    }
                }
            }
        }

        public static bool DecryptTicket(byte[] dec, byte[] enc)
        {
            // From hayabusa-builds-passwd.pdf
            // This would ideally be prompted from user
            var passphrases = new[] {
                "suikakamakiriringo0827",
                "mijinkokohakukuri0729",
                "uguisusunenezumi0630",
                "inagogomamanbou0602",
                "butatanukikinmedai0430",
                "koararakkokonbu0401",
                "ikakamemendako0225",
                "@ns0KZ3cGZmo",
                "Al1p97ThIJ@2",
            };

            foreach (var passphrase in passphrases)
            {
                if (DecryptTicket(dec, enc, Encoding.ASCII.GetBytes(passphrase)))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool DecryptTicket(byte[] dec, byte[] enc, byte[] passphrase)
        {
            var version = enc[0];
            var keyType = enc[1];
            var keySize = BitConverter.ToInt32(enc, 0x18);
            if (enc[0] != 1)
                return false;
            switch (keyType)
            {
                case 0: // AES-128
                    if (keySize != 16)
                        return false;
                    break;
                case 1: // AES-256
                    if (keySize != 16)
                        return false;
                    // aes-256 not implemented
                    return false;
                default:
                    return false;
            }

            // ??
            if (enc[0x1c] != 0 || enc[0x1d] != 1)
                return false;

            // Check length
            if (BitConverter.ToInt32(enc, 4) != dec.Length)
                return false;

            if (dec.Length < 32)
                return false;

            var keyBytes = new byte[0x20];
            var saltBytes = new byte[BitConverter.ToInt16(enc, 0x1E)];
            Buffer.BlockCopy(enc, 0x24, saltBytes, 0, saltBytes.Length);

            using (var deriver = new Rfc2898DeriveBytes(passphrase, saltBytes, BitConverter.ToInt32(enc, 0x20), HashAlgorithmName.SHA256))
            {
                keyBytes = deriver.GetBytes(0x20);
            }

            LibHac.Crypto.Aes.DecryptCtr128(enc.AsSpan(0x80, dec.Length), dec, keyBytes.AsSpan(0, 0x10), new byte[0x10]);

            var calcHash = new byte[0x20];
            LibHac.Crypto.Sha256.GenerateSha256Hash(dec.AsSpan(0x20), calcHash);
            return Enumerable.SequenceEqual(calcHash, dec.Take(0x20));
        }

        public static ref readonly FspPath GetFspPath(ServiceCtx context, int index = 0)
        {
            ulong position = context.Request.PtrBuff[index].Position;
            ulong size = context.Request.PtrBuff[index].Size;

            ReadOnlySpan<byte> buffer = context.Memory.GetSpan(position, (int)size);
            ReadOnlySpan<FspPath> fspBuffer = MemoryMarshal.Cast<byte, FspPath>(buffer);

            return ref fspBuffer[0];
        }

        public static ref readonly LibHac.FsSrv.Sf.Path GetSfPath(ServiceCtx context, int index = 0)
        {
            ulong position = context.Request.PtrBuff[index].Position;
            ulong size = context.Request.PtrBuff[index].Size;

            ReadOnlySpan<byte> buffer = context.Memory.GetSpan(position, (int)size);
            ReadOnlySpan<LibHac.FsSrv.Sf.Path> pathBuffer = MemoryMarshal.Cast<byte, LibHac.FsSrv.Sf.Path>(buffer);

            return ref pathBuffer[0];
        }
    }
}
