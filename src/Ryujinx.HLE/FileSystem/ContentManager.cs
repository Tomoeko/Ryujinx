using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.Ncm;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.Exceptions;
using Ryujinx.HLE.HOS.Services.Ssl;
using Ryujinx.HLE.HOS.Services.Time;
using Ryujinx.HLE.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Path = System.IO.Path;

namespace Ryujinx.HLE.FileSystem
{
    public class ContentManager
    {
        private const ulong SystemVersionTitleId = 0x0100000000000809;
        private const ulong SystemUpdateTitleId = 0x0100000000000816;

        private Dictionary<StorageId, LinkedList<LocationEntry>> _locationEntries;

        private readonly Dictionary<string, ulong> _sharedFontTitleDictionary;
        private readonly Dictionary<ulong, string> _systemTitlesNameDictionary;
        private readonly Dictionary<string, string> _sharedFontFilenameDictionary;

        private SortedDictionary<(ulong titleId, NcaContentType type), string> _contentDictionary;

        private readonly struct AocItem
        {
            public readonly string ContainerPath;
            public readonly string NcaPath;

            public AocItem(string containerPath, string ncaPath)
            {
                ContainerPath = containerPath;
                NcaPath = ncaPath;
            }
        }

        private SortedList<ulong, AocItem> AocData { get; }

        private readonly VirtualFileSystem _virtualFileSystem;

        // Prod keyset loaded when firmware NCAs are found at the non-dev
        // system path.  Used to decrypt prod-encrypted firmware content.
        private KeySet _prodKeySet;
        public KeySet ProdKeySet => _prodKeySet;

        private readonly object _lock = new();

        public ContentManager(VirtualFileSystem virtualFileSystem)
        {
            _contentDictionary = new SortedDictionary<(ulong, NcaContentType), string>();
            _locationEntries = new Dictionary<StorageId, LinkedList<LocationEntry>>();

            _sharedFontTitleDictionary = new Dictionary<string, ulong>
            {
                { "FontStandard",                  0x0100000000000811 },
                { "FontChineseSimplified",         0x0100000000000814 },
                { "FontExtendedChineseSimplified", 0x0100000000000814 },
                { "FontKorean",                    0x0100000000000812 },
                { "FontChineseTraditional",        0x0100000000000813 },
                { "FontNintendoExtended",          0x0100000000000810 },
            };

            _systemTitlesNameDictionary = new Dictionary<ulong, string>()
            {
                { 0x010000000000080E, "TimeZoneBinary"         },
                { 0x0100000000000810, "FontNintendoExtension"  },
                { 0x0100000000000811, "FontStandard"           },
                { 0x0100000000000812, "FontKorean"             },
                { 0x0100000000000813, "FontChineseTraditional" },
                { 0x0100000000000814, "FontChineseSimple"      },
            };

            _sharedFontFilenameDictionary = new Dictionary<string, string>
            {
                { "FontStandard",                  "nintendo_udsg-r_std_003.bfttf" },
                { "FontChineseSimplified",         "nintendo_udsg-r_org_zh-cn_003.bfttf" },
                { "FontExtendedChineseSimplified", "nintendo_udsg-r_ext_zh-cn_003.bfttf" },
                { "FontKorean",                    "nintendo_udsg-r_ko_003.bfttf" },
                { "FontChineseTraditional",        "nintendo_udjxh-db_zh-tw_003.bfttf" },
                { "FontNintendoExtended",          "nintendo_ext_003.bfttf" },
            };

            _virtualFileSystem = virtualFileSystem;

            AocData = new SortedList<ulong, AocItem>();
        }

        public void LoadEntries(Switch device = null)
        {
            lock (_lock)
            {
                _contentDictionary = new SortedDictionary<(ulong, NcaContentType), string>();
                _locationEntries = new Dictionary<StorageId, LinkedList<LocationEntry>>();

                foreach (StorageId storageId in Enum.GetValues<StorageId>())
                {
                    if (!ContentPath.TryGetContentPath(storageId, out var contentPathString))
                    {
                        continue;
                    }
                    if (!ContentPath.TryGetRealPath(contentPathString, out var contentDirectory))
                    {
                        continue;
                    }
                    var registeredDirectory = Path.Combine(contentDirectory, "registered");

                    Directory.CreateDirectory(registeredDirectory);

                    LinkedList<LocationEntry> locationList = new();

                    void AddEntry(LocationEntry entry)
                    {
                        locationList.AddLast(entry);
                    }

                    // Collect all directories to scan. For BuiltInSystem, if the dev-keyset
                    // registered directory is empty, also scan the non-dev system path as a
                    // fallback.  Firmware NCAs are typically installed under the non-dev path
                    // (bis/system/Contents/registered/) but the dev build looks under
                    // bis/system_dev/Contents/registered/.
                    var directoriesToScan = new System.Collections.Generic.List<string> { registeredDirectory };

                    if (storageId == StorageId.BuiltInSystem
                        && Directory.GetDirectories(registeredDirectory).Length == 0
                        && Directory.GetFiles(registeredDirectory).Length == 0)
                    {
                        // Try the non-dev path
                        string nonDevSystem = Path.Combine(AppDataManager.BaseDirPath,
                            AppDataManager.DefaultNandDir, "system", "Contents", "registered");
                        if (Directory.Exists(nonDevSystem) && nonDevSystem != registeredDirectory)
                        {
                            Logger.Info?.Print(LogClass.Loader,
                                $"Dev-keyset system content directory is empty, falling back to non-dev path: {nonDevSystem}");
                            directoriesToScan.Add(nonDevSystem);
                        }
                    }

                    // When scanning non-dev paths, firmware NCAs are encrypted with prod
                    // keys.  Create a temporary prod-mode keyset for those reads.
                    KeySet prodKeySet = null;
                    string nonDevScanDir = null;
                    if (directoriesToScan.Count > 1)
                    {
                        nonDevScanDir = directoriesToScan[1];
                        prodKeySet = KeySet.CreateDefaultKeySet();
                        // Load prod keys from the same locations the main keyset uses
                        string prodKeyFile = Path.Combine(AppDataManager.KeysDirPath, "prod.keys");
                        if (!File.Exists(prodKeyFile))
                        {
                            prodKeyFile = Path.Combine(AppDataManager.KeysDirPathUser, "prod.keys");
                        }
                        if (File.Exists(prodKeyFile))
                        {
                            ExternalKeyReader.ReadKeyFile(prodKeySet, prodKeyFile, null, null, null);
                            Logger.Info?.Print(LogClass.Loader, "Loaded prod keyset for non-dev firmware NCA decryption");
                        }
                    }

                    foreach (string scanDir in directoriesToScan)
                    {
                        // Use prod keys for NCAs in the non-dev fallback directory
                        KeySet activeKeySet = (scanDir == nonDevScanDir && prodKeySet != null)
                            ? prodKeySet : _virtualFileSystem.KeySet;

                        // For path construction, we need to strip the content directory
                        // (parent of "registered/") to get proper switch paths.
                        // For the non-dev fallback, use the non-dev content directory as base.
                        string pathBase = (scanDir == nonDevScanDir)
                            ? Path.Combine(AppDataManager.BaseDirPath,
                                AppDataManager.DefaultNandDir, "system", "Contents")
                            : contentDirectory;

                        foreach (string directoryPath in Directory.EnumerateDirectories(scanDir))
                        {
                            if (Directory.GetFiles(directoryPath).Length > 0)
                            {
                                string ncaName = new DirectoryInfo(directoryPath).Name.Replace(".nca", string.Empty);

                                try
                                {
                                    using FileStream ncaFile = File.OpenRead(Directory.GetFiles(directoryPath)[0]);
                                    Nca nca = new(activeKeySet, ncaFile.AsStorage());

                                    string switchPath = contentPathString + ":/" + ncaFile.Name.Replace(pathBase, string.Empty).TrimStart(Path.DirectorySeparatorChar);

                                    // Change path format to switch's
                                    switchPath = switchPath.Replace('\\', '/');

                                    LocationEntry entry = new(switchPath, 0, nca.Header.TitleId, nca.Header.ContentType);

                                    AddEntry(entry);

                                    _contentDictionary.TryAdd((nca.Header.TitleId, nca.Header.ContentType), ncaName);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Warning?.Print(LogClass.Loader,
                                        $"Failed to read NCA {ncaName}: {ex.Message}");
                                }
                            }
                        }

                        foreach (string filePath in Directory.EnumerateFiles(scanDir))
                        {
                            if (Path.GetExtension(filePath) == ".nca")
                            {
                                string ncaName = Path.GetFileNameWithoutExtension(filePath);

                                try
                                {
                                    using FileStream ncaFile = new(filePath, FileMode.Open, FileAccess.Read);
                                    Nca nca = new(activeKeySet, ncaFile.AsStorage());

                                    string switchPath = contentPathString + ":/" + filePath.Replace(pathBase, string.Empty).TrimStart(Path.DirectorySeparatorChar);

                                    // Change path format to switch's
                                    switchPath = switchPath.Replace('\\', '/');

                                    LocationEntry entry = new(switchPath, 0, nca.Header.TitleId, nca.Header.ContentType);

                                    AddEntry(entry);

                                    _contentDictionary.TryAdd((nca.Header.TitleId, nca.Header.ContentType), ncaName);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Warning?.Print(LogClass.Loader,
                                        $"Failed to read NCA {ncaName}: {ex.Message}");
                                }
                            }
                        }
                    }

                    // Store prod keyset reference for later content verification/font loading
                    if (prodKeySet != null)
                    {
                        _prodKeySet = prodKeySet;
                    }

                    if (_locationEntries.TryGetValue(storageId, out var locationEntriesItem) && locationEntriesItem?.Count == 0)
                    {
                        _locationEntries.Remove(storageId);
                    }

                    _locationEntries.TryAdd(storageId, locationList);
                }

                if (device != null)
                {
                    TimeManager.Instance.InitializeTimeZone(device);
                    BuiltInCertificateManager.Instance.Initialize(device);
                    device.System.SharedFontManager.Initialize();
                }
            }
        }

        public void AddAocItem(ulong titleId, string containerPath, string ncaPath, bool mergedToContainer = false)
        {
            // TODO: Check Aoc version.
            if (!AocData.TryAdd(titleId, new AocItem(containerPath, ncaPath)))
            {
                Logger.Warning?.Print(LogClass.Application, $"Duplicate AddOnContent detected. TitleId {titleId:X16}");
            }
            else
            {
                Logger.Info?.Print(LogClass.Application, $"Found AddOnContent with TitleId {titleId:X16}");

                if (!mergedToContainer)
                {
                    using var pfs = PartitionFileSystemUtils.OpenApplicationFileSystem(containerPath, _virtualFileSystem);
                }
            }
        }

        public void ClearAocData() => AocData.Clear();

        public int GetAocCount() => AocData.Count;

        public IList<ulong> GetAocTitleIds() => AocData.Select(e => e.Key).ToList();

        public bool GetAocDataStorage(ulong aocTitleId, out IStorage aocStorage, IntegrityCheckLevel integrityCheckLevel)
        {
            aocStorage = null;

            if (AocData.TryGetValue(aocTitleId, out AocItem aoc))
            {
                var file = new FileStream(aoc.ContainerPath, FileMode.Open, FileAccess.Read);
                using var ncaFile = new UniqueRef<IFile>();

                switch (Path.GetExtension(aoc.ContainerPath))
                {
                    case ".xci":
                        var xci = new Xci(_virtualFileSystem.KeySet, file.AsStorage()).OpenPartition(XciPartitionType.Secure);
                        xci.OpenFile(ref ncaFile.Ref, aoc.NcaPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();
                        break;
                    case ".nsp":
                        var pfs = new PartitionFileSystem();
                        pfs.Initialize(file.AsStorage());
                        pfs.OpenFile(ref ncaFile.Ref, aoc.NcaPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();
                        break;
                    default:
                        return false; // Print error?
                }

                aocStorage = new Nca(_virtualFileSystem.KeySet, ncaFile.Get.AsStorage()).OpenStorage(NcaSectionType.Data, integrityCheckLevel);

                return true;
            }

            return false;
        }

        public void ClearEntry(ulong titleId, NcaContentType contentType, StorageId storageId)
        {
            lock (_lock)
            {
                RemoveLocationEntry(titleId, contentType, storageId);
            }
        }

        public void RefreshEntries(StorageId storageId, int flag)
        {
            lock (_lock)
            {
                LinkedList<LocationEntry> locationList = _locationEntries[storageId];
                LinkedListNode<LocationEntry> locationEntry = locationList.First;

                while (locationEntry != null)
                {
                    LinkedListNode<LocationEntry> nextLocationEntry = locationEntry.Next;

                    if (locationEntry.Value.Flag == flag)
                    {
                        locationList.Remove(locationEntry.Value);
                    }

                    locationEntry = nextLocationEntry;
                }
            }
        }

        public bool HasNca(string ncaId, StorageId storageId)
        {
            lock (_lock)
            {
                if (_contentDictionary.ContainsValue(ncaId))
                {
                    var content = _contentDictionary.FirstOrDefault(x => x.Value == ncaId);
                    ulong titleId = content.Key.titleId;

                    NcaContentType contentType = content.Key.type;
                    StorageId storage = GetInstalledStorage(titleId, contentType, storageId);

                    return storage == storageId;
                }
            }

            return false;
        }

        public UInt128 GetInstalledNcaId(ulong titleId, NcaContentType contentType)
        {
            lock (_lock)
            {
                if (_contentDictionary.TryGetValue((titleId, contentType), out var contentDictionaryItem))
                {
                    return UInt128Utils.FromHex(contentDictionaryItem);
                }
            }

            return new UInt128();
        }

        public StorageId GetInstalledStorage(ulong titleId, NcaContentType contentType, StorageId storageId)
        {
            lock (_lock)
            {
                LocationEntry locationEntry = GetLocation(titleId, contentType, storageId);

                return locationEntry.ContentPath != null ? ContentPath.GetStorageId(locationEntry.ContentPath) : StorageId.None;
            }
        }

        public string GetInstalledContentPath(ulong titleId, StorageId storageId, NcaContentType contentType)
        {
            lock (_lock)
            {
                LocationEntry locationEntry = GetLocation(titleId, contentType, storageId);

                if (VerifyContentType(locationEntry, contentType))
                {
                    return locationEntry.ContentPath;
                }
            }

            return string.Empty;
        }

        public void RedirectLocation(LocationEntry newEntry, StorageId storageId)
        {
            lock (_lock)
            {
                LocationEntry locationEntry = GetLocation(newEntry.TitleId, newEntry.ContentType, storageId);

                if (locationEntry.ContentPath != null)
                {
                    RemoveLocationEntry(newEntry.TitleId, newEntry.ContentType, storageId);
                }

                AddLocationEntry(newEntry, storageId);
            }
        }

        private bool VerifyContentType(LocationEntry locationEntry, NcaContentType contentType)
        {
            if (locationEntry.ContentPath == null)
            {
                return false;
            }

            string installedPath = VirtualFileSystem.SwitchPathToSystemPath(locationEntry.ContentPath);

            if (!string.IsNullOrWhiteSpace(installedPath) && File.Exists(installedPath))
            {
                using FileStream file = new(installedPath, FileMode.Open, FileAccess.Read);
                Nca nca = new(_virtualFileSystem.GetKeySetForPath(installedPath), file.AsStorage());
                bool contentCheck = nca.Header.ContentType == contentType;

                return contentCheck;
            }

            return false;
        }

        private void AddLocationEntry(LocationEntry entry, StorageId storageId)
        {
            LinkedList<LocationEntry> locationList = null;

            if (_locationEntries.TryGetValue(storageId, out LinkedList<LocationEntry> locationEntry))
            {
                locationList = locationEntry;
            }

            if (locationList != null)
            {
                locationList.Remove(entry);

                locationList.AddLast(entry);
            }
        }

        private void RemoveLocationEntry(ulong titleId, NcaContentType contentType, StorageId storageId)
        {
            LinkedList<LocationEntry> locationList = null;

            if (_locationEntries.TryGetValue(storageId, out LinkedList<LocationEntry> locationEntry))
            {
                locationList = locationEntry;
            }

            if (locationList != null)
            {
                LocationEntry entry =
                    locationList.ToList().Find(x => x.TitleId == titleId && x.ContentType == contentType);

                if (entry.ContentPath != null)
                {
                    locationList.Remove(entry);
                }
            }
        }

        public bool TryGetFontTitle(string fontName, out ulong titleId)
        {
            return _sharedFontTitleDictionary.TryGetValue(fontName, out titleId);
        }

        public bool TryGetFontFilename(string fontName, out string filename)
        {
            return _sharedFontFilenameDictionary.TryGetValue(fontName, out filename);
        }

        public bool TryGetSystemTitlesName(ulong titleId, out string name)
        {
            return _systemTitlesNameDictionary.TryGetValue(titleId, out name);
        }

        private LocationEntry GetLocation(ulong titleId, NcaContentType contentType, StorageId storageId)
        {
            LinkedList<LocationEntry> locationList = _locationEntries[storageId];

            return locationList.ToList().Find(x => x.TitleId == titleId && x.ContentType == contentType);
        }

        public void InstallFirmware(string firmwareSource)
        {
            ContentPath.TryGetContentPath(StorageId.BuiltInSystem, out var contentPathString);
            ContentPath.TryGetRealPath(contentPathString, out var contentDirectory);
            string registeredDirectory = Path.Combine(contentDirectory, "registered");
            string temporaryDirectory = Path.Combine(contentDirectory, "temp");

            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }

            if (Directory.Exists(firmwareSource))
            {
                InstallFromDirectory(firmwareSource, temporaryDirectory);
                FinishInstallation(temporaryDirectory, registeredDirectory);

                return;
            }

            if (!File.Exists(firmwareSource))
            {
                throw new FileNotFoundException("Firmware file does not exist.");
            }

            FileInfo info = new(firmwareSource);

            using FileStream file = File.OpenRead(firmwareSource);

            switch (info.Extension)
            {
                case ".zip":
                    using (ZipArchive archive = ZipFile.OpenRead(firmwareSource))
                    {
                        InstallFromZip(archive, temporaryDirectory);
                    }
                    break;
                case ".xci":
                    Xci xci = new(_virtualFileSystem.KeySet, file.AsStorage());
                    InstallFromCart(xci, temporaryDirectory);
                    break;
                case ".nsp":
                    var nspPfs = new PartitionFileSystem();
                    nspPfs.Initialize(file.AsStorage()).ThrowIfFailure();
                    _virtualFileSystem.ImportTickets(nspPfs);
                    InstallFromNsp(nspPfs, temporaryDirectory);
                    break;
                default:
                    throw new InvalidFirmwarePackageException("Input file is not a valid firmware package");
            }

            FinishInstallation(temporaryDirectory, registeredDirectory);
        }

        private void FinishInstallation(string temporaryDirectory, string registeredDirectory)
        {
            if (Directory.Exists(registeredDirectory))
            {
                new DirectoryInfo(registeredDirectory).Delete(true);
            }

            Directory.Move(temporaryDirectory, registeredDirectory);

            LoadEntries();
        }

        private void InstallFromDirectory(string firmwareDirectory, string temporaryDirectory)
        {
            InstallFromPartition(new LocalFileSystem(firmwareDirectory), temporaryDirectory);
        }

        private void InstallFromPartition(IFileSystem filesystem, string temporaryDirectory)
        {
            foreach (var entry in filesystem.EnumerateEntries("/", "*.nca"))
            {
                Nca nca = new(_virtualFileSystem.KeySet, OpenPossibleFragmentedFile(filesystem, entry.FullPath, OpenMode.Read).AsStorage());

                SaveNca(nca, entry.Name.Remove(entry.Name.IndexOf('.')), temporaryDirectory);
            }
        }

        private void InstallFromCart(Xci gameCard, string temporaryDirectory)
        {
            if (gameCard.HasPartition(XciPartitionType.Update))
            {
                XciPartition partition = gameCard.OpenPartition(XciPartitionType.Update);

                InstallFromPartition(partition, temporaryDirectory);
            }
            else
            {
                throw new Exception("Update not found in xci file.");
            }
        }

        private void InstallFromNsp(PartitionFileSystem nspFs, string temporaryDirectory)
        {
            // Count NCAs in the outer NSP
            var ncaEntries = nspFs.EnumerateEntries("/", "*.nca").ToList();

            if (ncaEntries.Count > 10)
            {
                // This NSP directly contains firmware NCAs — install them
                Logger.Info?.Print(LogClass.ServiceFs,
                    $"NSP contains {ncaEntries.Count} NCAs, installing directly as firmware package.");
                InstallFromPartition(nspFs, temporaryDirectory);
                return;
            }

            // This is a firmware updater NSP — firmware NCAs are nested inside
            // the largest NCA's data section as an inner PFS0.
            Logger.Info?.Print(LogClass.ServiceFs,
                $"NSP contains {ncaEntries.Count} NCAs — searching for nested firmware content.");

            foreach (var entry in ncaEntries)
            {
                IStorage ncaStorage = OpenPossibleFragmentedFile(nspFs, entry.FullPath, OpenMode.Read).AsStorage();

                try
                {
                    Nca nca = new(_virtualFileSystem.KeySet, ncaStorage);

                    Logger.Info?.Print(LogClass.ServiceFs,
                        $"NCA: {entry.Name} Type={nca.Header.ContentType} TitleId={nca.Header.TitleId:X16}");

                    // Try each section type to find the inner firmware PFS0
                    NcaSectionType[] sectionTypes = { NcaSectionType.Data, NcaSectionType.Code };

                    foreach (var sectionType in sectionTypes)
                    {
                        try
                        {
                            IFileSystem innerFs = nca.OpenFileSystem(sectionType, IntegrityCheckLevel.None);

                            var allFiles = innerFs.EnumerateEntries("/", "*").ToList();

                            // Check for .initimg files (dev firmware init images containing NCAs as PFS0)
                            var initImgEntries = allFiles.Where(e => e.Name.EndsWith(".initimg")).ToList();
                            foreach (var initImgEntry in initImgEntries)
                            {
                                Logger.Info?.Print(LogClass.ServiceFs,
                                    $"Found init image: {initImgEntry.FullPath} ({initImgEntry.Size / (1024 * 1024)} MB)");

                                try
                                {
                                    using var initImgFile = new UniqueRef<IFile>();
                                    innerFs.OpenFile(ref initImgFile.Ref, initImgEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                                    var initImgPfs = new PartitionFileSystem();
                                    initImgPfs.Initialize(initImgFile.Get.AsStorage()).ThrowIfFailure();

                                    var fwNcas = initImgPfs.EnumerateEntries("/", "*.nca").ToList();
                                    Logger.Info?.Print(LogClass.ServiceFs,
                                        $"Init image contains {fwNcas.Count} NCA entries");

                                    if (fwNcas.Count > 10)
                                    {
                                        Logger.Info?.Print(LogClass.ServiceFs,
                                            $"Installing {fwNcas.Count} firmware NCAs from {initImgEntry.Name}...");
                                        InstallFromPartition(initImgPfs, temporaryDirectory);
                                        return;
                                    }
                                }
                                catch (Exception initEx)
                                {
                                    Logger.Warning?.Print(LogClass.ServiceFs,
                                        $"Failed to parse initimg as PFS0: {initEx.Message}");
                                }
                            }

                            var innerNcas = innerFs.EnumerateEntries("/", "*.nca").ToList();

                            if (innerNcas.Count > 10)
                            {
                                Logger.Info?.Print(LogClass.ServiceFs,
                                    $"Found {innerNcas.Count} firmware NCAs inside {entry.Name} section {sectionType}. Installing...");
                                InstallFromPartition(innerFs, temporaryDirectory);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug?.Print(LogClass.ServiceFs,
                                $"Could not open section {sectionType} of {entry.Name}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning?.Print(LogClass.ServiceFs,
                        $"Failed to parse NCA {entry.Name}: {ex.Message}");
                }
            }

            // Fallback — just install the NCAs directly from the outer NSP
            Logger.Warning?.Print(LogClass.ServiceFs,
                "No nested firmware content found. Installing outer NCAs directly.");
            InstallFromPartition(nspFs, temporaryDirectory);
        }


        private static void InstallFromZip(ZipArchive archive, string temporaryDirectory)
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".nca") || entry.FullName.EndsWith(".nca/00"))
                {
                    // Clean up the name and get the NcaId

                    string[] pathComponents = entry.FullName.Replace(".cnmt", "").Split('/');

                    string ncaId = pathComponents[^1];

                    // If this is a fragmented nca, we need to get the previous element.GetZip
                    if (ncaId.Equals("00"))
                    {
                        ncaId = pathComponents[^2];
                    }

                    if (ncaId.Contains(".nca"))
                    {
                        string newPath = Path.Combine(temporaryDirectory, ncaId);

                        Directory.CreateDirectory(newPath);

                        entry.ExtractToFile(Path.Combine(newPath, "00"));
                    }
                }
            }
        }

        public static void SaveNca(Nca nca, string ncaId, string temporaryDirectory)
        {
            string newPath = Path.Combine(temporaryDirectory, ncaId + ".nca");

            Directory.CreateDirectory(newPath);

            using FileStream file = File.Create(Path.Combine(newPath, "00"));
            nca.BaseStorage.AsStream().CopyTo(file);
        }

        private static IFile OpenPossibleFragmentedFile(IFileSystem filesystem, string path, OpenMode mode)
        {
            using var file = new UniqueRef<IFile>();

            if (filesystem.FileExists($"{path}/00"))
            {
                filesystem.OpenFile(ref file.Ref, $"{path}/00".ToU8Span(), mode).ThrowIfFailure();
            }
            else
            {
                filesystem.OpenFile(ref file.Ref, path.ToU8Span(), mode).ThrowIfFailure();
            }

            return file.Release();
        }

        private static Stream GetZipStream(ZipArchiveEntry entry)
        {
            MemoryStream dest = MemoryStreamManager.Shared.GetStream();

            using Stream src = entry.Open();
            src.CopyTo(dest);

            return dest;
        }

        public SystemVersion VerifyFirmwarePackage(string firmwarePackage)
        {
            _virtualFileSystem.ReloadKeySet();

            // LibHac.NcaHeader's DecryptHeader doesn't check if HeaderKey is empty and throws InvalidDataException instead
            // So, we check it early for a better user experience.
            if (_virtualFileSystem.KeySet.HeaderKey.IsZeros())
            {
                throw new MissingKeyException("HeaderKey is empty. Cannot decrypt NCA headers.");
            }

            Dictionary<ulong, List<(NcaContentType type, string path)>> updateNcas = new();

            if (Directory.Exists(firmwarePackage))
            {
                return VerifyAndGetVersionDirectory(firmwarePackage);
            }

            if (!File.Exists(firmwarePackage))
            {
                throw new FileNotFoundException("Firmware file does not exist.");
            }

            FileInfo info = new(firmwarePackage);

            using FileStream file = File.OpenRead(firmwarePackage);

            switch (info.Extension)
            {
                case ".zip":
                    using (ZipArchive archive = ZipFile.OpenRead(firmwarePackage))
                    {
                        return VerifyAndGetVersionZip(archive);
                    }
                case ".xci":
                    Xci xci = new(_virtualFileSystem.KeySet, file.AsStorage());

                    if (xci.HasPartition(XciPartitionType.Update))
                    {
                        XciPartition partition = xci.OpenPartition(XciPartitionType.Update);

                        return VerifyAndGetVersion(partition);
                    }
                    else
                    {
                        throw new InvalidFirmwarePackageException("Update not found in xci file.");
                    }
                case ".nsp":
                    var nspPfs = new PartitionFileSystem();
                    nspPfs.Initialize(file.AsStorage()).ThrowIfFailure();
                    _virtualFileSystem.ImportTickets(nspPfs);
                    return VerifyAndGetVersionNsp(nspPfs);
                default:
                    break;
            }

            SystemVersion VerifyAndGetVersionDirectory(string firmwareDirectory)
            {
                return VerifyAndGetVersion(new LocalFileSystem(firmwareDirectory));
            }

            SystemVersion VerifyAndGetVersionNsp(PartitionFileSystem nspFs)
            {
                var ncaEntries = nspFs.EnumerateEntries("/", "*.nca").ToList();

                if (ncaEntries.Count > 10)
                {
                    return VerifyAndGetVersion(nspFs);
                }

                // Probe inner NCA data sections for nested firmware
                foreach (var entry in ncaEntries)
                {
                    IStorage ncaStorage = OpenPossibleFragmentedFile(nspFs, entry.FullPath, OpenMode.Read).AsStorage();

                    try
                    {
                        Nca nca = new(_virtualFileSystem.KeySet, ncaStorage);

                        NcaSectionType[] sectionTypes = { NcaSectionType.Data, NcaSectionType.Code };

                        foreach (var sectionType in sectionTypes)
                        {
                            try
                            {
                                IFileSystem innerFs = nca.OpenFileSystem(sectionType, IntegrityCheckLevel.None);

                                // Check for .initimg (dev firmware init image containing NCAs as PFS0)
                                var initImgEntries = innerFs.EnumerateEntries("/", "*.initimg").ToList();
                                foreach (var initImgEntry in initImgEntries)
                                {
                                    try
                                    {
                                        using var initImgFile = new UniqueRef<IFile>();
                                        innerFs.OpenFile(ref initImgFile.Ref, initImgEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                                        var initImgPfs = new PartitionFileSystem();
                                        initImgPfs.Initialize(initImgFile.Get.AsStorage()).ThrowIfFailure();

                                        var fwNcas = initImgPfs.EnumerateEntries("/", "*.nca").ToList();
                                        if (fwNcas.Count > 10)
                                        {
                                            return VerifyAndGetVersion(initImgPfs);
                                        }
                                    }
                                    catch { /* initimg parse failed */ }
                                }

                                var innerNcas = innerFs.EnumerateEntries("/", "*.nca").ToList();

                                if (innerNcas.Count > 10)
                                {
                                    return VerifyAndGetVersion(innerFs);
                                }
                            }
                            catch { /* section not available */ }
                        }
                    }
                    catch { /* NCA parse failed */ }
                }

                // Fallback
                return VerifyAndGetVersion(nspFs);
            }


            SystemVersion VerifyAndGetVersionZip(ZipArchive archive)
            {
                SystemVersion systemVersion = null;

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".nca") || entry.FullName.EndsWith(".nca/00"))
                    {
                        using Stream ncaStream = GetZipStream(entry);
                        IStorage storage = ncaStream.AsStorage();

                        Nca nca = new(_virtualFileSystem.KeySet, storage);

                        if (updateNcas.TryGetValue(nca.Header.TitleId, out var updateNcasItem))
                        {
                            updateNcasItem.Add((nca.Header.ContentType, entry.FullName));
                        }
                        else
                        {
                            updateNcas.Add(nca.Header.TitleId, new List<(NcaContentType, string)>());
                            updateNcas[nca.Header.TitleId].Add((nca.Header.ContentType, entry.FullName));
                        }
                    }
                }

                if (updateNcas.TryGetValue(SystemUpdateTitleId, out var ncaEntry))
                {
                    string metaPath = ncaEntry.Find(x => x.type == NcaContentType.Meta).path;

                    CnmtContentMetaEntry[] metaEntries = null;

                    var fileEntry = archive.GetEntry(metaPath);

                    using (Stream ncaStream = GetZipStream(fileEntry))
                    {
                        Nca metaNca = new(_virtualFileSystem.KeySet, ncaStream.AsStorage());

                        IFileSystem fs = metaNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                        string cnmtPath = fs.EnumerateEntries("/", "*.cnmt").Single().FullPath;

                        using var metaFile = new UniqueRef<IFile>();

                        if (fs.OpenFile(ref metaFile.Ref, cnmtPath.ToU8Span(), OpenMode.Read).IsSuccess())
                        {
                            var meta = new Cnmt(metaFile.Get.AsStream());

                            if (meta.Type == ContentMetaType.SystemUpdate)
                            {
                                metaEntries = meta.MetaEntries;

                                updateNcas.Remove(SystemUpdateTitleId);
                            }
                        }
                    }

                    if (metaEntries == null)
                    {
                        throw new FileNotFoundException("System update title was not found in the firmware package.");
                    }

                    if (updateNcas.TryGetValue(SystemVersionTitleId, out var updateNcasItem))
                    {
                        string versionEntry = updateNcasItem.Find(x => x.type != NcaContentType.Meta).path;

                        using Stream ncaStream = GetZipStream(archive.GetEntry(versionEntry));
                        Nca nca = new(_virtualFileSystem.KeySet, ncaStream.AsStorage());

                        var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                        using var systemVersionFile = new UniqueRef<IFile>();

                        if (romfs.OpenFile(ref systemVersionFile.Ref, "/file".ToU8Span(), OpenMode.Read).IsSuccess())
                        {
                            systemVersion = new SystemVersion(systemVersionFile.Get.AsStream());
                        }
                    }

                    foreach (CnmtContentMetaEntry metaEntry in metaEntries)
                    {
                        if (updateNcas.TryGetValue(metaEntry.TitleId, out ncaEntry))
                        {
                            metaPath = ncaEntry.Find(x => x.type == NcaContentType.Meta).path;

                            string contentPath = ncaEntry.Find(x => x.type != NcaContentType.Meta).path;

                            // Nintendo in 9.0.0, removed PPC and only kept the meta nca of it.
                            // This is a perfect valid case, so we should just ignore the missing content nca and continue.
                            if (contentPath == null)
                            {
                                updateNcas.Remove(metaEntry.TitleId);

                                continue;
                            }

                            ZipArchiveEntry metaZipEntry = archive.GetEntry(metaPath);
                            ZipArchiveEntry contentZipEntry = archive.GetEntry(contentPath);

                            using Stream metaNcaStream = GetZipStream(metaZipEntry);
                            using Stream contentNcaStream = GetZipStream(contentZipEntry);
                            Nca metaNca = new(_virtualFileSystem.KeySet, metaNcaStream.AsStorage());

                            IFileSystem fs = metaNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                            string cnmtPath = fs.EnumerateEntries("/", "*.cnmt").Single().FullPath;

                            using var metaFile = new UniqueRef<IFile>();

                            if (fs.OpenFile(ref metaFile.Ref, cnmtPath.ToU8Span(), OpenMode.Read).IsSuccess())
                            {
                                var meta = new Cnmt(metaFile.Get.AsStream());

                                IStorage contentStorage = contentNcaStream.AsStorage();
                                if (contentStorage.GetSize(out long size).IsSuccess())
                                {
                                    byte[] contentData = new byte[size];

                                    Span<byte> content = new(contentData);

                                    contentStorage.Read(0, content);

                                    Span<byte> hash = new(new byte[32]);

                                    LibHac.Crypto.Sha256.GenerateSha256Hash(content, hash);

                                    if (LibHac.Common.Utilities.ArraysEqual(hash.ToArray(), meta.ContentEntries[0].Hash))
                                    {
                                        updateNcas.Remove(metaEntry.TitleId);
                                    }
                                }
                            }
                        }
                    }

                    if (updateNcas.Count > 0)
                    {
                        StringBuilder extraNcas = new();

                        foreach (var entry in updateNcas)
                        {
                            foreach (var (type, path) in entry.Value)
                            {
                                extraNcas.AppendLine(path);
                            }
                        }

                        throw new InvalidFirmwarePackageException($"Firmware package contains unrelated archives. Please remove these paths: {Environment.NewLine}{extraNcas}");
                    }
                }
                else
                {
                    throw new FileNotFoundException("System update title was not found in the firmware package.");
                }

                return systemVersion;
            }

            SystemVersion VerifyAndGetVersion(IFileSystem filesystem)
            {
                SystemVersion systemVersion = null;

                CnmtContentMetaEntry[] metaEntries = null;

                foreach (var entry in filesystem.EnumerateEntries("/", "*.nca"))
                {
                    IStorage ncaStorage = OpenPossibleFragmentedFile(filesystem, entry.FullPath, OpenMode.Read).AsStorage();

                    Nca nca = new(_virtualFileSystem.KeySet, ncaStorage);

                    if (nca.Header.TitleId == SystemUpdateTitleId && nca.Header.ContentType == NcaContentType.Meta)
                    {
                        IFileSystem fs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                        string cnmtPath = fs.EnumerateEntries("/", "*.cnmt").Single().FullPath;

                        using var metaFile = new UniqueRef<IFile>();

                        if (fs.OpenFile(ref metaFile.Ref, cnmtPath.ToU8Span(), OpenMode.Read).IsSuccess())
                        {
                            var meta = new Cnmt(metaFile.Get.AsStream());

                            if (meta.Type == ContentMetaType.SystemUpdate)
                            {
                                metaEntries = meta.MetaEntries;
                            }
                        }

                        continue;
                    }
                    else if (nca.Header.TitleId == SystemVersionTitleId && nca.Header.ContentType == NcaContentType.Data)
                    {
                        var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                        using var systemVersionFile = new UniqueRef<IFile>();

                        if (romfs.OpenFile(ref systemVersionFile.Ref, "/file".ToU8Span(), OpenMode.Read).IsSuccess())
                        {
                            systemVersion = new SystemVersion(systemVersionFile.Get.AsStream());
                        }
                    }

                    if (updateNcas.TryGetValue(nca.Header.TitleId, out var updateNcasItem))
                    {
                        updateNcasItem.Add((nca.Header.ContentType, entry.FullPath));
                    }
                    else
                    {
                        updateNcas.Add(nca.Header.TitleId, new List<(NcaContentType, string)>());
                        updateNcas[nca.Header.TitleId].Add((nca.Header.ContentType, entry.FullPath));
                    }

                    ncaStorage.Dispose();
                }

                // Dev System updates do not have SystemUpdateTitle
                //if (metaEntries == null)
                //{
                //    throw new FileNotFoundException("System update title was not found in the firmware package.");
                //}

                foreach (var titleId in updateNcas.Keys.ToArray())
                {
                    if (updateNcas.TryGetValue(titleId, out var ncaEntry))
                    {
                        string metaNcaPath = ncaEntry.Find(x => x.type == NcaContentType.Meta).path;
                        string contentPath = ncaEntry.Find(x => x.type != NcaContentType.Meta).path;

                        // Nintendo in 9.0.0, removed PPC and only kept the meta nca of it.
                        // This is a perfect valid case, so we should just ignore the missing content nca and continue.
                        //
                        // Dev updates may have contents without meta, ignore these
                        if (metaNcaPath == null ||  contentPath == null)
                        {
                            updateNcas.Remove(titleId);

                            continue;
                        }

                        IStorage metaStorage = OpenPossibleFragmentedFile(filesystem, metaNcaPath, OpenMode.Read).AsStorage();
                        IStorage contentStorage = OpenPossibleFragmentedFile(filesystem, contentPath, OpenMode.Read).AsStorage();

                        Nca metaNca = new(_virtualFileSystem.KeySet, metaStorage);

                        IFileSystem fs = metaNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                        string cnmtPath = fs.EnumerateEntries("/", "*.cnmt").Single().FullPath;

                        using var metaFile = new UniqueRef<IFile>();

                        if (fs.OpenFile(ref metaFile.Ref, cnmtPath.ToU8Span(), OpenMode.Read).IsSuccess())
                        {
                            var meta = new Cnmt(metaFile.Get.AsStream());

                            if (contentStorage.GetSize(out long size).IsSuccess())
                            {
                                byte[] contentData = new byte[size];

                                Span<byte> content = new(contentData);

                                contentStorage.Read(0, content);

                                Span<byte> hash = new(new byte[32]);

                                LibHac.Crypto.Sha256.GenerateSha256Hash(content, hash);

                                if (LibHac.Common.Utilities.ArraysEqual(hash.ToArray(), meta.ContentEntries[0].Hash))
                                {
                                    updateNcas.Remove(titleId);
                                }
                            }
                        }
                    }
                }

                if (updateNcas.Count > 0)
                {
                    StringBuilder extraNcas = new();

                    foreach (var entry in updateNcas)
                    {
                        foreach (var (type, path) in entry.Value)
                        {
                            extraNcas.AppendLine(path);
                        }
                    }

                    throw new InvalidFirmwarePackageException($"Firmware package contains unrelated archives. Please remove these paths: {Environment.NewLine}{extraNcas}");
                }

                return systemVersion;
            }

            return null;
        }

        public SystemVersion GetCurrentFirmwareVersion()
        {
            LoadEntries();

            lock (_lock)
            {
                var locationEnties = _locationEntries[StorageId.BuiltInSystem];

                foreach (var entry in locationEnties)
                {
                    if (entry.ContentType == NcaContentType.Data)
                    {
                        var path = ResolveSystemPath(entry.ContentPath);

                        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        {
                            continue;
                        }

                        using FileStream fileStream = File.OpenRead(path);
                        Nca nca = new(_virtualFileSystem.GetKeySetForPath(path), fileStream.AsStorage());

                        if (nca.Header.TitleId == SystemVersionTitleId && nca.Header.ContentType == NcaContentType.Data)
                        {
                            var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                            using var systemVersionFile = new UniqueRef<IFile>();

                            if (romfs.OpenFile(ref systemVersionFile.Ref, "/file".ToU8Span(), OpenMode.Read).IsSuccess())
                            {
                                return new SystemVersion(systemVersionFile.Get.AsStream());
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves a switch content path to a real filesystem path, falling back
        /// to the non-dev system directory when the dev path doesn't exist.
        /// </summary>
        public string ResolveSystemPath(string switchContentPath)
        {
            string path = VirtualFileSystem.SwitchPathToSystemPath(switchContentPath);

            if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path) && !Directory.Exists(Path.GetDirectoryName(path)))
            {
                string nonDevPath = path.Replace(
                    Path.Combine("bis", "system_dev"),
                    Path.Combine("bis", "system"));
                if (File.Exists(nonDevPath))
                {
                    return nonDevPath;
                }
            }

            return path;
        }

        /// <summary>
        /// Returns the appropriate KeySet for a resolved filesystem path.
        /// Uses the prod keyset for files in the non-dev system directory.
        /// </summary>
        public KeySet GetKeySetForPath(string resolvedPath)
        {
            if (_prodKeySet != null
                && resolvedPath != null
                && resolvedPath.Contains(Path.Combine("bis", "system" + Path.DirectorySeparatorChar))
                && !resolvedPath.Contains(Path.Combine("bis", "system_dev")))
            {
                return _prodKeySet;
            }

            return _virtualFileSystem.KeySet;
        }
    }
}
