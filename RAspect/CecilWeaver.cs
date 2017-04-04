using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect
{
    /// <summary>
    /// Cecil weaver for assemblies
    /// </summary>
    public class CecilWeaver
    {
        /// <summary>
        /// FileName
        /// </summary>
        string fileName;

        /// <summary>
        /// Flag indicating if assembly has pdb
        /// </summary>
        bool hasPDB;

        /// <summary>
        /// Module Definition
        /// </summary>
        ModuleDefinition moduleDefinition;

        /// <summary>
        /// Logger
        /// </summary>
        private Action<string> log = message => { };

        /// <summary>
        /// Initializes a new instance of the <see cref="CecilWeaver"/> class.
        /// </summary>
        /// <param name="path"></param>
        public CecilWeaver(string fileName, Action<string> logger = null)
        {
            this.log = logger ?? this.log;
            this.fileName = fileName;
            var fileInfo = new FileInfo(this.fileName);
            var directory = fileInfo.Directory.FullName;
            var pdbFileInfo = new FileInfo(Path.Combine(directory, Path.GetFileNameWithoutExtension(fileInfo.Name) + ".pdb"));
            
            hasPDB = pdbFileInfo.Exists;

            Backup(fileInfo, pdbFileInfo);
            
            moduleDefinition = ModuleDefinition.ReadModule(fileName, new ReaderParameters() { ReadSymbols = hasPDB });

            CecilExtensions.module = moduleDefinition;
        }

        /// <summary>
        /// Process current assembly
        /// </summary>
        public void Process()
        {
            ILWeaver.Weave(moduleDefinition);
            moduleDefinition.Write(fileName, new WriterParameters { WriteSymbols = hasPDB });
        }

        /// <summary>
        /// Backup original dll before weaving
        /// </summary>
        /// <param name="assembly">Assembly</param>
        /// <param name="pdb">Assembly's PDB</param>
        private void Backup(FileInfo assembly, FileInfo pdb)
        {
            try
            {
                var directory = assembly.Directory;
                var backupPath = Path.Combine(directory.FullName, "Original");
                var backupDirectory = new DirectoryInfo(backupPath);

                if (!backupDirectory.Exists)
                {
                    backupDirectory.Create();
                }

                if (assembly.Exists)
                {
                    LogMessage("Backing up {0}", assembly.FullName);
                    assembly.CopyTo(Path.Combine(backupPath, assembly.Name), true);
                    assembly.Directory.GetFiles().Where(x => x.Extension.IndexOf("dll", StringComparison.OrdinalIgnoreCase) >= 0) 
                        .ToList().ForEach(f => f.CopyTo(Path.Combine(backupPath, f.Name), true));
                }

                if (pdb.Exists)
                {
                    LogMessage("Backing up {0}", pdb.FullName);
                    pdb.CopyTo(Path.Combine(backupPath, pdb.Name), true);
                }
            }
            catch(Exception ex)
            {
                /*Swallow exception*/
                LogMessage("Something went wrong while backing up!!!. Exception: {0}", ex.Message);                
            }
        }

        /// <summary>
        /// Log message to underlying logger
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="args">Arguments</param>
        private void LogMessage(string message, params object[] args)
        {
            if(log == null)
            {
                return;
            }

            log(string.Format(message, args));
        }
    }
}
