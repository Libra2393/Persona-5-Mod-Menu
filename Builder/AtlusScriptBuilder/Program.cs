using AtlusScriptHelper;
using AtlusScriptLibrary;
using AtlusScriptLibrary.Common.Text.Encodings;

namespace AtlusScriptBuilder
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //AtlusScriptHelper.ScriptHelper.DecompileBFToFLOW("FIELD.BF", "FIELD.FLOW");
            //AtlusScriptHelper.ScriptHelper.CompileFLOWToBF("FIELD.FLOW", "FIELD.FLOW.BF");
            //AtlusScriptHelper.ScriptHelper.CompileMSGToBMD("FIELD.msg", "FIELD_2.bmd");
            //AtlusScriptHelper.ScriptHelper.DecompileBMDToMSG("FIELD.bmd", "field_2.msg");

            // Mod folder: first argument if given, otherwise found by walking up from the
            // current directory and the program's own folder. Works with `dotnet run`, from
            // bin/Debug/net8.0, and on Linux, where the old fixed "../../../../../" path broke.
            string modPath = FindModPath(args);
            Console.WriteLine($"Using mod folder: {modPath}");
            string msgFile = modPath + "ModMenuStrings.msg";
            string fieldFlowPath = modPath + "FEmulator/BF/FIELD/ETC/FIELD.flow";
            // Must match FIELD.flow's import("FIELD.MSG") exactly: Linux filesystems are case-sensitive.
            // (Upper case is deliberate: ScriptHelper.RemoveMsgImports only strips lower-case ".msg" imports.)
            string fieldMsgPath = fieldFlowPath.Replace(".flow", ".MSG");
            string fieldBfPath = modPath + "P5REssentials/CPK/DUMMYFILES.CPK/FIELD/ETC/FIELD.BF";
            string fieldBfPathRelativeToFlow = "../../../../P5REssentials/CPK/DUMMYFILES.CPK/FIELD/ETC/FIELD.BF";

            
            // Create a copy of the Mod Menu strings file with comments removed
            ScriptHelper.RemoveMsgComments(msgFile, fieldMsgPath);

            // Remove comments from the .flow file (fixes compilation issues for older game versions)
            foreach (var script in Directory.GetFiles(modPath, "*.flow", SearchOption.AllDirectories))
                ScriptHelper.RemoveFlowComments(script);

            

            // Update the indexes of [ref] blocks and [msg] names to reflect decompiled .bf (fixes menu descriptions)
            ScriptHelper.ReindexMsgs(fieldFlowPath, fieldMsgPath, fieldBfPathRelativeToFlow);

            //ScriptHelper.RunCompiler($"-Compile \"{Path.GetFullPath(fieldFlowPath)}\" -Library P5R -Encoding P5R_EFIGS -OutFormat V3BE -Hook -SumBits -Out \"{fieldBfPath}\"");
            // Test compile only: BF Emulator compiles the loose .flow files when the game loads.
            // Output goes to <repo>/build/ so nothing lands next to the binary or in the mod folder.
            string buildDir = Path.GetFullPath(Path.Combine(modPath, "..", "build"));
            Directory.CreateDirectory(buildDir);
            ScriptHelper.CompileFLOWToBF(fieldFlowPath, Path.Combine(buildDir, "FIELD.FLOW.BF"));
        }

        const string ModFolderName = "p5rpc.modmenu";

        static string FindModPath(string[] args)
        {
            if (args.Length > 0)
            {
                string fromArg = Path.GetFullPath(args[0]);
                if (Directory.Exists(Path.Combine(fromArg, ModFolderName)))
                    fromArg = Path.Combine(fromArg, ModFolderName);
                if (!File.Exists(Path.Combine(fromArg, "ModConfig.json")))
                    throw new DirectoryNotFoundException($"No {ModFolderName}/ModConfig.json at: {fromArg}");
                return fromArg + Path.DirectorySeparatorChar;
            }

            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, ModFolderName);
                    if (File.Exists(Path.Combine(candidate, "ModConfig.json")))
                        return candidate + Path.DirectorySeparatorChar;
                }
            }

            throw new DirectoryNotFoundException(
                $"Couldn't find the {ModFolderName} folder. Run from inside the repo, or pass the repo path as the first argument.");
        }
    }
}
