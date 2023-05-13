
// FACS01-01/Safe_Import v1.1
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json;


namespace FACSSafeImport
{
    public static class ZipFileCreator
    {
        [MenuItem("FACS Safe Import/Zip Unknown Scripts", false, 11)]
        public static void StartZip()
        {
            string selectedFolder = Application.dataPath.Replace(@"\", @"/");
            string[] CSPaths = Directory.GetFiles(selectedFolder, "*.cs", SearchOption.AllDirectories);
            string[] DLLPaths = Directory.GetFiles(selectedFolder, "*.dll", SearchOption.AllDirectories);
            if (CSPaths.Length != 0 || DLLPaths.Length != 0)
            {
                string[] ScriptPaths = CSPaths.Concat(DLLPaths).Select(p=>p.Replace(@"\", @"/")).ToArray();

                if (SafeImport.badFiles == null || SafeImport.safeFiles == null)
                {
                    SafeImport.ReloadDatabase();
                }
                List<string> unknownFiles = new List<string>();
                foreach (string file in ScriptPaths)
                {
                    string fileHash = DetectScriptChanges.SHA256CheckSum(file);
                    if (!SafeImport.badFiles.Contains(fileHash) && !SafeImport.safeFiles.Contains(fileHash))
                    {
                        unknownFiles.Add(file);
                    }
                }

                if (unknownFiles.Count>0)
                {
                    string tempFolder = Path.GetTempPath().Replace(@"\",@"/");
                    string resultsFolder = tempFolder + "FACS Safe Import Zip";
                    string workFolder = resultsFolder + "/work";
                    if (!Directory.Exists(resultsFolder)) Directory.CreateDirectory(resultsFolder);
                    if (Directory.Exists(workFolder)) Directory.Delete(workFolder, true);
                    Directory.CreateDirectory(workFolder);

                    foreach (string file in unknownFiles)
                    {
                        string endfilepath = file.Replace(selectedFolder+"/","");
                        string newfilepath = Path.Combine(workFolder, endfilepath).Replace(@"\", @"/");
                        string newfolder = Path.GetDirectoryName(newfilepath);
                        if (!Directory.Exists(newfolder)) Directory.CreateDirectory(newfolder);
                        File.Copy(file, newfilepath);
                    }

                    string zippath = Path.GetTempFileName(); File.Delete(zippath);
                    string zippath2 = resultsFolder + "/" + DateTime.Now.ToString("yyyy-MM-dd--HH-mm-ss") + ".zip";
                    CompressDirectory(workFolder+"/", zippath);
                    Directory.Delete(workFolder, true);
                    File.Move(zippath, zippath2);
                    EditorUtility.RevealInFinder(zippath2);
                }
                else
                {
                    Debug.LogWarning($"[<color=cyan>FACS Safe Import</color>] There are no unknown scripts to zip in this project.\n");
                }
            }
            else
            {
                Debug.LogWarning($"[<color=cyan>FACS Safe Import</color>] There are no scripts to zip in this project.\n");
            }
        }

        private static void CompressDirectory(string DirectoryPath, string OutputFilePath)
        {
            try
            {
                ZipFile.CreateFromDirectory(DirectoryPath, OutputFilePath, System.IO.Compression.CompressionLevel.Optimal, false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[<color=cyan>FACS Safe Import</color>] Exception while zipping unknown scripts.\n{ex.Message}");
            }
        }
    }
    public static class SafeImport
    {
        public static string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Replace("\\", "/");
        public static string MainFolder = LocalAppData + "/FACS Utils/Safe Import";
        public static string SafeImport_Safe = MainFolder + "/Safe Files";
        public static string SafeImport_Bad = MainFolder + "/Unsafe Files";

        private static int ticks = 0;
        public static string[] safeFiles;
        public static string[] badFiles;

        public static void OnUpdate() // workaround for bug in EditorApplication.LockReloadAssemblies. Updates assets edited outside Unity
        {
            if (ticks < 500)
            {
                ticks++;
            }
            else
            {
                ticks = 0;
                AssetDatabase.Refresh();
            }
        }

        public static void DownloadOnlineSourcesOnStartup()
        {
            if (!Directory.Exists(SafeImport.MainFolder))
            {
                Directory.CreateDirectory(SafeImport.MainFolder);
            }
            if (!File.Exists(OnlineSources.Sources_file))
            {
                using (StreamWriter sw = File.CreateText(OnlineSources.Sources_file))
                {
                    sw.WriteLine("FACS01-01/Safe_Import");
                }
            }
            List<string> sources_list = new List<string>();
            var lines = File.ReadLines(OnlineSources.Sources_file);
            foreach (var line in lines)
            {
                sources_list.Add(line);
            }
            if (sources_list.Count > 0)
            {
                OnlineSources.GetGitHubContent(sources_list);
                EditorUtility.ClearProgressBar();
            }
        }

        [InitializeOnLoadMethod]
        public static void ApplySafeModeOnStartup()
        {
            if (PlayerPrefs.GetInt("FACSSafeImport_SafeMode", 0) == 1)
            {
                EditorApplication.LockReloadAssemblies();
                EditorApplication.update += OnUpdate;
                if (SessionState.GetBool("FACSSafeImport_OnFirstLaunch", true))
                {
                    DownloadOnlineSourcesOnStartup();
                    ReloadDatabase();
                    Debug.Log($"[<color=cyan>FACS Safe Import</color>] Safe Mode Started.\n");
                }
            }
            else if (SessionState.GetBool("FACSSafeImport_OnFirstLaunch", true))
            {
                DownloadOnlineSourcesOnStartup();
                Debug.LogWarning($"[<color=cyan>FACS Safe Import</color>] Safe Mode hasn't been started.\n");
            }
            SessionState.SetBool("FACSSafeImport_OnFirstLaunch", false);
        }

        [MenuItem("FACS Safe Import/Start Safe Mode", true, 6)]
        public static bool CanStartSafeMode()
        {
            if (PlayerPrefs.GetInt("FACSSafeImport_SafeMode", 0) == 0)
                return true;
            return false;
        }

        [MenuItem("FACS Safe Import/Start Safe Mode", false, 6)]
        public static void StartSafeMode()
        {
            PlayerPrefs.SetInt("FACSSafeImport_SafeMode", 1);
            EditorApplication.LockReloadAssemblies();
            EditorApplication.update += OnUpdate;
            Debug.Log($"[<color=cyan>FACS Safe Import</color>] Safe Mode Started.\n");
            ReloadDatabase();
        }

        [MenuItem("FACS Safe Import/Exit Safe Mode", true, 7)]
        public static bool CanExitSafeMode()
        {
            if (PlayerPrefs.GetInt("FACSSafeImport_SafeMode", 0) == 1)
                return true;
            return false;
        }

        [MenuItem("FACS Safe Import/Exit Safe Mode", false, 7)]
        public static void ExitSafeMode()
        {
            PlayerPrefs.SetInt("FACSSafeImport_SafeMode", 0);
            EditorApplication.UnlockReloadAssemblies();
            EditorApplication.update -= OnUpdate;
            Debug.LogWarning($"[<color=cyan>FACS Safe Import</color>] Safe Mode Disabled.\n");
        }

        [MenuItem("FACS Safe Import/Force Reload Scripts", false, 8)]
        public static void ReloadScripts()
        {
            if (!EditorUtility.DisplayDialog($"FACS Safe Import - Force Reload Scripts", $"You are about to allow all scripts currently in your project, safe and unsafe, to compile and run.\nDo you want to proceed?", "Yes", "No"))
            {
                return;
            }
            if (PlayerPrefs.GetInt("FACSSafeImport_SafeMode", 0) == 0)
            {
                CompilationPipeline.RequestScriptCompilation();
            }
            else
            {
                EditorApplication.UnlockReloadAssemblies();
                CompilationPipeline.RequestScriptCompilation();
                AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            }
            Debug.Log($"[<color=cyan>FACS Safe Import</color>] Reloading scripts...\n");
        }

        public static void OnAfterAssemblyReload()
        {
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.LockReloadAssemblies();
            AssetDatabase.Refresh();
        }

        [MenuItem("FACS Safe Import/Database/Reload Local Database", false, 2)]
        public static void ReloadDatabase()
        {
            if (!Directory.Exists(SafeImport_Safe))
                Directory.CreateDirectory(SafeImport_Safe);

            if (!Directory.Exists(SafeImport_Bad))
                Directory.CreateDirectory(SafeImport_Bad);

            string[] SafePaths = Directory.GetFiles(SafeImport_Safe, "*.txt", SearchOption.AllDirectories);
            string[] UnsafePaths = Directory.GetFiles(SafeImport_Bad, "*.txt", SearchOption.AllDirectories);

            List<string> safepathslist = new List<string>();
            foreach (string f in SafePaths)
            {
                string file = f.Replace("\\", "/");
                var lines = File.ReadLines(file);
                foreach (var line in lines)
                {
                    if (line.StartsWith("#") || String.IsNullOrWhiteSpace(line)) continue;
                    string line2 = line.Trim();
                    if (!safepathslist.Contains(line2)) safepathslist.Add(line2);
                }
            }
            safeFiles = safepathslist.ToArray();

            List<string> unsafepathslist = new List<string>();
            foreach (string f in UnsafePaths)
            {
                string file = f.Replace("\\", "/");
                var lines = File.ReadLines(file);
                foreach (var line in lines)
                {
                    if (line.StartsWith("#") || String.IsNullOrWhiteSpace(line)) continue;
                    string line2 = line.Trim();
                    if (!unsafepathslist.Contains(line2)) unsafepathslist.Add(line2);
                }
            }
            badFiles = unsafepathslist.ToArray();

            Debug.Log($"[<color=cyan>FACS Safe Import</color>] Database loaded with {safeFiles.Length} safe hashes and {badFiles.Length} unsafe hashes.\n");
        }

        [MenuItem("FACS Safe Import/Database/Open Database Folder", false, 3)]
        public static void OpenDatabaseFolder()
        {
            if (!Directory.Exists(SafeImport_Safe))
                Directory.CreateDirectory(SafeImport_Safe);

            if (!Directory.Exists(SafeImport_Bad))
                Directory.CreateDirectory(SafeImport_Bad);

            EditorUtility.RevealInFinder(SafeImport_Safe);
        }
    }
    public class MyWebClient : WebClient
    {
        public MyWebClient() : base()
        {
            this.Headers.Add("User-Agent: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)");
            this.Headers.Add("Cache-Control", "no-cache");
            this.Headers.Add("Cache-Control", "no-store");
            this.Headers.Add("Pragma", "no-cache");
            this.Headers.Add("Expires", "-1");
        }
    }
    public class OnlineSources : EditorWindow
    {
        private static FACSGUIStyles FacsGUIStyles;
        public static string Sources_file = SafeImport.MainFolder + "/Online Sources.txt";
        public static List<string> Sources = new List<string>();
        private static int Sources_Count = 0;
        private static string newsource;

        [MenuItem("FACS Safe Import/Database/Manage Online Sources", false, 1)]
        public static void ShowWindow()
        {
            LoadSources();
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }
            FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;
            FacsGUIStyles.button.alignment = TextAnchor.MiddleCenter;
            if (newsource == null) newsource = "{user}/{repo}";
            GetWindow(typeof(OnlineSources), false, "Safe Import Sources", true);
        }

        public static int IsValidGitHubSource(string gitname)
        {
            string url = "https://api.github.com/repos/" + gitname + "/contents";
            string json;
            try
            {
                using (MyWebClient wc = new MyWebClient())
                {
                    json = wc.DownloadString(new Uri(url));
                }
            }
            catch (WebException e)
            {
                Debug.LogError($"An error occurred while fetching Github page {gitname}. Internet down? Webpage down?\n" + e.Message);
                return 0;
            }
            catch (NotSupportedException e)
            {
                Debug.LogError($"A Not Supported Exception occurred while fetching Github page {gitname}.\n" + e.Message);
                return 0;
            }
            GitHub_content[] contents = JsonConvert.DeserializeObject<GitHub_content[]>(json);
            int output = 1;
            foreach (var cont in contents)
            {
                if (cont.type == "dir")
                {
                    if (cont.name == "Safe Files")
                    {
                        output++;
                    }
                    else if (cont.name == "Unsafe Files")
                    {
                        output++;
                    }
                }
            }
            return output;
        }

        public static void GetGitHubContent(List<string> onlinesources)
        {
            int sourcescount = onlinesources.Count;
            //gitname,safe(cont.name,cont.download_url),unsafe(cont.name,cont.download_url)
            List<(string, List<(string, string)>, List<(string, string)>)> todownload = new List<(string, List<(string, string)>, List<(string, string)>)>();
            int downloadCount = 0;
            for (int i = 0; i < sourcescount; i++)
            {
                var isValidGitHubSource = IsValidGitHubSource(onlinesources[i]);
                if (isValidGitHubSource == 3)
                {
                    string gitname = onlinesources[i]; string json; GitHub_content[] contents;
                    List<(string, string)> safes = new List<(string, string)>();
                    List<(string, string)> unsafes = new List<(string, string)>();

                    string safeurl = $"https://api.github.com/repos/{gitname}/contents/Safe%20Files";
                    try
                    {
                        using (MyWebClient wc = new MyWebClient())
                        {
                            json = wc.DownloadString(new Uri(safeurl));
                        }
                        contents = JsonConvert.DeserializeObject<GitHub_content[]>(json);
                        foreach (var cont in contents)
                        {
                            if (cont.type == "file" && cont.name.EndsWith(".txt"))
                            {
                                safes.Add((cont.name, cont.download_url));
                            }
                        }
                    }
                    catch (WebException e)
                    {
                        Debug.LogError($"An error occurred while fetching Github page {gitname} (Safe Files). Internet down? Webpage down?\n" + e.Message);
                    }
                    catch (NotSupportedException e)
                    {
                        Debug.LogError($"A Not Supported Exception occurred while fetching Github page {gitname} (Safe Files).\n" + e.Message);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"An Exception occurred while processing Github page {gitname} (Safe Files).\n" + e.Message);
                    }
                    
                    string unsafeurl = $"https://api.github.com/repos/{gitname}/contents/Unsafe%20Files";
                    try
                    {
                        using (MyWebClient wc = new MyWebClient())
                        {
                            json = wc.DownloadString(new Uri(unsafeurl));
                        }
                        contents = JsonConvert.DeserializeObject<GitHub_content[]>(json);
                        foreach (var cont in contents)
                        {
                            if (cont.type == "file" && cont.name.EndsWith(".txt"))
                            {
                                unsafes.Add((cont.name, cont.download_url));
                            }
                        }
                    }
                    catch (WebException e)
                    {
                        Debug.LogError($"An error occurred while fetching Github page {gitname} (Unsafe Files). Internet down? Webpage down?\n" + e.Message);
                    }
                    catch (NotSupportedException e)
                    {
                        Debug.LogError($"A Not Supported Exception occurred while fetching Github page {gitname} (Unsafe Files).\n" + e.Message);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"An Exception occurred while processing Github page {gitname} (Unsafe Files).\n" + e.Message);
                    }

                    if (safes.Count>0 || unsafes.Count>0)
                    {
                        downloadCount += (safes.Count + unsafes.Count);
                        todownload.Add((gitname, safes, unsafes));
                    }
                }
            }
            float index = 0;
            foreach (var d in todownload)
            {
                string gitname = d.Item1;
                string safefolder = SafeImport.SafeImport_Safe + "/" + gitname.Replace("/", " ");
                string unsafefolder = SafeImport.SafeImport_Bad + "/" + gitname.Replace("/", " ");
                if (Directory.Exists(safefolder)) Directory.Delete(safefolder, true);
                Directory.CreateDirectory(safefolder);
                if (Directory.Exists(unsafefolder)) Directory.Delete(unsafefolder, true);
                Directory.CreateDirectory(unsafefolder);

                foreach (var dd in d.Item2)
                {
                    EditorUtility.DisplayProgressBar("FACS Safe Import - Downloading Online Databases", $"From {gitname}, Safe Hashes, {dd.Item1}", index / downloadCount);
                    try
                    {
                        using (MyWebClient wc = new MyWebClient())
                        {
                            wc.DownloadFile(dd.Item2, safefolder + "/" + dd.Item1);
                        }
                    }
                    catch
                    {
                        Debug.LogError($"Failed to download Safe Hashes from GitHub {gitname}, {dd.Item1}\n");
                    }
                    index++;
                }
                foreach (var dd in d.Item3)
                {
                    EditorUtility.DisplayProgressBar("FACS Safe Import - Downloading Online Databases", $"From {gitname}, Unsafe Hashes, {dd.Item1}", index / downloadCount);
                    try
                    {
                        using (MyWebClient wc = new MyWebClient())
                        {
                            wc.DownloadFile(dd.Item2, unsafefolder + "/" + dd.Item1);
                        }
                    }
                    catch
                    {
                        Debug.LogError($"Failed to download Unsafe Hashes from GitHub {gitname}, {dd.Item1}\n");
                    }
                    index++;
                }
            }
        }

        public static void DownloadGitHubContent_Single(string gitname)
        {
            //safe(cont.name,cont.download_url)
            string json; GitHub_content[] contents;
            List<(string, string)> safes = new List<(string, string)>();
            List<(string, string)> unsafes = new List<(string, string)>();

            string safeurl = $"https://api.github.com/repos/{gitname}/contents/Safe%20Files";
            try
            {
                using (MyWebClient wc = new MyWebClient())
                {
                    json = wc.DownloadString(new Uri(safeurl));
                }
                contents = JsonConvert.DeserializeObject<GitHub_content[]>(json);
                foreach (var cont in contents)
                {
                    if (cont.type == "file" && cont.name.EndsWith(".txt"))
                    {
                        safes.Add((cont.name, cont.download_url));
                    }
                }
            }
            catch (WebException e)
            {
                Debug.LogError($"An error occurred while fetching Github page {gitname} (Safe Files). Internet down? Webpage down?\n" + e.Message);
            }
            catch (NotSupportedException e)
            {
                Debug.LogError($"A Not Supported Exception occurred while fetching Github page {gitname} (Safe Files).\n" + e.Message);
            }
            catch (Exception e)
            {
                Debug.LogError($"An Exception occurred while processing Github page {gitname} (Safe Files).\n" + e.Message);
            }

            string unsafeurl = $"https://api.github.com/repos/{gitname}/contents/Unsafe%20Files";
            try
            {
                using (MyWebClient wc = new MyWebClient())
                {
                    json = wc.DownloadString(new Uri(unsafeurl));
                }
                contents = JsonConvert.DeserializeObject<GitHub_content[]>(json);
                foreach (var cont in contents)
                {
                    if (cont.type == "file" && cont.name.EndsWith(".txt"))
                    {
                        unsafes.Add((cont.name, cont.download_url));
                    }
                }
            }
            catch (WebException e)
            {
                Debug.LogError($"An error occurred while fetching Github page {gitname} (Unsafe Files). Internet down? Webpage down?\n" + e.Message);
            }
            catch (NotSupportedException e)
            {
                Debug.LogError($"A Not Supported Exception occurred while fetching Github page {gitname} (Unsafe Files).\n" + e.Message);
            }
            catch (Exception e)
            {
                Debug.LogError($"An Exception occurred while processing Github page {gitname} (Unsafe Files).\n" + e.Message);
            }

            int downloadCount = safes.Count + unsafes.Count;
            string safefolder = SafeImport.SafeImport_Safe + "/" + gitname.Replace("/", " ");
            string unsafefolder = SafeImport.SafeImport_Bad + "/" + gitname.Replace("/", " ");
            if (Directory.Exists(safefolder)) Directory.Delete(safefolder, true);
            if (Directory.Exists(unsafefolder)) Directory.Delete(unsafefolder, true);
            if (!(downloadCount > 0))
            {
                Debug.LogWarning($"There were no Safe or Unsafe entries to download from the Github page {gitname}.\n");
                return;
            }

            Directory.CreateDirectory(safefolder);
            Directory.CreateDirectory(unsafefolder);
            float index = 0;
            foreach (var dd in safes)
            {
                EditorUtility.DisplayProgressBar("FACS Safe Import - Downloading Online Databases", $"From {gitname}, Safe Hashes, {dd.Item1}", index / downloadCount);
                try
                {
                    using (MyWebClient wc = new MyWebClient())
                    {
                        wc.DownloadFile(dd.Item2, safefolder + "/" + dd.Item1);
                    }
                }
                catch
                {
                    Debug.LogError($"Failed to download Safe Hashes from GitHub {gitname}, {dd.Item1}\n");
                }
                index++;
            }
            foreach (var dd in unsafes)
            {
                EditorUtility.DisplayProgressBar("FACS Safe Import - Downloading Online Databases", $"From {gitname}, Unsafe Hashes, {dd.Item1}", index / downloadCount);
                try
                {
                    using (MyWebClient wc = new MyWebClient())
                    {
                        wc.DownloadFile(dd.Item2, unsafefolder + "/" + dd.Item1);
                    }
                }
                catch
                {
                    Debug.LogError($"Failed to download Unsafe Hashes from GitHub {gitname}, {dd.Item1}\n");
                }
                index++;
            }
            EditorUtility.ClearProgressBar();
        }

        public static void LoadSources()
        {
            if (!File.Exists(Sources_file))
            {
                using (StreamWriter sw = File.CreateText(OnlineSources.Sources_file))
                {
                    sw.WriteLine("FACS01-01/Safe_Import");
                }
            }
            List<string> sources_list = new List<string>();
            var lines = File.ReadLines(Sources_file);
            foreach (var line in lines)
            {
                sources_list.Add(line);
            }
            if (sources_list.Count > 0)
            {
                Sources = sources_list; Sources_Count = Sources.Count;
            }
            else { Sources = null; Sources_Count = 0; }
        }

        public void OnGUI()
        {
            if (FacsGUIStyles == null) { this.Close(); return; }

            EditorGUILayout.LabelField($"Your Online Database Sources\nfor FACS Safe Import", FacsGUIStyles.helpbox);

            if (Sources_Count > 0)
            {
                for (int i = 0; i < Sources_Count; i++)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button($"<b> X</b>", FacsGUIStyles.button, GUILayout.Height(25), GUILayout.Width(25)))
                    {
                        string toremove = Sources[i];
                        Sources.RemoveAt(i); Sources_Count--; i--;

                        var tempFile = Path.GetTempFileName();
                        var linesToKeep = File.ReadLines(Sources_file).Where(l => l != toremove);
                        File.WriteAllLines(tempFile, linesToKeep);
                        File.Delete(Sources_file);
                        File.Move(tempFile, Sources_file);
                        string safefolder = SafeImport.SafeImport_Safe+"/"+ toremove.Replace("/"," ");
                        string unsafefolder = SafeImport.SafeImport_Bad + "/" + toremove.Replace("/", " ");
                        if (Directory.Exists(safefolder)) Directory.Delete(safefolder, true);
                        if (Directory.Exists(unsafefolder)) Directory.Delete(unsafefolder, true);
                        SafeImport.ReloadDatabase();
                        continue;
                    }
                    if (GUILayout.Button(Sources[i], FacsGUIStyles.button, GUILayout.Height(25)))
                    {
                        Application.OpenURL($"https://github.com/{Sources[i]}");
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Add New Source: ", GUILayout.Width(105), GUILayout.Height(19));
            newsource = EditorGUILayout.TextField(newsource, GUILayout.Height(19));
            if (GUILayout.Button($"<b>✔</b>", FacsGUIStyles.button, GUILayout.Height(20), GUILayout.Width(19)))
            {
                if (Sources == null) { Sources = new List<string>(); Sources_Count = 0; }
                newsource = newsource.Trim();
                if (!String.IsNullOrWhiteSpace(newsource) && !Sources.Contains(newsource))
                {
                    int isValidGitHubSource = IsValidGitHubSource(newsource);
                    if (isValidGitHubSource == 3)
                    {
                        Sources.Add(newsource); Sources_Count++;
                        using (StreamWriter sw = File.AppendText(Sources_file))
                        {
                            sw.WriteLine(newsource);
                        }
                        Debug.Log($"[<color=cyan>FACS Safe Import</color>] The database source \"{newsource}\" was added!\n");
                        DownloadGitHubContent_Single(newsource);
                        SafeImport.ReloadDatabase();
                    }
                    else if (isValidGitHubSource > 0)
                    {
                        Debug.LogWarning($"[<color=cyan>FACS Safe Import</color>] The database source \"{newsource}\" isn't valid.\n");
                    }
                    else
                    {
                        Debug.LogWarning($"[<color=cyan>FACS Safe Import</color>] Couldn't connect to database source \"{newsource}\".\n");
                    }
                }
            }
            GUILayout.EndHorizontal();
            if (Sources_Count > 0)
            {
                if (GUILayout.Button($"Download all Databases", FacsGUIStyles.button, GUILayout.Height(30)))
                {
                    GetGitHubContent(Sources);
                    EditorUtility.ClearProgressBar();
                    Debug.Log($"[<color=cyan>FACS Safe Import</color>] Finished downloading online databases!\n");
                    SafeImport.ReloadDatabase();
                }
            }
        }

        public void OnDestroy()
        {
            Sources = null;
            Sources_Count = 0;
            newsource = null;
        }
    }

    public class NewDatabaseEntry : EditorWindow
    {
        private static FACSGUIStyles FacsGUIStyles;
        private static bool safeEntry = true;
        private static string newFileName;
        private static string newFileHeader;
        private static string selectedFolder;
        private static string[] selectedFiles;
        private static bool[] selectedFilesB;
        private static Vector2 scroll;

        [MenuItem("FACS Safe Import/Database/Add Safe Entry", false, 4)]
        public static void ShowWindow_Allowed()
        {
            OnDestroy();
            var w = GetWindow(typeof(NewDatabaseEntry), false, "SafeImport - Add Safe", true);
            w.titleContent = new GUIContent("SafeImport - Add Safe");
        }

        [MenuItem("FACS Safe Import/Database/Add Unsafe Entry", false, 5)]
        public static void ShowWindow_NotAllowed()
        {
            OnDestroy();
            safeEntry = false;
            var w = GetWindow(typeof(NewDatabaseEntry), false, "SafeImport - Add Unsafe", true);
            w.titleContent = new GUIContent("SafeImport - Add Unsafe");
        }

        public void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new FACSGUIStyles(); }
            FacsGUIStyles.helpbox.alignment = TextAnchor.MiddleCenter;

            EditorGUILayout.LabelField($"You are about to create a new {(safeEntry ? "" : "Not ")}Safe entry for your personal database.", FacsGUIStyles.helpbox);
            newFileName = EditorGUILayout.TextField("File name: ", newFileName);
            newFileHeader = EditorGUILayout.TextField("Header: ", newFileHeader);

            if (GUILayout.Button("Select Folder", FacsGUIStyles.button))
            {
                selectedFolder = EditorUtility.OpenFolderPanel("Select Folder to Scan", Application.dataPath, "");
                if (!String.IsNullOrEmpty(selectedFolder))
                {
                    selectedFolder = selectedFolder.Replace(@"/", @"\");
                    string[] CSPaths = Directory.GetFiles(selectedFolder, "*.cs", SearchOption.AllDirectories);
                    string[] DLLPaths = Directory.GetFiles(selectedFolder, "*.dll", SearchOption.AllDirectories);
                    if (CSPaths.Length == 0 && DLLPaths.Length == 0)
                    {
                        selectedFiles = null; selectedFilesB = null;
                    }
                    else
                    {
                        selectedFiles = CSPaths.Concat(DLLPaths).ToArray();
                        selectedFilesB = new bool[selectedFiles.Length];
                    }
                }
                else
                {
                    selectedFiles = null; selectedFilesB = null;
                }
            }
            if (selectedFilesB != null)
            {
                bool enable = false;
                EditorGUILayout.LabelField($"<color=green><b>Scripts found in folder</b></color>:", FacsGUIStyles.helpbox);

                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
                for (int i = 0; i < selectedFilesB.Length; i++)
                {
                    selectedFilesB[i] = GUILayout.Toggle(selectedFilesB[i], selectedFiles[i]);
                    if (!enable && selectedFilesB[i]) { enable = true; }
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All", FacsGUIStyles.button, GUILayout.Height(20))) { SelectAll(true); }
                if (GUILayout.Button("Deselect All", FacsGUIStyles.button, GUILayout.Height(20))) { SelectAll(false); }
                EditorGUILayout.EndHorizontal();

                if (enable && !String.IsNullOrWhiteSpace(newFileName) && !String.IsNullOrWhiteSpace(newFileHeader) && (newFileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0))
                {
                    if (GUILayout.Button("Generate!", FacsGUIStyles.button, GUILayout.Height(40)))
                    {
                        CreateNewEntry();
                        selectedFiles = null; selectedFilesB = null;
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void SelectAll(bool yesno)
        {
            if (selectedFilesB == null) return;
            for (int i = 0; i < selectedFilesB.Length; i++)
            {
                selectedFilesB[i] = yesno;
            }
        }

        private void WriteToFile(string filepath)
        {
            using (StreamWriter sw = File.AppendText(filepath))
            {
                sw.WriteLine("##" + newFileHeader);
                for (int i = 0; i < selectedFilesB.Length; i++)
                {
                    if (selectedFilesB[i])
                    {
                        string f = selectedFiles[i];
                        f = f.Replace(@"\", @"/");
                        string fn;
                        if (f.StartsWith(Application.dataPath))
                        {
                            fn = f.Substring(Application.dataPath.Length - 6);
                        }
                        else
                        {
                            fn = f.Substring(selectedFolder.Length + 1);
                        }
                        if (!File.Exists(f))
                        {
                            Debug.LogWarning($"[<color=cyan>FACS Safe Import</color>] The file {fn} doesn't exist and couldn't be added to the database.\n");
                            continue;
                        }
                        sw.WriteLine("#" + fn);
                        sw.WriteLine(DetectScriptChanges.SHA256CheckSum(f));
                    }
                }
            }
        }

        private void CreateNewEntry()
        {
            string FileName = newFileName + ".txt";
            string onPath;
            if (safeEntry) onPath = SafeImport.SafeImport_Safe;
            else onPath = SafeImport.SafeImport_Bad;
            if (!Directory.Exists(onPath)) Directory.CreateDirectory(onPath);

            var samefile = Directory.GetFiles(onPath, "*.txt", SearchOption.TopDirectoryOnly).Where(o => o.EndsWith(FileName, StringComparison.OrdinalIgnoreCase));
            if (samefile.Any())
            {
                if (EditorUtility.DisplayDialog($"FACS Safe Import - Adding {(safeEntry ? "Safe" : "Unsafe")} Entry - File already exists", $"A file with name \"{FileName}\" already exist in your database.\nDo you want to append the new entries to it?", "Yes", "No"))
                {
                    FileName = samefile.First();
                    WriteToFile(FileName);
                    Debug.Log($"[<color=cyan>FACS Safe Import</color>] Finished appending new entries to the database!\n");
                    SafeImport.ReloadDatabase();
                    return;
                }
                Debug.LogWarning($"[<color=cyan>FACS Safe Import</color>] Adding a new entry to the database was cancelled.\n");
                return;
            }
            WriteToFile(onPath + "/" + FileName);
            Debug.Log($"[<color=cyan>FACS Safe Import</color>] Finished adding a new entry file to the database!\n");
            SafeImport.ReloadDatabase();
        }

        public static void OnDestroy()
        {
            newFileName = newFileHeader = selectedFolder = "";
            selectedFiles = null;
            selectedFilesB = null;
            scroll = default;
            safeEntry = true;
        }
    }

    public class DetectScriptChanges : AssetPostprocessor
    {
        public static string SHA256CheckSum(string filePath)
        {
            using (SHA256 SHA256 = SHA256.Create())
            {
                using (FileStream fileStream = File.OpenRead(filePath))
                    return BitConverter.ToString(SHA256.ComputeHash(fileStream)).Replace("-", "").ToLowerInvariant();
            }
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (PlayerPrefs.GetInt("FACSSafeImport_SafeMode", 0) == 0) { return; }

            if (importedAssets.Length > 0)
            {
                List<string> importeds = new List<string>();
                foreach (string asset in importedAssets)
                {
                    if (asset.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || asset.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        importeds.Add(asset);
                    }
                }

                if (importeds.Count > 0)
                {
                    Debug.LogWarning($"[<color=cyan>FACS Safe Import</color>] Some scripts ({importeds.Count}) were added/modified!\n");

                    CheckSafeUnsafeFiles(importeds);
                }
            }

            if (deletedAssets.Length > 0)
            {
                List<string> deleteds = new List<string>();
                foreach (string asset in deletedAssets)
                {
                    if (asset.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || asset.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        deleteds.Add(asset);
                    }
                }
                if (deleteds.Count > 0)
                {
                    deleteds.Sort();
                    string output = String.Join("\n", deleteds);
                    Debug.LogWarning($"[<color=cyan>FACS Safe Import</color>] Some scripts ({deleteds.Count}) were deleted:\n" + output + "\n");
                }
            }
        }

        [MenuItem("FACS Safe Import/Scan Current Scripts", false, 9)]
        public static void CheckSafeUnsafeFilesManual()
        {
            string dataPath = Application.dataPath;
            string dataPath_Assets = dataPath.Substring(0, dataPath.LastIndexOf('/') + 1);
            string[] CSPaths = Directory.GetFiles(dataPath, "*.cs", SearchOption.AllDirectories);
            string[] DLLPaths = Directory.GetFiles(dataPath, "*.dll", SearchOption.AllDirectories);

            List<string> files = new List<string>();

            foreach (string file in CSPaths)
            {
                string f = file.Replace(@"\", @"/").Replace(dataPath_Assets, "");
                files.Add(f);
            }
            foreach (string file in DLLPaths)
            {
                string f = file.Replace(@"\", @"/").Replace(dataPath_Assets, "");
                files.Add(f);
            }
            if (files.Any())
            {
                if (SafeImport.badFiles == null || SafeImport.safeFiles == null)
                {
                    SafeImport.ReloadDatabase();
                }
                CheckSafeUnsafeFiles(files);
            }
            else
            {
                Debug.Log($"[<color=cyan>FACS Safe Import</color>] There are no scripts in this project to scan.\n");
            }
        }

        public static void CheckSafeUnsafeFiles(List<string> importeds)
        {
            importeds.Sort();
            List<(string, string)> safeFiles = new List<(string, string)>();
            List<(string, string)> badFilesShouldDelete = new List<(string, string)>();
            List<(string, string)> unknownFiles = new List<(string, string)>();

            foreach (string file in importeds)
            {
                string fileHash = SHA256CheckSum(file);
                if (SafeImport.badFiles.Contains(fileHash))
                {
                    badFilesShouldDelete.Add((file, fileHash));
                }
                else if (SafeImport.safeFiles.Contains(fileHash))
                {
                    safeFiles.Add((file, fileHash));
                }
                else unknownFiles.Add((file, fileHash));
            }

            if (safeFiles.Count > 0)
            {
                string output = "";
                foreach (var f in safeFiles)
                {
                    output += f.Item1 + " | Hash: " + f.Item2 + "\n";
                }
                Debug.Log($"[<color=cyan>FACS Safe Import</color>] Safe scripts ({safeFiles.Count}):\n" + output);
            }

            if (unknownFiles.Count > 0)
            {
                string output = "";
                foreach (var f in unknownFiles)
                {
                    output += f.Item1 + " | Hash: " + f.Item2 + "\n";
                }
                Debug.LogWarning($"[<color=cyan>FACS Safe Import</color>] Unknown scripts ({unknownFiles.Count}):\n" + output);
            }

            if (badFilesShouldDelete.Count > 0)
            {
                string output = "";
                foreach (var f in badFilesShouldDelete)
                {
                    output += f.Item1 + " | Hash: " + f.Item2 + "\n";
                    File.Delete(f.Item1);
                    if (File.Exists(f.Item1 + ".meta"))
                    {
                        File.Delete(f.Item1 + ".meta");
                    }
                }
                Debug.LogError($"[<color=cyan>FACS Safe Import</color>] Unsafe scripts ({badFilesShouldDelete.Count}). They will be deleted:\n" + output);
            }
        }
    }
    public class FACSGUIStyles
    {
        public GUIStyle helpbox;
        public GUIStyle dropdownbutton;
        public GUIStyle button;
        public GUIStyle helpboxSmall;
        public GUIStyle buttonSmall;
        
        public FACSGUIStyles()
        {
            helpbox = new GUIStyle("HelpBox")
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                wordWrap = true
            };

            dropdownbutton = new GUIStyle("dropdownbutton")
            {
                richText = true,
                alignment = TextAnchor.MiddleLeft
            };

            button = new GUIStyle("button")
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };

            helpboxSmall = new GUIStyle("HelpBox")
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                wordWrap = true,
                padding = new RectOffset(4, 4, 1, 2)
            };

            buttonSmall = new GUIStyle("button")
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                padding = new RectOffset(4, 4, 1, 2)
            };
        }
    }
    public class GitHub_content
    {
        public string name { get; set; }
        public string path { get; set; }
        public string sha { get; set; }
        public long size { get; set; }
        public string url { get; set; }
        public string html_url { get; set; }
        public string git_url { get; set; }
        public string download_url { get; set; }
        public string type { get; set; }
        public GitHub_content_links _links { get; set; }

    }
    public class GitHub_content_links
    {
        public string self { get; set; }
        public string git { get; set; }
        public string html { get; set; }
    }
}
#endif

