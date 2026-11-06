using AtlusScriptCompiler;
using AtlusScriptLibrary.Common.Libraries;
using AtlusScriptLibrary.Common.Logging;
using AtlusScriptLibrary.Common.Text.Encodings;
using AtlusScriptLibrary.FlowScriptLanguage;
using AtlusScriptLibrary.MessageScriptLanguage;
using AtlusScriptLibrary.MessageScriptLanguage.Compiler;
using Microsoft.CSharp.RuntimeBinder;
using Octokit;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;

namespace AtlusScriptHelper
{
    public class ScriptHelper
    {
        public static void InitializeScriptCompiler(string inputPath, string outputPath, AtlusEncoding encoding)
        {
            AtlusScriptCompiler.Program.ProgramOptions.IsActionAssigned = false;
            AtlusScriptCompiler.Program.ProgramOptions.InputFilePath = inputPath;
            AtlusScriptCompiler.Program.ProgramOptions.OutputFilePath = outputPath;
            AtlusScriptCompiler.Program.MessageScriptOptions.Encoding = encoding;
            AtlusScriptCompiler.Program.ProgramOptions.LogTrace = false;
            AtlusScriptCompiler.Program.MessageScriptOptions.EncodingName = encoding.EncodingName;
            if (Path.GetExtension(inputPath).ToLower() == ".bmd")
            {
                AtlusScriptCompiler.Program.ProgramOptions.InputFileFormat = InputFileFormat.MessageScriptBinary;
                AtlusScriptCompiler.Program.ProgramOptions.DoCompile = false;
                AtlusScriptCompiler.Program.ProgramOptions.DoDecompile = true;
            }
            else if (Path.GetExtension(inputPath).ToLower() == ".bf")
            {
                AtlusScriptCompiler.Program.ProgramOptions.InputFileFormat = InputFileFormat.FlowScriptBinary;
                AtlusScriptCompiler.Program.ProgramOptions.DoCompile = false;
                AtlusScriptCompiler.Program.ProgramOptions.DoDecompile = true;
            }
            else if (Path.GetExtension(inputPath).ToLower() == ".msg")
            {
                AtlusScriptCompiler.Program.ProgramOptions.InputFileFormat = InputFileFormat.MessageScriptTextSource;
                AtlusScriptCompiler.Program.ProgramOptions.DoCompile = true;
                AtlusScriptCompiler.Program.ProgramOptions.DoDecompile = false;
            }
            else if (Path.GetExtension(inputPath).ToLower() == ".flow")
            {
                AtlusScriptCompiler.Program.ProgramOptions.InputFileFormat = InputFileFormat.FlowScriptTextSource;
                AtlusScriptCompiler.Program.ProgramOptions.DoCompile = true;
                AtlusScriptCompiler.Program.ProgramOptions.DoDecompile = false;
            }
            AtlusScriptCompiler.Program.Logger = new Logger($"{nameof(AtlusScriptCompiler)}_{Path.GetFileNameWithoutExtension(outputPath)}");
            AtlusScriptCompiler.Program.Listener = new FileAndConsoleLogListener(true, LogLevel.Info);
        }

        public static void CompileMSGToBMD(string msgFile, string outPath, string encoding = "P5R_EFIGS", string library = "P5R", string outFormat = "V1BE")
        {
            AtlusEncoding atlusEncoding = new AtlusEncoding(encoding);
            InitializeScriptCompiler(msgFile, outPath, atlusEncoding);
            AtlusScriptCompiler.Program.RunCompiler(new string[] {
                msgFile, "-Compile",
                "-Library", "P5R",
                "-Encoding", atlusEncoding.EncodingName,
                "-OutFormat", "V1BE",
                "-Out", outPath });
        }

        public static void CompileFLOWToBF(string flowFile, string outPath, string encoding = "P5R_EFIGS", string library = "P5R", string outFormat = "V3BE")
        {
            AtlusEncoding atlusEncoding = new AtlusEncoding(encoding);
            InitializeScriptCompiler(flowFile, outPath, atlusEncoding);
            AtlusScriptCompiler.Program.RunCompiler(new string[] {
                flowFile, "-Compile",
                "-Library", "P5R",
                "-Encoding", atlusEncoding.EncodingName,
                "-OutFormat", "V3BE",
                "-Out", outPath, "-Hook", "-SumBits" });
        }

        public static void DecompileBFToFLOW(string bfFile, string outPath, string encoding = "P5R_EFIGS", string library = "P5R")
        {
            AtlusEncoding atlusEncoding = new AtlusEncoding(encoding);
            InitializeScriptCompiler(bfFile, outPath, atlusEncoding);
            AtlusScriptCompiler.Program.RunCompiler(new string[] {
                bfFile, "-Decompile",
                "-Library", "P5R",
                "-Encoding", atlusEncoding.EncodingName,
                "-Out", outPath, "-SumBits" });
        }

        public static void DecompileBMDToMSG(string bmdFile, string outPath, string encoding = "P5R_EFIGS", string library = "P5R")
        {
            AtlusEncoding atlusEncoding = new AtlusEncoding(encoding);
            InitializeScriptCompiler(bmdFile, outPath, atlusEncoding);
            AtlusScriptCompiler.Program.RunCompiler(new string[] {
                bmdFile, "-Decompile",
                "-Library", "P5R",
                "-Encoding", atlusEncoding.EncodingName,
                "-Out", outPath });
        }

        public static string ConcatMsgs(string msgsDir)
        {
            string txt = "";
            foreach(var file in Directory.GetFiles(msgsDir, "*.msg", SearchOption.AllDirectories))
            {
                txt += File.ReadAllText(file) + "\r\n";
            }
            return txt;
        }


        public static void RemoveMsgComments(string msgFile, string newMsgDest, string removeIfComment = "Royal")
        {
            if (!File.Exists(msgFile))
                return;

            var lines = File.ReadAllLines(msgFile);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // Remove entire line if comment doesn't match given string
                if (line.Contains($"// {removeIfComment}") || line.Contains($"//{removeIfComment}"))
                    lines[i] = line.Replace($"// {removeIfComment}", "").Replace($"//{removeIfComment}", "");
                else if (line.Contains("//"))
                    lines[i] = "";
            }

            File.WriteAllText(newMsgDest, String.Join("\n", lines), Encoding.Unicode);
        }

        public static void RemoveMsgImports(string scriptsDir)
        {
            foreach (var file in Directory.GetFiles(scriptsDir, "*.flow", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains(".msg"))
                        lines[i] = "//" + lines[i];
                }
                File.WriteAllText(file, String.Join("\n", lines), Encoding.Unicode);
            }
        }

        public static void RemoveFlowComments(string script, string removeIfComment = "Vanilla", bool completelyRemoveComments = false)
        {
            if (File.Exists(script))
            {
                string text = File.ReadAllText(script);
                
                // Comment out blocks that only pertain to specific game version (i.e. Royal, Vanilla)
                text = text.Replace($"/* {removeIfComment} Start */", $"/* {removeIfComment} Start ")
                .Replace($"/* {removeIfComment} End */", $" {removeIfComment} End */");

                // Optionally remove commented out blocks entirely from script
                if (completelyRemoveComments)
                    text = RemoveBetween(text, "/*", "*/");

                File.WriteAllText(script, text, Encoding.Unicode);
            }
        }

        public static string RemoveBetween(string sourceString, string startTag, string endTag)
        {
            Regex regex = new Regex(string.Format("{0}(.*?){1}", Regex.Escape(startTag), Regex.Escape(endTag)), RegexOptions.Singleline);
            return regex.Replace(sourceString, startTag + endTag);
        }

        public static void ReindexMsgs(string script, string msgFile, string bfPathRelativeToFlow)
        {
            // STEP 1: Compile FLOW to BF to get msg file and header
            string tempDir = Path.GetDirectoryName(script);
            string tempFlow = Path.Combine(tempDir, "temp.flow");
            string tempBf = Path.Combine(tempDir, "temp.bf");
            string tempMsg = Path.Combine(tempDir, "temp.bf.msg");
            string tempMsgHeader = tempMsg + ".h";
            
            
            if (File.Exists(tempFlow))
                File.Delete(tempFlow);
            if (File.Exists(tempBf))
                File.Delete(tempBf);
            if (File.Exists(tempMsg))
                File.Delete(tempMsg);
            if (File.Exists(tempMsgHeader))
                File.Delete(tempMsgHeader);

            string scriptTxt = File.ReadAllText(script);
            // Create temporary .FLOW with import of original .BF
            File.WriteAllText(tempFlow, $"import( \"{bfPathRelativeToFlow.Replace("\\","/")}\" );\r\n"
                + scriptTxt);

            CompileFLOWToBF( tempFlow, tempBf );

            DecompileBFToFLOW( tempBf, tempBf + ".flow");

            if (!File.Exists(tempMsgHeader))
            {
                // Stop here: the header is read below, so continuing would just crash
                // with FileNotFoundException and hide the real compile error above.
                Console.WriteLine($"Failed to reindex messages: compiler didn't produce {tempMsgHeader}. See the compiler log above.");
                return;
            }

            
            // Names of selection menus in the compiled BF, from the decompiled .msg ("[sel Name ...]").
            // Help captions are assigned to the most recent *selection*, not just the previous message:
            // a menu can have other dialogs between its captions (e.g. Title_JokerScale in Options_PlayerModel).
            var selNames = new HashSet<string>(
                File.ReadAllLines(tempMsg)
                    .Where(l => l.StartsWith("[sel "))
                    .Select(l => l.Substring(5).TrimEnd(']').Split(' ')[0]));

            // If compilation and decompilation succeeds, save indexes of GENERIC_HELP msgs and their references
            var msgNames = File.ReadAllLines(tempMsgHeader);
            List<Tuple<string, int, int>> msgIndexes = new List<Tuple<string, int, int>>(); // sel_name, help_index, overall_msg_index
            string currentSel = null;
            for (int i = 0; i < msgNames.Length; i++)
            {
                if (!msgNames[i].StartsWith("const int "))
                    continue;
                string[] parts = msgNames[i].Replace("const int ", "").Split('=');
                string name = parts[0].Trim();
                int overallIndex = int.Parse(parts[1].Trim().TrimEnd(';'));

                if (selNames.Contains(name))
                    currentSel = name;
                else if (name.StartsWith("GENERIC_HELP_") && currentSel != null)
                    msgIndexes.Add(new Tuple<string, int, int>(currentSel, msgIndexes.Count(x => x.Item1.Equals(currentSel)), overallIndex));
            }

            //foreach(var msgFile in Directory.GetFiles(msgsDir, "*.msg", SearchOption.AllDirectories))
            if (File.Exists(msgFile))
            {
                string[] msgLines = File.ReadAllLines(msgFile);
                int helpCountSinceLastSel = 0;
                int refCountSinceLastSel = 0;

                string latestSel = "";
                for (int i = 0; i < msgLines.Length; i++)
                {
                    if (msgLines[i].Contains("[sel "))
                    {
                        latestSel = msgLines[i].Replace("[sel ", "").Replace("]","");
                        helpCountSinceLastSel = 0;
                        refCountSinceLastSel = 0;
                    }

                    var selHelps = msgIndexes.Where(x => x.Item1.Equals(latestSel)).ToList();

                    // If current line contains "[ref "...
                    if (msgLines[i].Contains("[ref "))
                    {
                        // Menu has no (or too few) help captions in the BF: leave its refs alone.
                        if (refCountSinceLastSel >= selHelps.Count)
                        {
                            Console.WriteLine($"Warning: {latestSel} option {refCountSinceLastSel} has a [ref] but no matching GENERIC_HELP message; left unchanged.");
                            refCountSinceLastSel++;
                            continue;
                        }

                        // Get name of selection option
                        int refIndex = msgLines[i].IndexOf("[ref ");
                        string substring = msgLines[i].Substring(0, refIndex);

                        // Create new option string with reindexed ref block
                        // [ref RelativeID GlobalID]: GlobalID is the N in GENERIC_HELP_N (see P5R MessageScriptLibrary.json).
                        // Captions are renamed to GENERIC_HELP_<overall message index> below, so both sides agree.
                        // (Previously wrote the menu *name* as RelativeID, which the compiler rejects.)
                        var help = selHelps[refCountSinceLastSel];
                        string newString = $"{substring}[ref {help.Item2} {help.Item3}][e]";
                        
                        // Update line with new data
                        msgLines[i] = newString;

                        refCountSinceLastSel++;
                    }

                    if (msgLines[i].Contains("[dlg GENERIC_HELP_") || msgLines[i].Contains("[msg GENERIC_HELP_"))
                    {
                        if (helpCountSinceLastSel < selHelps.Count)
                            // Was the whole Tuple, giving "GENERIC_HELP_(Options_X, 0, 38)".
                            msgLines[i] = $"[dlg GENERIC_HELP_{selHelps[helpCountSinceLastSel].Item3}]";
                        else
                            Console.WriteLine($"Warning: extra GENERIC_HELP under {latestSel} not found in the BF; left unchanged.");
                        helpCountSinceLastSel++;
                    }
                }
                

                File.WriteAllText(msgFile, string.Join("\n", msgLines), Encoding.Unicode);
            }
        }

        public static FileStream WaitForFile(string fullPath,
            System.IO.FileMode mode = System.IO.FileMode.Open,
            FileAccess access = FileAccess.ReadWrite,
            FileShare share = FileShare.None)
        {
            for (int numTries = 0; numTries < 10; numTries++)
            {
                FileStream fs = null;
                try
                {
                    fs = new FileStream(fullPath, mode, access, share);
                    return fs;
                }
                catch (IOException)
                {
                    if (fs != null)
                    {
                        fs.Dispose();
                    }
                    Thread.Sleep(2000);
                }
            }
            return null;
        }

    }
}
