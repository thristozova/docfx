// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Docs.Build
{
    internal class RestoreLocker
    {
        public static async Task Save(string docset, Func<Task<RestoreLock>> updateAction)
        {
            Debug.Assert(!string.IsNullOrEmpty(docset));

            var restoreLockFilePath = GetRestoreLockFilePath(docset);
            await ProcessUtility.CreateFileMutex(
                Path.GetRelativePath(AppData.RestoreLockDir, restoreLockFilePath),
                async () =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(restoreLockFilePath));

                    var restoreLock = await updateAction();
                    if (restoreLock.IsEmpty)
                    {
                        // clean up the restore lock file
                        File.Delete(restoreLockFilePath);
                    }
                    else
                    {
                        File.WriteAllText(restoreLockFilePath, JsonUtility.Serialize(restoreLock));
                    }
                });
        }

        public static async Task<RestoreLock> Load(string docset)
        {
            Debug.Assert(!string.IsNullOrEmpty(docset));

            var restoreLockFilePath = GetRestoreLockFilePath(docset);
            var restore = new RestoreLock();
            await ProcessUtility.CreateFileMutex(
                Path.GetRelativePath(AppData.RestoreLockDir, restoreLockFilePath),
                () =>
                {
                    if (File.Exists(restoreLockFilePath))
                    {
                        restore = JsonUtility.Deserialize<RestoreLock>(File.ReadAllText(restoreLockFilePath)).Item2;
                    }

                    return Task.CompletedTask;
                });

            return restore;
        }

        public static async Task<List<RestoreLock>> LoadAll()
        {
            var restoreLocks = new ConcurrentBag<RestoreLock>();
            await ParallelUtility.ForEach(Directory.EnumerateFiles(AppData.RestoreLockDir, "*", SearchOption.TopDirectoryOnly), async restoreLockFilePath =>
            {
                await ProcessUtility.CreateFileMutex(
                Path.GetRelativePath(AppData.RestoreLockDir, restoreLockFilePath),
                () =>
                {
                    if (File.Exists(restoreLockFilePath))
                    {
                        restoreLocks.Add(JsonUtility.Deserialize<RestoreLock>(File.ReadAllText(restoreLockFilePath)).Item2);
                    }

                    return Task.CompletedTask;
                });
            });

            return restoreLocks.ToList();
        }

        public static string GetRestoreLockFilePath(string docset)
        {
            docset = PathUtility.NormalizeFile(Path.GetFullPath(docset));
            var docsetKey = Path.GetFileName(docset) + "-" + HashUtility.GetSha1HashString(docset);

            return Path.Combine(AppData.RestoreLockDir, $"{docsetKey}-lock.json");
        }
    }
}