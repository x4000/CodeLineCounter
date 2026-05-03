using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CodeLineCounter
{
    public sealed class CountResult
    {
        public string Label;
        public int Files;
        public long CodeLines;
        public long CommentLines;
        public long BlankLines;

        public CountResult( string label ) { this.Label = label; }
    }

    public sealed class ScopeBucket
    {
        public string DisplayLabel;
        public bool IsUnity;

        public CountResult CSharp = new CountResult( "C#" );                 // outside-of-Unity bucket
        public CountResult RuntimeCSharp = new CountResult( "Runtime C#" );  // Unity-only
        public CountResult EditorCSharp = new CountResult( "Editor C#" );    // Unity-only
        public CountResult Shaders = new CountResult( "shaders" );
        public CountResult Xml = new CountResult( "XML" );
        public CountResult XmlMetadata = new CountResult( "Xml Metadata" );
        public CountResult Cpp = new CountResult( "C++" );
        public CountResult Js = new CountResult( "JavaScript" );
        public CountResult Java = new CountResult( "Java" );
        public CountResult Html = new CountResult( "HTML" );
        public CountResult Lua = new CountResult( "Lua" );
        public CountResult Python = new CountResult( "Python" );
        public CountResult Json = new CountResult( "JSON" );
        public CountResult Bash = new CountResult( "Bash" );
        public CountResult Batch = new CountResult( "Batch" );

        public ScopeBucket( string label, bool isUnity )
        {
            DisplayLabel = label;
            IsUnity = isUnity;
        }

        public IEnumerable<CountResult> All()
        {
            yield return RuntimeCSharp;
            yield return EditorCSharp;
            yield return CSharp;
            yield return Shaders;
            yield return Xml;
            yield return XmlMetadata;
            yield return Cpp;
            yield return Js;
            yield return Java;
            yield return Html;
            yield return Lua;
            yield return Python;
            yield return Json;
            yield return Bash;
            yield return Batch;
        }

        public bool HasAnything()
        {
            foreach ( CountResult r in All() )
                if ( r.Files > 0 || r.CodeLines > 0 || r.CommentLines > 0 )
                    return true;
            return false;
        }
    }

    public static class LineCounter
    {
        private static readonly HashSet<string> ExcludedDirNames = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".git", ".svn", ".hg", ".bzr",
            ".vs", ".vscode", ".idea",
            "bin", "obj",
            "node_modules",
            "Library", "Temp"
        };

        private static readonly string[] UnityProjectMarkers = new[]
        {
            "Assets", "Library", "Packages", "ProjectSettings"
        };

        private static readonly HashSet<string> CSharpExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".cs"
        };

        private static readonly HashSet<string> ShaderExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".shader", ".shadergraph", ".shadersubgraph",
            ".cginc", ".compute",
            ".hlsl", ".hlsli",
            ".glsl", ".glslinc",
            ".vert", ".frag", ".geom", ".tesc", ".tese",
            ".fx", ".fxh",
            ".usf", ".ush"
        };

        private static readonly HashSet<string> XmlExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".xml"
        };

        private static readonly HashSet<string> XmlMetadataExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".metadata"
        };

        private const string ArcenXmlMarkerFileName = "SharedMetaData.metadata";

        private static readonly HashSet<string> CppExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".cpp", ".cc", ".cxx", ".c++", ".c",
            ".h", ".hpp", ".hxx", ".hh", ".h++", ".inl"
        };

        private static readonly HashSet<string> JavaScriptExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".js", ".mjs", ".cjs", ".jsx"
        };

        private static readonly HashSet<string> JavaExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".java"
        };

        private static readonly HashSet<string> HtmlExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".html", ".htm"
        };

        private static readonly HashSet<string> LuaExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".lua"
        };

        private static readonly HashSet<string> PythonExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".py", ".pyw"
        };

        private static readonly HashSet<string> JsonExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".json"
        };

        private static readonly HashSet<string> BashExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".sh", ".bash"
        };

        private static readonly HashSet<string> BatchExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
        {
            ".bat", ".cmd"
        };

        private struct StackEntry
        {
            public string Dir;
            public ScopeBucket Scope;
            public bool InEditorFolder;
            public bool InArcenXml;
        }

        public static void RunCount( string root, Action<string> log )
        {
            log( "Scanning: " + root );
            log( "" );
            DateTime start = DateTime.Now;

            ScopeBucket outsideScope = new ScopeBucket( "Outside of Unity projects", false );
            List<ScopeBucket> unityScopes = new List<ScopeBucket>();

            HashSet<string> seenHashes = new HashSet<string>( StringComparer.Ordinal );

            long otherFiles = 0;
            long skippedDirs = 0;
            long errorCount = 0;
            long duplicateFiles = 0;

            Stack<StackEntry> stack = new Stack<StackEntry>();
            stack.Push( new StackEntry { Dir = root, Scope = outsideScope, InEditorFolder = false, InArcenXml = false } );

            while ( stack.Count > 0 )
            {
                StackEntry current = stack.Pop();
                string dir = current.Dir;
                ScopeBucket scope = current.Scope;
                bool inEditorFolder = current.InEditorFolder;
                bool inArcenXml = current.InArcenXml;

                string[] subdirs;
                string[] files;
                try
                {
                    subdirs = Directory.GetDirectories( dir );
                    files = Directory.GetFiles( dir );
                }
                catch ( UnauthorizedAccessException )
                {
                    log( "  (access denied: " + dir + ")" );
                    errorCount++;
                    continue;
                }
                catch ( Exception ex )
                {
                    log( "  (error reading " + dir + ": " + ex.Message + ")" );
                    errorCount++;
                    continue;
                }

                bool isUnityRoot = scope == outsideScope && IsUnityProjectRoot( subdirs );
                ScopeBucket scopeForChildren = scope;
                if ( isUnityRoot )
                {
                    ScopeBucket unityScope = new ScopeBucket( "Unity project (" + Path.GetFileName( dir ) + ")", true );
                    unityScopes.Add( unityScope );
                    scopeForChildren = unityScope;
                    log( "  Unity project detected; scanning Assets only: " + dir );
                }

                bool entersArcenXml = !inArcenXml && HasArcenXmlMarker( files );
                bool inArcenXmlForChildren = inArcenXml || entersArcenXml;
                if ( entersArcenXml )
                    log( "  Arcen Xml folder detected; ignoring JSON within: " + dir );

                foreach ( string sub in subdirs )
                {
                    string name = Path.GetFileName( sub );
                    if ( ExcludedDirNames.Contains( name ) )
                    {
                        skippedDirs++;
                        continue;
                    }
                    if ( isUnityRoot && !string.Equals( name, "Assets", StringComparison.OrdinalIgnoreCase ) )
                    {
                        skippedDirs++;
                        continue;
                    }
                    bool childInEditorFolder = inEditorFolder
                        || string.Equals( name, "Editor", StringComparison.OrdinalIgnoreCase );
                    stack.Push( new StackEntry
                    {
                        Dir = sub,
                        Scope = scopeForChildren,
                        InEditorFolder = childInEditorFolder,
                        InArcenXml = inArcenXmlForChildren
                    } );
                }

                foreach ( string file in files )
                {
                    string ext = Path.GetExtension( file );
                    if ( inArcenXmlForChildren && JsonExtensions.Contains( ext ) )
                        continue;
                    CountResult target = ResolveTarget( ext, scopeForChildren, inEditorFolder );
                    if ( target == null )
                    {
                        otherFiles++;
                        continue;
                    }

                    List<string> lines;
                    FileReadOutcome outcome = TryReadFile( file, seenHashes, log, out lines );
                    if ( outcome == FileReadOutcome.Error )
                    {
                        errorCount++;
                        continue;
                    }
                    if ( outcome == FileReadOutcome.Duplicate )
                    {
                        duplicateFiles++;
                        continue;
                    }

                    target.Files++;
                    DispatchParser( ext, lines, target );
                }
            }

            TimeSpan elapsed = DateTime.Now - start;

            log( "" );

            unityScopes.Sort( ( a, b ) => string.Compare( a.DisplayLabel, b.DisplayLabel, StringComparison.OrdinalIgnoreCase ) );
            foreach ( ScopeBucket bucket in unityScopes )
                EmitSection( bucket, log );

            EmitSection( outsideScope, log );

            EmitGrandTotal( outsideScope, unityScopes, log );

            log( "" );
            log( string.Format( "(also: {0:N0} other files not counted; {1:N0} subfolders skipped by exclusion list; {2:N0} duplicate files skipped; {3:N0} read errors)",
                otherFiles, skippedDirs, duplicateFiles, errorCount ) );
            log( string.Format( "Done in {0:0.0}s.", elapsed.TotalSeconds ) );
        }

        private static CountResult ResolveTarget( string ext, ScopeBucket scope, bool inEditorFolder )
        {
            if ( CSharpExtensions.Contains( ext ) )
            {
                if ( scope.IsUnity )
                    return inEditorFolder ? scope.EditorCSharp : scope.RuntimeCSharp;
                return scope.CSharp;
            }
            if ( ShaderExtensions.Contains( ext ) ) return scope.Shaders;
            if ( CppExtensions.Contains( ext ) ) return scope.Cpp;
            if ( JavaScriptExtensions.Contains( ext ) ) return scope.Js;
            if ( JavaExtensions.Contains( ext ) ) return scope.Java;
            if ( XmlExtensions.Contains( ext ) ) return scope.Xml;
            if ( XmlMetadataExtensions.Contains( ext ) ) return scope.XmlMetadata;
            if ( HtmlExtensions.Contains( ext ) ) return scope.Html;
            if ( LuaExtensions.Contains( ext ) ) return scope.Lua;
            if ( PythonExtensions.Contains( ext ) ) return scope.Python;
            if ( JsonExtensions.Contains( ext ) ) return scope.Json;
            if ( BashExtensions.Contains( ext ) ) return scope.Bash;
            if ( BatchExtensions.Contains( ext ) ) return scope.Batch;
            return null;
        }

        private static void DispatchParser( string ext, List<string> lines, CountResult result )
        {
            if ( CSharpExtensions.Contains( ext )
                || ShaderExtensions.Contains( ext )
                || CppExtensions.Contains( ext )
                || JavaScriptExtensions.Contains( ext )
                || JavaExtensions.Contains( ext ) )
            {
                ParseCStyle( lines, result );
            }
            else if ( XmlExtensions.Contains( ext ) || XmlMetadataExtensions.Contains( ext ) || HtmlExtensions.Contains( ext ) )
            {
                ParseXmlStyle( lines, result );
            }
            else if ( LuaExtensions.Contains( ext ) )
            {
                ParseLua( lines, result );
            }
            else if ( PythonExtensions.Contains( ext ) )
            {
                ParsePython( lines, result );
            }
            else if ( JsonExtensions.Contains( ext ) )
            {
                ParseJson( lines, result );
            }
            else if ( BashExtensions.Contains( ext ) )
            {
                ParseBash( lines, result );
            }
            else if ( BatchExtensions.Contains( ext ) )
            {
                ParseBatch( lines, result );
            }
        }

        private static void EmitSection( ScopeBucket bucket, Action<string> log )
        {
            if ( !bucket.HasAnything() )
                return;

            log( bucket.DisplayLabel + ":" );

            int categoriesShown = 0;
            int fileSubtotal = 0;
            long codeSubtotal = 0;
            long commentSubtotal = 0;

            foreach ( CountResult r in bucket.All() )
            {
                if ( r.Files == 0 && r.CodeLines == 0 && r.CommentLines == 0 )
                    continue;
                log( "  " + FormatLine( r ) );
                categoriesShown++;
                fileSubtotal += r.Files;
                codeSubtotal += r.CodeLines;
                commentSubtotal += r.CommentLines;
            }

            if ( categoriesShown > 1 )
            {
                CountResult sub = new CountResult( "subtotal" );
                sub.Files = fileSubtotal;
                sub.CodeLines = codeSubtotal;
                sub.CommentLines = commentSubtotal;
                log( "  " + FormatLine( sub ) );
            }
            log( "" );
        }

        private static void EmitGrandTotal( ScopeBucket outsideScope, List<ScopeBucket> unityScopes, Action<string> log )
        {
            int populatedScopes = ( outsideScope.HasAnything() ? 1 : 0 );
            foreach ( ScopeBucket b in unityScopes )
                if ( b.HasAnything() ) populatedScopes++;

            if ( populatedScopes <= 1 )
                return;

            CountResult grand = new CountResult( "GRAND TOTAL" );
            AddInto( grand, outsideScope );
            foreach ( ScopeBucket b in unityScopes )
                AddInto( grand, b );

            log( FormatLine( grand ) );
        }

        private static void AddInto( CountResult target, ScopeBucket b )
        {
            foreach ( CountResult r in b.All() )
            {
                target.Files += r.Files;
                target.CodeLines += r.CodeLines;
                target.CommentLines += r.CommentLines;
                target.BlankLines += r.BlankLines;
            }
        }

        private static bool IsUnityProjectRoot( string[] subdirs )
        {
            HashSet<string> names = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
            foreach ( string s in subdirs )
                names.Add( Path.GetFileName( s ) );
            foreach ( string marker in UnityProjectMarkers )
                if ( !names.Contains( marker ) )
                    return false;
            return true;
        }

        private static bool HasArcenXmlMarker( string[] files )
        {
            foreach ( string f in files )
            {
                if ( string.Equals( Path.GetFileName( f ), ArcenXmlMarkerFileName, StringComparison.OrdinalIgnoreCase ) )
                    return true;
            }
            return false;
        }

        private static string FormatLine( CountResult r )
        {
            if ( r.CommentLines == 0 )
            {
                return string.Format( "{0,12:N0} lines of {1} across {2:N0} files",
                    r.CodeLines, r.Label, r.Files );
            }
            return string.Format( "{0,12:N0} lines of {1} (+{2:N0} lines of comments) across {3:N0} files",
                r.CodeLines, r.Label, r.CommentLines, r.Files );
        }

        // --- File I/O with deduplication ---

        private enum FileReadOutcome { Success, Duplicate, Error }

        private static FileReadOutcome TryReadFile( string path, HashSet<string> seenHashes, Action<string> log, out List<string> lines )
        {
            lines = null;
            byte[] bytes;
            try { bytes = File.ReadAllBytes( path ); }
            catch ( Exception ex )
            {
                log( "  (error reading " + path + ": " + ex.Message + ")" );
                return FileReadOutcome.Error;
            }

            string hash = ComputeMd5Hex( bytes );
            if ( !seenHashes.Add( hash ) )
                return FileReadOutcome.Duplicate;

            string text = DecodeText( bytes );
            lines = SplitLines( text );
            return FileReadOutcome.Success;
        }

        private static string ComputeMd5Hex( byte[] bytes )
        {
            using ( MD5 md5 = MD5.Create() )
            {
                byte[] hash = md5.ComputeHash( bytes );
                StringBuilder sb = new StringBuilder( hash.Length * 2 );
                for ( int i = 0; i < hash.Length; i++ )
                    sb.Append( hash[i].ToString( "x2" ) );
                return sb.ToString();
            }
        }

        private static string DecodeText( byte[] bytes )
        {
            if ( bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF )
                return Encoding.UTF8.GetString( bytes, 3, bytes.Length - 3 );
            if ( bytes.Length >= 2 )
            {
                if ( bytes[0] == 0xFF && bytes[1] == 0xFE )
                    return Encoding.Unicode.GetString( bytes, 2, bytes.Length - 2 );
                if ( bytes[0] == 0xFE && bytes[1] == 0xFF )
                    return Encoding.BigEndianUnicode.GetString( bytes, 2, bytes.Length - 2 );
            }
            return Encoding.UTF8.GetString( bytes );
        }

        private static List<string> SplitLines( string text )
        {
            List<string> result = new List<string>();
            int start = 0;
            int i = 0;
            while ( i < text.Length )
            {
                char c = text[i];
                if ( c == '\r' || c == '\n' )
                {
                    result.Add( text.Substring( start, i - start ) );
                    if ( c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' )
                        i += 2;
                    else
                        i++;
                    start = i;
                }
                else
                {
                    i++;
                }
            }
            if ( start < text.Length )
                result.Add( text.Substring( start ) );
            return result;
        }

        // --- Parsers (operate on pre-read line lists) ---

        private static void ParseCStyle( List<string> lines, CountResult result )
        {
            bool inBlockComment = false;
            bool inVerbatimString = false;   // C# @"..." with "" escape
            bool inTemplateLiteral = false;  // JS `...` with \ escape

            foreach ( string line in lines )
            {
                bool hasCode = false;
                bool hasComment = false;
                int i = 0;
                int n = line.Length;

                while ( i < n )
                {
                    if ( inBlockComment )
                    {
                        hasComment = true;
                        int idx = line.IndexOf( "*/", i, StringComparison.Ordinal );
                        if ( idx < 0 ) { i = n; break; }
                        inBlockComment = false;
                        i = idx + 2;
                        continue;
                    }
                    if ( inVerbatimString )
                    {
                        hasCode = true;
                        bool closed = false;
                        while ( i < n )
                        {
                            if ( line[i] == '"' )
                            {
                                if ( i + 1 < n && line[i + 1] == '"' ) { i += 2; continue; }
                                inVerbatimString = false;
                                i++;
                                closed = true;
                                break;
                            }
                            i++;
                        }
                        if ( !closed ) break;
                        continue;
                    }
                    if ( inTemplateLiteral )
                    {
                        hasCode = true;
                        bool closed = false;
                        while ( i < n )
                        {
                            if ( line[i] == '\\' && i + 1 < n ) { i += 2; continue; }
                            if ( line[i] == '`' )
                            {
                                inTemplateLiteral = false;
                                i++;
                                closed = true;
                                break;
                            }
                            i++;
                        }
                        if ( !closed ) break;
                        continue;
                    }

                    char c = line[i];
                    char next = ( i + 1 < n ) ? line[i + 1] : '\0';

                    if ( c == '/' && next == '/' )
                    {
                        hasComment = true;
                        break;
                    }
                    if ( c == '/' && next == '*' )
                    {
                        hasComment = true;
                        inBlockComment = true;
                        i += 2;
                        int idx = line.IndexOf( "*/", i, StringComparison.Ordinal );
                        if ( idx >= 0 ) { inBlockComment = false; i = idx + 2; }
                        else { i = n; }
                        continue;
                    }
                    if ( ( c == '@' && next == '"' )
                        || ( c == '$' && next == '@' && i + 2 < n && line[i + 2] == '"' )
                        || ( c == '@' && next == '$' && i + 2 < n && line[i + 2] == '"' ) )
                    {
                        hasCode = true;
                        i = ( c == '@' && next == '"' ) ? i + 2 : i + 3;
                        inVerbatimString = true;
                        continue;
                    }
                    if ( c == '$' && next == '"' )
                    {
                        hasCode = true;
                        i += 2;
                        ConsumeRegularString( line, ref i, n, '"' );
                        continue;
                    }
                    if ( c == '"' )
                    {
                        hasCode = true;
                        i++;
                        ConsumeRegularString( line, ref i, n, '"' );
                        continue;
                    }
                    if ( c == '\'' )
                    {
                        hasCode = true;
                        i++;
                        ConsumeRegularString( line, ref i, n, '\'' );
                        continue;
                    }
                    if ( c == '`' )
                    {
                        hasCode = true;
                        inTemplateLiteral = true;
                        i++;
                        continue;
                    }
                    if ( !char.IsWhiteSpace( c ) )
                        hasCode = true;
                    i++;
                }

                if ( hasCode ) result.CodeLines++;
                else if ( hasComment ) result.CommentLines++;
                else result.BlankLines++;
            }
        }

        private static void ConsumeRegularString( string line, ref int i, int n, char quote )
        {
            while ( i < n )
            {
                if ( line[i] == '\\' && i + 1 < n ) { i += 2; continue; }
                if ( line[i] == quote ) { i++; return; }
                i++;
            }
        }

        private static void ParseXmlStyle( List<string> lines, CountResult result )
        {
            bool inComment = false;

            foreach ( string line in lines )
            {
                bool hasCode = false;
                bool hasComment = false;
                int i = 0;
                int n = line.Length;

                while ( i < n )
                {
                    if ( inComment )
                    {
                        hasComment = true;
                        int idx = line.IndexOf( "-->", i, StringComparison.Ordinal );
                        if ( idx < 0 ) { i = n; break; }
                        inComment = false;
                        i = idx + 3;
                        continue;
                    }
                    if ( i + 4 <= n && line[i] == '<' && line[i + 1] == '!' && line[i + 2] == '-' && line[i + 3] == '-' )
                    {
                        hasComment = true;
                        inComment = true;
                        i += 4;
                        int idx = line.IndexOf( "-->", i, StringComparison.Ordinal );
                        if ( idx >= 0 ) { inComment = false; i = idx + 3; }
                        else { i = n; }
                        continue;
                    }
                    if ( !char.IsWhiteSpace( line[i] ) )
                        hasCode = true;
                    i++;
                }

                if ( hasCode ) result.CodeLines++;
                else if ( hasComment ) result.CommentLines++;
                else result.BlankLines++;
            }
        }

        private static void ParseLua( List<string> lines, CountResult result )
        {
            bool inBlockComment = false;

            foreach ( string line in lines )
            {
                bool hasCode = false;
                bool hasComment = false;
                int i = 0;
                int n = line.Length;

                while ( i < n )
                {
                    if ( inBlockComment )
                    {
                        hasComment = true;
                        int idx = line.IndexOf( "]]", i, StringComparison.Ordinal );
                        if ( idx < 0 ) { i = n; break; }
                        inBlockComment = false;
                        i = idx + 2;
                        continue;
                    }

                    char c = line[i];
                    char next = ( i + 1 < n ) ? line[i + 1] : '\0';

                    if ( c == '-' && next == '-' )
                    {
                        hasComment = true;
                        if ( i + 3 < n && line[i + 2] == '[' && line[i + 3] == '[' )
                        {
                            inBlockComment = true;
                            i += 4;
                            int idx = line.IndexOf( "]]", i, StringComparison.Ordinal );
                            if ( idx >= 0 ) { inBlockComment = false; i = idx + 2; }
                            else { i = n; }
                            continue;
                        }
                        break;
                    }

                    if ( c == '"' || c == '\'' )
                    {
                        hasCode = true;
                        char quote = c;
                        i++;
                        ConsumeRegularString( line, ref i, n, quote );
                        continue;
                    }

                    if ( !char.IsWhiteSpace( c ) )
                        hasCode = true;
                    i++;
                }

                if ( hasCode ) result.CodeLines++;
                else if ( hasComment ) result.CommentLines++;
                else result.BlankLines++;
            }
        }

        private static void ParsePython( List<string> lines, CountResult result )
        {
            bool inTripleSingle = false;
            bool inTripleDouble = false;

            foreach ( string line in lines )
            {
                bool hasCode = false;
                bool hasComment = false;
                int i = 0;
                int n = line.Length;

                while ( i < n )
                {
                    if ( inTripleSingle )
                    {
                        hasCode = true;
                        int idx = line.IndexOf( "'''", i, StringComparison.Ordinal );
                        if ( idx < 0 ) { i = n; break; }
                        inTripleSingle = false;
                        i = idx + 3;
                        continue;
                    }
                    if ( inTripleDouble )
                    {
                        hasCode = true;
                        int idx = line.IndexOf( "\"\"\"", i, StringComparison.Ordinal );
                        if ( idx < 0 ) { i = n; break; }
                        inTripleDouble = false;
                        i = idx + 3;
                        continue;
                    }

                    char c = line[i];

                    if ( c == '#' )
                    {
                        hasComment = true;
                        break;
                    }

                    if ( c == '\'' && i + 2 < n && line[i + 1] == '\'' && line[i + 2] == '\'' )
                    {
                        hasCode = true;
                        i += 3;
                        int idx = line.IndexOf( "'''", i, StringComparison.Ordinal );
                        if ( idx >= 0 ) { i = idx + 3; }
                        else { inTripleSingle = true; i = n; }
                        continue;
                    }
                    if ( c == '"' && i + 2 < n && line[i + 1] == '"' && line[i + 2] == '"' )
                    {
                        hasCode = true;
                        i += 3;
                        int idx = line.IndexOf( "\"\"\"", i, StringComparison.Ordinal );
                        if ( idx >= 0 ) { i = idx + 3; }
                        else { inTripleDouble = true; i = n; }
                        continue;
                    }
                    if ( c == '"' || c == '\'' )
                    {
                        hasCode = true;
                        char quote = c;
                        i++;
                        ConsumeRegularString( line, ref i, n, quote );
                        continue;
                    }

                    if ( !char.IsWhiteSpace( c ) )
                        hasCode = true;
                    i++;
                }

                if ( hasCode ) result.CodeLines++;
                else if ( hasComment ) result.CommentLines++;
                else result.BlankLines++;
            }
        }

        private static void ParseJson( List<string> lines, CountResult result )
        {
            foreach ( string line in lines )
            {
                bool hasCode = false;
                for ( int i = 0; i < line.Length; i++ )
                {
                    if ( !char.IsWhiteSpace( line[i] ) ) { hasCode = true; break; }
                }
                if ( hasCode ) result.CodeLines++;
                else result.BlankLines++;
            }
        }

        private static void ParseBash( List<string> lines, CountResult result )
        {
            foreach ( string line in lines )
            {
                bool hasCode = false;
                bool hasComment = false;
                int i = 0;
                int n = line.Length;

                while ( i < n )
                {
                    char c = line[i];

                    if ( c == '#' )
                    {
                        bool atTokenStart = i == 0 || IsBashTokenBoundary( line[i - 1] );
                        if ( atTokenStart )
                        {
                            hasComment = true;
                            break;
                        }
                        hasCode = true;
                        i++;
                        continue;
                    }

                    if ( c == '"' )
                    {
                        hasCode = true;
                        i++;
                        ConsumeBashDoubleString( line, ref i, n );
                        continue;
                    }
                    if ( c == '\'' )
                    {
                        hasCode = true;
                        i++;
                        while ( i < n && line[i] != '\'' ) i++;
                        if ( i < n ) i++;
                        continue;
                    }

                    if ( !char.IsWhiteSpace( c ) )
                        hasCode = true;
                    i++;
                }

                if ( hasCode ) result.CodeLines++;
                else if ( hasComment ) result.CommentLines++;
                else result.BlankLines++;
            }
        }

        private static bool IsBashTokenBoundary( char c )
        {
            if ( char.IsWhiteSpace( c ) ) return true;
            switch ( c )
            {
                case ';': case '|': case '&': case '(': case ')':
                case '`': case '{': case '}':
                    return true;
            }
            return false;
        }

        private static void ConsumeBashDoubleString( string line, ref int i, int n )
        {
            while ( i < n )
            {
                if ( line[i] == '\\' && i + 1 < n ) { i += 2; continue; }
                if ( line[i] == '"' ) { i++; return; }
                i++;
            }
        }

        private static void ParseBatch( List<string> lines, CountResult result )
        {
            foreach ( string line in lines )
            {
                int firstNonWs = 0;
                while ( firstNonWs < line.Length && char.IsWhiteSpace( line[firstNonWs] ) )
                    firstNonWs++;

                if ( firstNonWs >= line.Length )
                {
                    result.BlankLines++;
                    continue;
                }

                bool isComment = false;

                // :: comment
                if ( firstNonWs + 1 < line.Length && line[firstNonWs] == ':' && line[firstNonWs + 1] == ':' )
                {
                    isComment = true;
                }
                // REM (followed by whitespace or end of line; case-insensitive)
                else if ( firstNonWs + 2 < line.Length
                    && ( line[firstNonWs] == 'R' || line[firstNonWs] == 'r' )
                    && ( line[firstNonWs + 1] == 'E' || line[firstNonWs + 1] == 'e' )
                    && ( line[firstNonWs + 2] == 'M' || line[firstNonWs + 2] == 'm' )
                    && ( firstNonWs + 3 == line.Length || char.IsWhiteSpace( line[firstNonWs + 3] ) ) )
                {
                    isComment = true;
                }

                if ( isComment ) result.CommentLines++;
                else result.CodeLines++;
            }
        }
    }
}
