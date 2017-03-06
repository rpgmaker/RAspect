using Mono.Cecil;
using System;
using System.Collections.Generic;
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
        /// Initializes a new instance of the <see cref="CecilWeaver"/> class.
        /// </summary>
        /// <param name="path"></param>
        public CecilWeaver(string fileName)
        {
            this.fileName = fileName;
            var fileInfo = new FileInfo(this.fileName);
            var pdbFileInfo = new FileInfo(Path.Combine(fileInfo.Directory.FullName, fileInfo.Name + ".pdb"));

            hasPDB = pdbFileInfo.Exists;

            moduleDefinition = ModuleDefinition.ReadModule(fileName, new ReaderParameters() { ReadSymbols = hasPDB });
            CecilExtensions.module = moduleDefinition;
        }

        public void Weave()
        {
            ILWeaver.Weave(moduleDefinition);
        }

        public void Process()
        {
            Weave();
            moduleDefinition.Write(fileName, new WriterParameters { WriteSymbols = hasPDB });
        }
    }
}
