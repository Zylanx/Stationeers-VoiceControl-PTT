using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using UnityEngine;
using FieldAttributes = Mono.Cecil.FieldAttributes;

namespace SE_PTTVoiceControl
{
    class Patcher
    {
        public const string NormalExt = ".dll";
        public const string BackupExt = ".orig.dll";
        public const string TempExt = ".temp.dll";

        public string AssemblyName;
        public string AssemblyBackupName;
        public string AssemblyTempName;

        public string SERootDir;
        public string BackupDir;

        private string _localDir;

        public Patcher(string seRootDir, string assemblyName = "Assembly-CSharp.dll")
        {
            SERootDir = seRootDir;
            AssemblyName = assemblyName;

            AssemblyBackupName = Path.ChangeExtension(AssemblyName, BackupExt);
            AssemblyTempName = Path.ChangeExtension(AssemblyName, TempExt);

            BackupDir = GetBackupDir();
            _localDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (!ValidGameDir())
            {
                Console.WriteLine("Invalid Stationeers Directory");
                throw new ArgumentException("Invalid Stationeers Directory", "seRootDir");
            }
        }

        public string GetBackupDir()
        {
            return Path.Combine(SERootDir, "Unpatched");
        }

        public bool ValidGameDir()
        {
            if (!File.Exists(Path.Combine(SERootDir, AssemblyName)))
            {
                return false;
            }

            return true;
        }

        public void PatchAssembly()
        {
            SetupFolders();
            BackupFiles();

            using (var cSharpAssembly = LoadAssembly(Path.Combine(SERootDir, AssemblyName)))
            {
                ModuleDefinition cSharpAssemblyModule = cSharpAssembly.MainModule;

                cSharpAssemblyModule.ImportReference(typeof(Input));
                MethodReference inputGetKeyDown =
                    cSharpAssemblyModule.ImportReference(typeof(Input).GetMethod("GetKeyDown", new[] { typeof(string) }));

                TypeDefinition speechRecognizer = cSharpAssemblyModule.Types.FirstOrDefault(t => (t.Name == "SpeechRecognizer"));
                MethodDefinition onRecognizedMethod = speechRecognizer.Methods.FirstOrDefault(m => (m.Name == "KeywordRecognizerOnPhraseRecognized"));
                onRecognizedMethod.Body.SimplifyMacros();

                ILProcessor processorOnRecognized = onRecognizedMethod.Body.GetILProcessor();

                Instruction insertTarget = onRecognizedMethod.Body.Instructions.FirstOrDefault(i =>
                    i.OpCode.Name == "call" && ((MemberReference)i.Operand).Name == "GetWindowVersion");
                Instruction ifFalseJumpTarget = (Instruction)insertTarget.Next.Next.Next.Operand;

                List<Instruction> ifInstrs = new List<Instruction>();

                ifInstrs.Add(processorOnRecognized.Create(OpCodes.Ldstr, "Period"));
                ifInstrs.Add(processorOnRecognized.Create(OpCodes.Call, inputGetKeyDown));
                ifInstrs.Add(processorOnRecognized.Create(OpCodes.Brfalse, ifFalseJumpTarget));

                foreach (Instruction instr in ifInstrs)
                {
                    processorOnRecognized.InsertBefore(insertTarget, instr);
                }

                onRecognizedMethod.Body.OptimizeMacros();

                cSharpAssembly.Write(Path.Combine(_localDir, AssemblyName));
            }
        }

        public void SetupFolders()
        {
            if (!Directory.Exists(BackupDir))
            {
                Directory.CreateDirectory(BackupDir);
            }
        }

        public void BackupFiles()
        {
            File.Copy(Path.Combine(SERootDir, AssemblyName), Path.Combine(BackupDir, AssemblyBackupName), true);
        }

        public AssemblyDefinition LoadAssembly(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    var resolver = new DefaultAssemblyResolver();
                    resolver.AddSearchDirectory(SERootDir);
                    resolver.AddSearchDirectory(_localDir);
                    AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver, InMemory = true, ReadWrite = true });
                    return assembly;
                }
                catch (Exception e)
                {
                    Console.WriteLine(string.Format("Couldn't load assembly {0}{1}{2}", path, Environment.NewLine, e.Message));
                }
            }
            else
            {
                Console.WriteLine(string.Format("Assembly {0} doesn't exist", path));
            }

            return null;
        }
    }
}
