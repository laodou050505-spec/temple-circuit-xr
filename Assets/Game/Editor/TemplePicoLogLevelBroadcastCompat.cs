using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace TempleCircuitEditor
{
    /// <summary>
    /// PICO 6.0.0's platform AAR subscribes every player to a system log-level-refresh
    /// broadcast. In the PICO Emulator + Unity 6000.5 IL2CPP combination that native
    /// callback can terminate the player. The game does not change SDK log levels while
    /// it is running, so this build-only compatibility shim gives that receiver a private,
    /// never-broadcast action. The official PXR loader and native libraries remain intact.
    /// </summary>
    public sealed class TemplePicoLogLevelBroadcastCompat : IPostGenerateGradleAndroidProject
    {
        const string OfficialLogLevelRefreshAction = "android.intent.action.loglevel_refresh";
        const string DisabledLogLevelRefreshAction = "com.templecircuit.xr.loglevel.off.xxxy";
        const string PxrPlatformAarName = "PxrPlatform.aar";

        public int callbackOrder => 9999;

        public void OnPostGenerateGradleAndroidProject(string gradleProjectPath)
        {
            var aarPath = Path.Combine(gradleProjectPath, "libs", PxrPlatformAarName);
            if (!File.Exists(aarPath))
                throw new BuildFailedException("PICO compatibility patch could not find " + aarPath);

            var patchedClassCount = PatchLogLevelBroadcastAction(aarPath);
            Debug.Log("PICO compatibility patch: disabled the SDK log-level refresh broadcast in "
                      + PxrPlatformAarName + " (" + patchedClassCount + " class constants)."
                      + " PXR loader and native libraries are unchanged.");
        }

        static int PatchLogLevelBroadcastAction(string aarPath)
        {
            var originalBytes = Encoding.ASCII.GetBytes(OfficialLogLevelRefreshAction);
            var replacementBytes = Encoding.ASCII.GetBytes(DisabledLogLevelRefreshAction);
            if (originalBytes.Length != replacementBytes.Length)
                throw new BuildFailedException("The PICO broadcast compatibility action must preserve the Java constant length.");

            var aarEntries = ReadArchiveEntries(aarPath);
            var classesEntryIndex = aarEntries.FindIndex(entry => entry.Name == "classes.jar");
            if (classesEntryIndex < 0)
                throw new BuildFailedException("PICO compatibility patch could not find classes.jar in " + aarPath);

            var classEntries = ReadArchiveEntries(aarEntries[classesEntryIndex].Bytes);
            var replacements = 0;
            for (var i = 0; i < classEntries.Count; i++)
            {
                if (!classEntries[i].Name.StartsWith("com/psmart/aosoperation/SysActivity", StringComparison.Ordinal))
                    continue;

                var replacedInClass = ReplaceAll(classEntries[i].Bytes, originalBytes, replacementBytes);
                replacements += replacedInClass;
            }

            if (replacements != 2)
                throw new BuildFailedException("Unexpected PICO SDK log-level broadcast layout: expected 2 action constants, found " + replacements + ".");

            aarEntries[classesEntryIndex] = new ArchiveEntryData(
                aarEntries[classesEntryIndex].Name,
                aarEntries[classesEntryIndex].LastWriteTime,
                WriteArchiveEntries(classEntries));

            var patchedAar = WriteArchiveEntries(aarEntries);
            var verifiedClasses = ReadArchiveEntries(patchedAar);
            var verifiedClassesIndex = verifiedClasses.FindIndex(entry => entry.Name == "classes.jar");
            var verifiedActionCount = CountOccurrences(verifiedClasses[verifiedClassesIndex].Bytes, originalBytes);
            if (verifiedActionCount != 0)
                throw new BuildFailedException("PICO compatibility patch verification failed: original log-level action remains in classes.jar.");

            var temporaryAarPath = aarPath + ".templecompat-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporaryAarPath, patchedAar);
                File.Copy(temporaryAarPath, aarPath, true);
            }
            finally
            {
                if (File.Exists(temporaryAarPath))
                    File.Delete(temporaryAarPath);
            }

            return replacements;
        }

        static int ReplaceAll(byte[] buffer, byte[] original, byte[] replacement)
        {
            var replacements = 0;
            for (var index = 0; index <= buffer.Length - original.Length; index++)
            {
                var match = true;
                for (var offset = 0; offset < original.Length; offset++)
                {
                    if (buffer[index + offset] != original[offset])
                    {
                        match = false;
                        break;
                    }
                }

                if (!match)
                    continue;

                Buffer.BlockCopy(replacement, 0, buffer, index, replacement.Length);
                replacements++;
                index += original.Length - 1;
            }

            return replacements;
        }

        static int CountOccurrences(byte[] buffer, byte[] value)
        {
            var count = 0;
            for (var index = 0; index <= buffer.Length - value.Length; index++)
            {
                var match = true;
                for (var offset = 0; offset < value.Length; offset++)
                {
                    if (buffer[index + offset] != value[offset])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    count++;
                    index += value.Length - 1;
                }
            }

            return count;
        }

        static List<ArchiveEntryData> ReadArchiveEntries(string archivePath)
        {
            return ReadArchiveEntries(File.ReadAllBytes(archivePath));
        }

        static List<ArchiveEntryData> ReadArchiveEntries(byte[] archiveBytes)
        {
            var entries = new List<ArchiveEntryData>();
            using (var stream = new MemoryStream(archiveBytes, false))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
            {
                foreach (var entry in archive.Entries)
                {
                    using (var entryStream = entry.Open())
                    using (var copy = new MemoryStream())
                    {
                        entryStream.CopyTo(copy);
                        entries.Add(new ArchiveEntryData(entry.FullName, entry.LastWriteTime, copy.ToArray()));
                    }
                }
            }

            return entries;
        }

        static byte[] WriteArchiveEntries(List<ArchiveEntryData> entries)
        {
            using (var stream = new MemoryStream())
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    foreach (var data in entries)
                    {
                        var entry = archive.CreateEntry(data.Name, System.IO.Compression.CompressionLevel.Optimal);
                        entry.LastWriteTime = data.LastWriteTime;
                        using (var entryStream = entry.Open())
                            entryStream.Write(data.Bytes, 0, data.Bytes.Length);
                    }
                }

                return stream.ToArray();
            }
        }

        struct ArchiveEntryData
        {
            public readonly string Name;
            public readonly DateTimeOffset LastWriteTime;
            public readonly byte[] Bytes;

            public ArchiveEntryData(string name, DateTimeOffset lastWriteTime, byte[] bytes)
            {
                Name = name;
                LastWriteTime = lastWriteTime;
                Bytes = bytes;
            }
        }
    }
}
