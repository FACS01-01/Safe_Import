using System.IO;
using UnityEditor;
using UnityEngine;


namespace FACS01_Dependencies
{
    [InitializeOnLoad]
    public class SafeImport_Dependencies
    {
        private const string lockedScript = "/FACS Safe Import/Editor/SafeImport.cs";
        private const string SysIOCompDLL = "-r:System.IO.Compression.dll";
        private const string SysIOCompFSDLL = "-r:System.IO.Compression.FileSystem.dll";

        static SafeImport_Dependencies()
        {
            string datapath = Application.dataPath;
            string cscFile = datapath + "/csc.rsp";
            if (!File.Exists(cscFile))
            {
                using (StreamWriter sw = File.CreateText(cscFile))
                {
                    sw.WriteLine(SysIOCompDLL);
                    sw.WriteLine(SysIOCompFSDLL);
                }
            }
            else
            {
                bool b1 = false; bool b2 = false;
                using (StreamReader sr = File.OpenText(cscFile))
                {
                    string s = ""; 
                    while ((s = sr.ReadLine()) != null)
                    {
                        if (s.Contains(SysIOCompDLL)) b1 = true;
                        else if (s.Contains(SysIOCompFSDLL)) b2 = true;
                    }
                }
                if (!b1) File.AppendAllText(cscFile, SysIOCompDLL + "\n");
                if (!b2) File.AppendAllText(cscFile, SysIOCompFSDLL + "\n");
            }
            UnlockScript();
        }

        static void UnlockScript()
        {
            string scriptpath = Application.dataPath + lockedScript;
            var script = File.ReadAllLines(scriptpath);
            if (script[0].Contains("/*"))
            {
                script[0] = "";
                script[script.Length-1] = "";
                File.WriteAllLines(scriptpath, script);
                AssetDatabase.ImportAsset("Assets"+lockedScript);
            }
        }
    }
}
