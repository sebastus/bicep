// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Bicep.Core.Diagnostics;
using System.IO;
using System.Text;

namespace Bicep.Core.FileSystem
{
    public class InMemoryFileResolver : IFileResolver
    {
        private readonly IReadOnlyDictionary<Uri, string> fileLookup;
        private readonly Func<Uri, string> missingFileFailureBuilder;

        public InMemoryFileResolver(IReadOnlyDictionary<Uri, string> fileLookup, Func<Uri, string>? missingFileFailureBuilder = null)
        {
            this.fileLookup = fileLookup;
            this.missingFileFailureBuilder = missingFileFailureBuilder ?? (fileUri => $"Could not find file \"{fileUri.LocalPath}\"");
        }

        public bool TryRead(Uri fileUri, [NotNullWhen(true)] out string? fileContents, [NotNullWhen(false)] out DiagnosticBuilder.ErrorBuilderDelegate? failureBuilder)
        {
            if (!fileLookup.TryGetValue(fileUri, out fileContents))
            {
                failureBuilder = x => x.ErrorOccurredReadingFile(missingFileFailureBuilder(fileUri));
                fileContents = null;
                return false;
            }
            failureBuilder = null;
            return true;
        }

        public bool TryRead(Uri fileUri, [NotNullWhen(true)] out string? fileContents, [NotNullWhen(false)] out DiagnosticBuilder.ErrorBuilderDelegate? failureBuilder, Encoding fileEncoding, int maxCharacters, [NotNullWhen(true)] out Encoding? detectedEncoding)
        {
            if (!fileLookup.TryGetValue(fileUri, out fileContents))
            {
                failureBuilder = x => x.ErrorOccurredReadingFile(missingFileFailureBuilder(fileUri));
                fileContents = null;
                detectedEncoding = null;
                return false;
            }
            if (maxCharacters > 0)
            {                
                if (fileContents.Length > maxCharacters)
                {
                    failureBuilder = x => x.FileExceedsMaximumSize(fileUri.LocalPath, maxCharacters, "characters");
                    fileContents = null;
                    detectedEncoding = null;
                    return false;
                }
            }
            failureBuilder = null;
            detectedEncoding = fileEncoding;
            return true;
        }

        public Uri? TryResolveFilePath(Uri parentFileUri, string childFilePath)
        {
            if (!Uri.TryCreate(parentFileUri, childFilePath, out var relativeUri))
            {
                return null;
            }

            return relativeUri;
        }

        public bool TryDirExists(Uri fileUri)
        {
            return this.fileLookup.Keys
                .Any(key => key.ToString().StartsWith(fileUri.ToString()));
        }

        public IEnumerable<Uri> GetDirectories(Uri fileUri, string pattern)
        {
            return Enumerable.Empty<Uri>();
        }

        public IEnumerable<Uri> GetFiles(Uri fileUri, string pattern)
        {
            return fileLookup.Keys;
        }

        public bool TryReadAsBase64(Uri fileUri, [NotNullWhen(true)] out string? fileBase64, [NotNullWhen(false)] out DiagnosticBuilder.ErrorBuilderDelegate? failureBuilder, int maxCharacters = -1)
        {
            if (!fileLookup.TryGetValue(fileUri, out var fileContents))
            {
                failureBuilder = x => x.ErrorOccurredReadingFile(missingFileFailureBuilder(fileUri));
                fileBase64 = null;
                return false;
            }

            failureBuilder = null;
            var bytes = Encoding.UTF8.GetBytes(fileContents);
            if (maxCharacters > 0)
            {
                var maxBytes = maxCharacters / 4 * 3; //each base64 character represents 6 bits
                if (bytes.Length > maxBytes)
                {
                    failureBuilder = x => x.FileExceedsMaximumSize(fileUri.LocalPath, maxBytes, "bytes");
                    fileBase64 = null;
                    return false;
                }
            }
            fileBase64 = Convert.ToBase64String(bytes);           
            return true;
        }
    }
}
