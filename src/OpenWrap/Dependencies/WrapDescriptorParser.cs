using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using OpenFileSystem.IO;

namespace OpenWrap.Dependencies
{
    public class WrapDescriptorParser
    {
        const int FILE_READ_RETRIES = 5;
        const int FILE_READ_RETRIES_WAIT = 500;
        

        readonly IEnumerable<IDescriptorParser> _lineParsers = new List<IDescriptorParser>
        {
            new DependsParser(),
            new DescriptionParser(),
            new OverrideParser(),
            new AnchorParser()
        };

        public WrapDescriptor ParseFile(IFile filePath)
        {
            int tries = 0;
            while (tries < FILE_READ_RETRIES)
            {
                try
                {
                    using (var fileStream = filePath.OpenRead())
                    {
                        return ParseFile(filePath, fileStream);
                    }
                }
                catch (IOException)
                {
                    tries++;
                    Thread.Sleep(FILE_READ_RETRIES_WAIT);
                    continue;
                }
            }
            return null;
        }

        public WrapDescriptor ParseFile(IFile filePath, Stream content)
        {
            var stringReader = new StreamReader(content, true);
            var lines = stringReader.ReadToEnd().GetUnfoldedLines();
            IFile versionFile;
            if (filePath.Parent != null && (versionFile = filePath.Parent.GetFile("version")).Exists)
            {
                using(var versionStream = versionFile.OpenRead())
                lines.Concat(new[] { "version: " } + versionStream.ReadString(Encoding.UTF8));
            }
            var descriptor = new WrapDescriptor
            {
                Name = WrapNameUtility.GetName(filePath.NameWithoutExtension),
                Version = WrapNameUtility.GetVersion(filePath.NameWithoutExtension),
                File = filePath
            };
            foreach (var line in lines)
                foreach (var parser in _lineParsers)
                    parser.Parse(line, descriptor);
            
            return descriptor;
        }
    }
    public static class StringExtensions
    {
        static readonly Regex _foldableLines = new Regex(@"\r\n[\f\t\v\x85\p{Z}]+", RegexOptions.Multiline | RegexOptions.Compiled);
        public static string[] GetUnfoldedLines(this string content)
        {
            content = _foldableLines.Replace(content, " ");
            return content.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x != string.Empty)
                    .ToArray();
        }
    }
}