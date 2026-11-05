using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RP0
{
    /// <summary>
    /// Automatic backup saves made before important career actions.
    /// They are written next to the regular saves so that they show up in the in-game load dialog.
    /// Only a limited number of each kind is kept; the oldest ones get deleted.
    /// </summary>
    public static class BackupSaves
    {
        public enum Kind
        {
            Launch,
            Program
        }

        // Starts with "zz" so that the backups end up at the bottom of alphabetically sorted save lists.
        public const string Prefix = "zz RP-1 Backup ";

        // Windows MAX_PATH is 260 including the terminating null. Keep some margin.
        private const int MaxFullPathLength = 250;
        private const int MaxNameLength = 100;
        private const string LongestExtension = ".loadmeta";

        private static readonly Regex _nameRegex = new Regex(@"^zz RP-1 Backup \d{4}-\d{2}-\d{2} - (?<kind>Launch of|Program)", RegexOptions.Compiled);

        public static bool IsBackupSave(string saveName) => saveName != null && _nameRegex.IsMatch(saveName);

        public static void Create(Kind kind, string subject)
        {
            if (HighLogic.CurrentGame == null || !ROUtils.KSPUtils.CurrentGameIsCareer())
                return;

            int keepCount = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().BackupSavesToKeep;
            if (keepCount <= 0)
                return;

            try
            {
                string name = BuildName(kind, subject);
                if (name == null)
                {
                    RP0Debug.LogError("Not creating backup save: the save folder path is too long");
                    return;
                }

                RP0Debug.Log($"Creating backup save '{name}'");
                GamePersistence.SaveGame(name, HighLogic.SaveFolder, SaveMode.OVERWRITE);

                DeleteOldBackups(kind, keepCount);
            }
            catch (Exception ex)
            {
                RP0Debug.LogError($"Failed to create backup save: {ex}");
            }
        }

        private static string GetKindLabel(Kind kind) => kind == Kind.Launch ? "Launch of" : "Program";

        // Same form as the path KSP uses for saving. It is not normalized (contains "KSP_x64_Data/../"),
        // and Windows counts those characters against the path length limit too.
        private static string SaveDirectory => KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder;

        /// <summary>
        /// Builds the save name and shortens the subject so that neither the name
        /// nor the full path of any file KSP writes for the save gets too long.
        /// Returns null if even the name without a subject does not fit.
        /// </summary>
        private static string BuildName(Kind kind, string subject)
        {
            string date = ROUtils.DTUtils.UTToDate(Planetarium.GetUniversalTime()).ToString("yyyy-MM-dd");
            string baseName = $"{Prefix}{date} - {GetKindLabel(kind)}";

            int dirLength = SaveDirectory.Length + 1;
            int maxLength = Math.Min(MaxNameLength, MaxFullPathLength - dirLength - LongestExtension.Length);
            if (baseName.Length > maxLength)
                return null;

            subject = SanitizeName(subject ?? string.Empty);
            int maxSubjectLength = maxLength - baseName.Length - 1;
            if (subject.Length > maxSubjectLength)
                subject = subject.Substring(0, Math.Max(0, maxSubjectLength)).TrimEnd();

            return subject.Length > 0 ? $"{baseName} {subject}" : baseName;
        }

        private static string SanitizeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        private static void DeleteOldBackups(Kind kind, int keepCount)
        {
            string dir = SaveDirectory;
            string label = GetKindLabel(kind);

            var oldFiles = new DirectoryInfo(dir).GetFiles(Prefix + "*.sfs")
                .Where(f =>
                {
                    Match m = _nameRegex.Match(Path.GetFileNameWithoutExtension(f.Name));
                    return m.Success && m.Groups["kind"].Value == label;
                })
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(keepCount);

            foreach (FileInfo f in oldFiles)
            {
                try
                {
                    RP0Debug.Log($"Deleting old backup save '{f.Name}'");
                    f.Delete();
                    string metaFile = Path.ChangeExtension(f.FullName, ".loadmeta");
                    if (File.Exists(metaFile))
                        File.Delete(metaFile);
                }
                catch (Exception ex)
                {
                    RP0Debug.LogError($"Failed to delete old backup save {f.Name}: {ex.Message}");
                }
            }
        }
    }
}
