using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Compression;
using System.Linq;

namespace uwap.VSIX.ReleaseCollector
{
    internal class Options : DialogPage
    {
        private string _Systems = "linux-x64,linux-arm,linux-arm64,win-x64,win-arm64";
        [Category("ReleaseCollector")]
        [DisplayName("Systems")]
        [Description("The systems to release for according to the dotnet command documentation, separated by a comma/semicolon/space.")]
        public string Systems
        {
            get { return _Systems; }
            set { _Systems = value; }
        }
        internal IEnumerable<string> SystemsClean
            => _Systems.Split(',', ';', ' ').Select(x => x.Trim()).Where(x => x != "");

        private string _IgnoredExtensions = "pdb,json,config";
        [Category("ReleaseCollector")]
        [DisplayName("IgnoredExtensions")]
        [Description("The file extensions (without the preceding dot) that should be ignored in generated builds, separated by a comma/semicolon/space.")]
        public string IgnoredExtensions
        {
            get { return _IgnoredExtensions; }
            set { _IgnoredExtensions = value; }
        }
        internal IEnumerable<string> IgnoredExtensionsClean
            => _IgnoredExtensions.Split(',', ';', ' ').Select(x => x.Trim()).Where(x => x != "").Select(x => x[0] == '.' ? ("*"+x) : ("*."+x));

        private SCMode _SelfContainedMode = SCMode.Yes;
        [Category("ReleaseCollector")]
        [DisplayName("SelfContainedMode")]
        [Description("Whether to additionally generate self-contained binaries (don't require dotnet to be installed, framework-dependent is always generated). Note that selecting the trimmed option can cause major issues in the SC binaries, research 'c# trim warning' and make sure you are handling it properly, otherwise avoid it. The benefit of trimming is a significantly lower file size, but publishing takes a lot longer.")]
        public SCMode SelfContainedMode
        {
            get { return _SelfContainedMode; }
            set { _SelfContainedMode = value; }
        }
        public enum SCMode
        {
            No,
            Yes,
            Trimmed_DANGEROUS
        }

        private bool _ZipSubfolder = false;
        [Category("ReleaseCollector")]
        [DisplayName("ZipSubfolder")]
        [Description("Whether to put the files for .zip files into a subfolder within the .zip file (true) or directly into the .zip file (false).")]
        public bool ZipSubfolder
        {
            get { return _ZipSubfolder; }
            set { _ZipSubfolder = value; }
        }

        private CompressionLevel _ZipCompression = CompressionLevel.Fastest;
        [Category("ReleaseCollector")]
        [DisplayName("ZipCompression")]
        [Description("The level of ZIP compression to use when combining multiple generated files into a .zip file.")]
        public CompressionLevel ZipCompression
        {
            get { return _ZipCompression; }
            set { _ZipCompression = value; }
        }
    }
}
