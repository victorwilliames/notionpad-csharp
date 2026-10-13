using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;

namespace NotionPad
{
    public class EmojiData
    {
        public string Glyph { get; set; }
        public string Keywords { get; set; }
        public EmojiData(string glyph, string keywords)
        {
            Glyph = glyph;
            Keywords = keywords;
        }
    }

    public class CoverItem
    {
        public string Name { get; set; }
        public string Type { get; set; } // "solid", "gradient", "image"
        public string Color1 { get; set; }
        public string Color2 { get; set; }
        public Brush DisplayBrush { get; set; }

        public CoverItem(string name, string color1, string color2 = null)
        {
            Name = name;
            Color1 = color1;
            Color2 = color2;
            if (string.IsNullOrEmpty(color2))
            {
                Type = "solid";
                DisplayBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color1));
            }
            else
            {
                Type = "gradient";
                var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(color1), 0.0));
                grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(color2), 1.0));
                DisplayBrush = grad;
            }
        }
    }

    public class DocMetadata
    {
        public string Title { get; set; }
        public string Icon { get; set; }
        public string CoverType { get; set; }
        public string CoverValue1 { get; set; }
        public string CoverValue2 { get; set; }
        public double CoverOffsetY { get; set; }
    }

    public static class MetadataStore
    {
        private static string StorageFile
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "NotionPad");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return Path.Combine(dir, "page_metadata.tsv");
            }
        }

        private static Dictionary<string, DocMetadata> _cache = null;

        private static void LoadCache()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, DocMetadata>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(StorageFile))
                {
                    string[] lines = File.ReadAllLines(StorageFile, Encoding.UTF8);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        string[] parts = line.Split('\t');
                        if (parts.Length >= 7)
                        {
                            string path = parts[0];
                            var meta = new DocMetadata
                            {
                                Title = Uri.UnescapeDataString(parts[1]),
                                Icon = Uri.UnescapeDataString(parts[2]),
                                CoverType = parts[3],
                                CoverValue1 = Uri.UnescapeDataString(parts[4]),
                                CoverValue2 = Uri.UnescapeDataString(parts[5]),
                            };
                            double off;
                            double.TryParse(parts[6], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out off);
                            meta.CoverOffsetY = off;
                            _cache[path] = meta;
                        }
                    }
                }
            }
            catch { }
        }

        public static void SaveMetadata(string filePath, DocMetadata meta)
        {
            if (string.IsNullOrEmpty(filePath) || meta == null) return;
            LoadCache();
            _cache[filePath] = meta;
            try
            {
                var sb = new StringBuilder();
                foreach (var kvp in _cache)
                {
                    sb.Append(kvp.Key).Append('\t')
                      .Append(Uri.EscapeDataString(kvp.Value.Title ?? "")).Append('\t')
                      .Append(Uri.EscapeDataString(kvp.Value.Icon ?? "")).Append('\t')
                      .Append(kvp.Value.CoverType ?? "none").Append('\t')
                      .Append(Uri.EscapeDataString(kvp.Value.CoverValue1 ?? "")).Append('\t')
                      .Append(Uri.EscapeDataString(kvp.Value.CoverValue2 ?? "")).Append('\t')
                      .Append(kvp.Value.CoverOffsetY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                      .AppendLine();
                }
                File.WriteAllText(StorageFile, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static DocMetadata GetMetadata(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;
            LoadCache();
            DocMetadata meta;
            if (_cache.TryGetValue(filePath, out meta)) return meta;
            return null;
        }
    }

    public class TabDocument
    {
        public string FilePath { get; set; }
        public string Title { get; set; }
        public bool IsModified { get; set; }
        public string EmojiIcon { get; set; }
        public string CoverType { get; set; } // "none", "solid", "gradient", "image", "url"
        public string CoverValue1 { get; set; }
        public string CoverValue2 { get; set; }
        public double CoverOffsetY { get; set; }
        public string Language { get; set; } // "HTML", "JavaScript", "CSS", "JSON", "XML", "Python", "C#", "SQL", "Markdown", "Texto"

        public ScrollViewer PageScroller { get; set; }
        public Border CoverBanner { get; set; }
        public Image CoverImageElement { get; set; }
        public TranslateTransform CoverTransform { get; set; }
        public StackPanel CoverHoverBar { get; set; }
        public Border RepositionHintBanner { get; set; }
        public StackPanel RepositionActionsBar { get; set; }

        public Grid HeaderHoverArea { get; set; }
        public StackPanel ActionsRow { get; set; }
        public Button BtnAddIcon { get; set; }
        public Button BtnAddCover { get; set; }
        public Button EmojiButton { get; set; }

        public TextBlock TitlePlaceholder { get; set; }
        public TextBox TitleEditor { get; set; }
        public TextBlock EditorPlaceholder { get; set; }
        public RichTextBox Editor { get; set; }

        public Border TabBorder { get; set; }
        public TextBlock TitleText { get; set; }
        public Button CloseBtn { get; set; }

        public TabDocument(string title)
        {
            Title = title;
            FilePath = null;
            IsModified = false;
            EmojiIcon = ""; // Start completely clean
            CoverType = "none";
            CoverValue1 = null;
            CoverValue2 = null;
            CoverOffsetY = 0.0;
            Language = "Texto";
        }

        public void UpdateTitle(string newTitle, bool modified)
        {
            Title = newTitle;
            IsModified = modified;
            if (TitleText != null)
            {
                string iconPrefix = string.IsNullOrEmpty(EmojiIcon) ? "" : EmojiIcon + " ";
                string displayTitle = string.IsNullOrEmpty(Title) ? "Sem título" : Title;
                TitleText.Text = (IsModified ? "● " : "") + iconPrefix + displayTitle;
            }
        }

        public void UpdateHeaderState()
        {
            bool hasCover = CoverType != "none";
            bool hasIcon = !string.IsNullOrEmpty(EmojiIcon);

            if (CoverBanner != null)
            {
                CoverBanner.Visibility = hasCover ? Visibility.Visible : Visibility.Collapsed;
            }
            if (EmojiButton != null)
            {
                EmojiButton.Visibility = hasIcon ? Visibility.Visible : Visibility.Collapsed;
                EmojiButton.Content = EmojiIcon;
                // Notion signature overlap: negative margin when cover is present!
                EmojiButton.Margin = hasCover ? new Thickness(0, -42, 0, 6) : new Thickness(0, 8, 0, 6);
            }
            if (BtnAddCover != null)
            {
                BtnAddCover.Visibility = hasCover ? Visibility.Collapsed : Visibility.Visible;
            }
            if (BtnAddIcon != null)
            {
                BtnAddIcon.Visibility = hasIcon ? Visibility.Collapsed : Visibility.Visible;
            }
            if (ActionsRow != null)
            {
                ActionsRow.Visibility = (hasCover && hasIcon) ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }

    public class TextMatch
    {
        public TextPointer Start;
        public TextPointer End;
    }

    public static class SyntaxHighlighter
    {
        public static readonly SolidColorBrush BrushKeyword;
        public static readonly SolidColorBrush BrushTag;
        public static readonly SolidColorBrush BrushAttribute;
        public static readonly SolidColorBrush BrushString;
        public static readonly SolidColorBrush BrushNumber;
        public static readonly SolidColorBrush BrushComment;
        public static readonly SolidColorBrush BrushType;
        public static readonly SolidColorBrush BrushEntity;
        public static readonly SolidColorBrush BrushMarkdownHeader;
        public static readonly SolidColorBrush BrushDefault;

        static SyntaxHighlighter()
        {
            BrushKeyword = new SolidColorBrush(Color.FromRgb(86, 156, 214)); BrushKeyword.Freeze();       // #569CD6
            BrushTag = new SolidColorBrush(Color.FromRgb(86, 156, 214)); BrushTag.Freeze();               // #569CD6
            BrushAttribute = new SolidColorBrush(Color.FromRgb(156, 220, 254)); BrushAttribute.Freeze(); // #9CDCFE
            BrushString = new SolidColorBrush(Color.FromRgb(206, 145, 120)); BrushString.Freeze();       // #CE9178
            BrushNumber = new SolidColorBrush(Color.FromRgb(181, 206, 168)); BrushNumber.Freeze();       // #B5CEA8
            BrushComment = new SolidColorBrush(Color.FromRgb(106, 153, 85)); BrushComment.Freeze();       // #6A9955
            BrushType = new SolidColorBrush(Color.FromRgb(78, 201, 176)); BrushType.Freeze();             // #4EC9B0
            BrushEntity = new SolidColorBrush(Color.FromRgb(215, 186, 125)); BrushEntity.Freeze();       // #D7BA7D
            BrushMarkdownHeader = new SolidColorBrush(Color.FromRgb(78, 201, 176)); BrushMarkdownHeader.Freeze(); // #4EC9B0
            BrushDefault = new SolidColorBrush(Color.FromRgb(212, 212, 212)); BrushDefault.Freeze();       // #D4D4D4
        }

        public static string DetectLanguage(string filePath, string text)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                switch (ext)
                {
                    case ".html":
                    case ".htm":
                    case ".xhtml":
                    case ".vue":
                        return "HTML";
                    case ".css":
                    case ".scss":
                    case ".less":
                        return "CSS";
                    case ".js":
                    case ".jsx":
                    case ".mjs":
                    case ".cjs":
                    case ".ts":
                    case ".tsx":
                        return "JavaScript";
                    case ".json":
                        return "JSON";
                    case ".xml":
                    case ".xaml":
                    case ".svg":
                    case ".config":
                    case ".csproj":
                        return "XML";
                    case ".py":
                    case ".pyw":
                        return "Python";
                    case ".cs":
                        return "C#";
                    case ".sql":
                        return "SQL";
                    case ".md":
                    case ".markdown":
                        return "Markdown";
                    case ".txt":
                    case ".log":
                    case ".ini":
                    case ".env":
                        return "Texto";
                }
            }

            if (!string.IsNullOrEmpty(text))
            {
                string trimmed = text.TrimStart();
                if (trimmed.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
                    return "HTML";
                if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
                    return "XML";
                if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                    return "JSON";
                if (trimmed.StartsWith("import ") || trimmed.StartsWith("def ") || (trimmed.StartsWith("class ") && trimmed.Contains(":")))
                    return "Python";
                if (trimmed.StartsWith("using System") || trimmed.Contains("namespace "))
                    return "C#";
                if (trimmed.StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("-- "))
                    return "SQL";
            }

            return "Texto";
        }

        public static void ApplyHighlightingToDocument(FlowDocument doc, string language, string fullText)
        {
            if (doc == null) return;
            doc.Blocks.Clear();

            if (string.IsNullOrEmpty(fullText))
            {
                doc.Blocks.Add(new Paragraph { Margin = new Thickness(0) });
                return;
            }

            string[] lines = fullText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                var p = new Paragraph { Margin = new Thickness(0) };
                PopulateParagraphWithTokens(p, lines[i], language);
                doc.Blocks.Add(p);
            }
        }

        public static void ApplyHighlightingToParagraph(Paragraph p, string language)
        {
            if (p == null) return;
            var tr = new TextRange(p.ContentStart, p.ContentEnd);
            string text = tr.Text.TrimEnd('\r', '\n');
            p.Inlines.Clear();
            PopulateParagraphWithTokens(p, text, language);
        }

        public static void PopulateParagraphWithTokens(Paragraph p, string lineText, string language)
        {
            if (string.IsNullOrEmpty(lineText))
            {
                p.Inlines.Add(new Run(""));
                return;
            }

            if (language == "Texto")
            {
                p.Inlines.Add(new Run(lineText) { Foreground = BrushDefault });
                return;
            }

            List<SyntaxToken> tokens = TokenizeLine(lineText, language);
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                var run = new Run(t.Text) { Foreground = GetBrushForToken(t.Type) };
                if (t.Type == SyntaxTokenType.MarkdownHeader)
                {
                    run.FontWeight = FontWeights.Bold;
                }
                p.Inlines.Add(run);
            }
        }

        private static Brush GetBrushForToken(SyntaxTokenType type)
        {
            switch (type)
            {
                case SyntaxTokenType.Keyword: return BrushKeyword;
                case SyntaxTokenType.Tag: return BrushTag;
                case SyntaxTokenType.Attribute: return BrushAttribute;
                case SyntaxTokenType.String: return BrushString;
                case SyntaxTokenType.Number: return BrushNumber;
                case SyntaxTokenType.Comment: return BrushComment;
                case SyntaxTokenType.Type: return BrushType;
                case SyntaxTokenType.Entity: return BrushEntity;
                case SyntaxTokenType.MarkdownHeader: return BrushMarkdownHeader;
                default: return BrushDefault;
            }
        }

        public enum SyntaxTokenType
        {
            PlainText,
            Keyword,
            Tag,
            Attribute,
            String,
            Number,
            Comment,
            Type,
            Entity,
            MarkdownHeader
        }

        public struct SyntaxToken
        {
            public string Text;
            public SyntaxTokenType Type;
            public SyntaxToken(string text, SyntaxTokenType type)
            {
                Text = text;
                Type = type;
            }
        }

        private static List<SyntaxToken> TokenizeLine(string line, string language)
        {
            var list = new List<SyntaxToken>();
            if (string.IsNullOrEmpty(line)) return list;

            switch (language)
            {
                case "HTML":
                case "XML":
                    TokenizeHtmlXml(line, list);
                    break;
                case "JavaScript":
                case "JSON":
                    TokenizeJs(line, list);
                    break;
                case "CSS":
                    TokenizeCss(line, list);
                    break;
                case "Python":
                    TokenizePython(line, list);
                    break;
                case "C#":
                    TokenizeCSharp(line, list);
                    break;
                case "SQL":
                    TokenizeSql(line, list);
                    break;
                case "Markdown":
                    TokenizeMarkdown(line, list);
                    break;
                default:
                    list.Add(new SyntaxToken(line, SyntaxTokenType.PlainText));
                    break;
            }
            return list;
        }

        private static void TokenizeHtmlXml(string line, List<SyntaxToken> list)
        {
            var rx = new Regex(@"(<!--[\s\S]*?-->)|(<!DOCTYPE[^>]*>)|(<[^>]+>)|([^<]+)", RegexOptions.Compiled);
            var matches = rx.Matches(line);
            foreach (Match m in matches)
            {
                string v = m.Value;
                if (v.StartsWith("<!--"))
                {
                    list.Add(new SyntaxToken(v, SyntaxTokenType.Comment));
                }
                else if (v.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(new SyntaxToken(v, SyntaxTokenType.Entity));
                }
                else if (v.StartsWith("<"))
                {
                    var tagRx = new Regex(@"(</?[a-zA-Z0-9_\-]+)|([a-zA-Z0-9_\-]+(?=\=))|(=)|(""[^""]*""|'[^']*')|(/?>)|(\s+)", RegexOptions.Compiled);
                    var tagMatches = tagRx.Matches(v);
                    int last = 0;
                    foreach (Match tm in tagMatches)
                    {
                        if (tm.Index > last)
                        {
                            list.Add(new SyntaxToken(v.Substring(last, tm.Index - last), SyntaxTokenType.PlainText));
                        }
                        if (tm.Value.StartsWith("<") || tm.Value == ">" || tm.Value == "/>")
                        {
                            list.Add(new SyntaxToken(tm.Value, SyntaxTokenType.Tag));
                        }
                        else if (tm.Groups[2].Success)
                        {
                            list.Add(new SyntaxToken(tm.Value, SyntaxTokenType.Attribute));
                        }
                        else if (tm.Value.StartsWith("\"") || tm.Value.StartsWith("'"))
                        {
                            list.Add(new SyntaxToken(tm.Value, SyntaxTokenType.String));
                        }
                        else
                        {
                            list.Add(new SyntaxToken(tm.Value, SyntaxTokenType.PlainText));
                        }
                        last = tm.Index + tm.Length;
                    }
                    if (last < v.Length)
                    {
                        list.Add(new SyntaxToken(v.Substring(last), SyntaxTokenType.PlainText));
                    }
                }
                else
                {
                    list.Add(new SyntaxToken(v, SyntaxTokenType.PlainText));
                }
            }
        }

        private static void TokenizeJs(string line, List<SyntaxToken> list)
        {
            var rx = new Regex(@"(//.*$|/\*[\s\S]*?\*/)|(""[^""\\]*(?:\\.[^""\\]*)*""|'[^'\\]*(?:\\.[^'\\]*)*'|`[^`\\]*(?:\\.[^`\\]*)*`)|(\b(break|case|catch|class|const|continue|debugger|default|delete|do|else|export|extends|finally|for|function|if|import|in|instanceof|new|return|super|switch|this|throw|try|typeof|var|void|while|with|yield|let|static|async|await)\b)|(\b(true|false|null|undefined)\b)|(\b\d+(?:\.\d+)?\b)|(\b[a-zA-Z_$][a-zA-Z0-9_$]*(?=\s*:))", RegexOptions.Compiled);
            TokenizeWithRegex(line, rx, list, 1, 2, 3, 4, 5, 6);
        }

        private static void TokenizeCss(string line, List<SyntaxToken> list)
        {
            var rx = new Regex(@"(/\*[\s\S]*?\*/)|(""[^""]*""|'[^']*')|([.#][a-zA-Z0-9_\-]+(?=[\s\{,]))|([a-zA-Z\-]+(?=\s*:))|(#[0-9a-fA-F]{3,8}|\b\d+(?:px|em|rem|%|vh|vw|s|ms|pt)?\b)", RegexOptions.Compiled);
            int last = 0;
            foreach (Match m in rx.Matches(line))
            {
                if (m.Index > last) list.Add(new SyntaxToken(line.Substring(last, m.Index - last), SyntaxTokenType.PlainText));
                if (m.Groups[1].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Comment));
                else if (m.Groups[2].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.String));
                else if (m.Groups[3].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Entity));
                else if (m.Groups[4].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Attribute));
                else if (m.Groups[5].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Number));
                else list.Add(new SyntaxToken(m.Value, SyntaxTokenType.PlainText));
                last = m.Index + m.Length;
            }
            if (last < line.Length) list.Add(new SyntaxToken(line.Substring(last), SyntaxTokenType.PlainText));
        }

        private static void TokenizePython(string line, List<SyntaxToken> list)
        {
            var rx = new Regex(@"(#.*$)|(""""[\s\S]*?""""|'''[\s\S]*?'''|""[^""\\]*(?:\\.[^""\\]*)*""|'[^'\\]*(?:\\.[^'\\]*)*')|(\b(and|as|assert|async|await|break|class|continue|def|del|elif|else|except|finally|for|from|global|if|import|in|is|lambda|nonlocal|not|or|pass|raise|return|try|while|with|yield|True|False|None|self)\b)|(\b\d+(?:\.\d+)?\b)|((?<=\bdef\s+)[a-zA-Z_][a-zA-Z0-9_]*|(?<=\bclass\s+)[a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Compiled);
            int last = 0;
            foreach (Match m in rx.Matches(line))
            {
                if (m.Index > last) list.Add(new SyntaxToken(line.Substring(last, m.Index - last), SyntaxTokenType.PlainText));
                if (m.Groups[1].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Comment));
                else if (m.Groups[2].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.String));
                else if (m.Groups[3].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Keyword));
                else if (m.Groups[4].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Number));
                else if (m.Groups[5].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Type));
                else list.Add(new SyntaxToken(m.Value, SyntaxTokenType.PlainText));
                last = m.Index + m.Length;
            }
            if (last < line.Length) list.Add(new SyntaxToken(line.Substring(last), SyntaxTokenType.PlainText));
        }

        private static void TokenizeCSharp(string line, List<SyntaxToken> list)
        {
            var rx = new Regex(@"(//.*$|/\*[\s\S]*?\*/)|(@""[^""]*(""""[^""]*)*""|""[^""\\]*(?:\\.[^""\\]*)*""|'[^'\\]*(?:\\.[^'\\]*)*')|(\b(abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|var|virtual|void|volatile|while)\b)|(\b\d+(?:\.\d+)?f?m?d?L?\b)|(\b[A-Z][a-zA-Z0-9_]*\b)", RegexOptions.Compiled);
            int last = 0;
            foreach (Match m in rx.Matches(line))
            {
                if (m.Index > last) list.Add(new SyntaxToken(line.Substring(last, m.Index - last), SyntaxTokenType.PlainText));
                if (m.Groups[1].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Comment));
                else if (m.Groups[2].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.String));
                else if (m.Groups[3].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Keyword));
                else if (m.Groups[4].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Number));
                else if (m.Groups[5].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Type));
                else list.Add(new SyntaxToken(m.Value, SyntaxTokenType.PlainText));
                last = m.Index + m.Length;
            }
            if (last < line.Length) list.Add(new SyntaxToken(line.Substring(last), SyntaxTokenType.PlainText));
        }

        private static void TokenizeSql(string line, List<SyntaxToken> list)
        {
            var rx = new Regex(@"(--.*$|/\*[\s\S]*?\*/)|('[^']*')|(\b(SELECT|FROM|WHERE|INSERT|INTO|UPDATE|SET|DELETE|JOIN|INNER|LEFT|RIGHT|FULL|OUTER|ON|GROUP\s+BY|ORDER\s+BY|HAVING|LIMIT|OFFSET|CREATE|TABLE|ALTER|DROP|INDEX|VIEW|TRIGGER|PRIMARY\s+KEY|FOREIGN\s+KEY|REFERENCES|DEFAULT|NULL|NOT\s+NULL|AND|OR|NOT|IN|LIKE|BETWEEN|IS|EXISTS|UNION|ALL|AS|DISTINCT|CASE|WHEN|THEN|ELSE|END|COUNT|SUM|AVG|MIN|MAX|VALUES|VARCHAR|INT|INTEGER|DATETIME|BOOLEAN|TEXT)\b)|(\b\d+(?:\.\d+)?\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            int last = 0;
            foreach (Match m in rx.Matches(line))
            {
                if (m.Index > last) list.Add(new SyntaxToken(line.Substring(last, m.Index - last), SyntaxTokenType.PlainText));
                if (m.Groups[1].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Comment));
                else if (m.Groups[2].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.String));
                else if (m.Groups[3].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Keyword));
                else if (m.Groups[4].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Number));
                else list.Add(new SyntaxToken(m.Value, SyntaxTokenType.PlainText));
                last = m.Index + m.Length;
            }
            if (last < line.Length) list.Add(new SyntaxToken(line.Substring(last), SyntaxTokenType.PlainText));
        }

        private static void TokenizeMarkdown(string line, List<SyntaxToken> list)
        {
            if (line.StartsWith("#"))
            {
                list.Add(new SyntaxToken(line, SyntaxTokenType.MarkdownHeader));
                return;
            }
            var rx = new Regex(@"(```[\s\S]*?```|`[^`]+`)|(\[[^\]]+\]\([^\)]+\))|(\*\*.*?\*\*|\*.*?\*)|(^\s*[-*+]\s|^\s*\d+\.\s)", RegexOptions.Compiled);
            int last = 0;
            foreach (Match m in rx.Matches(line))
            {
                if (m.Index > last) list.Add(new SyntaxToken(line.Substring(last, m.Index - last), SyntaxTokenType.PlainText));
                if (m.Groups[1].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.String));
                else if (m.Groups[2].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Keyword));
                else if (m.Groups[3].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Type));
                else if (m.Groups[4].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Entity));
                else list.Add(new SyntaxToken(m.Value, SyntaxTokenType.PlainText));
                last = m.Index + m.Length;
            }
            if (last < line.Length) list.Add(new SyntaxToken(line.Substring(last), SyntaxTokenType.PlainText));
        }

        private static void TokenizeWithRegex(string line, Regex rx, List<SyntaxToken> list, int commentGrp, int strGrp, int kwGrp, int boolGrp, int numGrp, int attrGrp)
        {
            int last = 0;
            foreach (Match m in rx.Matches(line))
            {
                if (m.Index > last) list.Add(new SyntaxToken(line.Substring(last, m.Index - last), SyntaxTokenType.PlainText));
                if (commentGrp > 0 && m.Groups[commentGrp].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Comment));
                else if (strGrp > 0 && m.Groups[strGrp].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.String));
                else if (kwGrp > 0 && m.Groups[kwGrp].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Keyword));
                else if (boolGrp > 0 && m.Groups[boolGrp].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Number));
                else if (numGrp > 0 && m.Groups[numGrp].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Number));
                else if (attrGrp > 0 && m.Groups[attrGrp].Success) list.Add(new SyntaxToken(m.Value, SyntaxTokenType.Attribute));
                else list.Add(new SyntaxToken(m.Value, SyntaxTokenType.PlainText));
                last = m.Index + m.Length;
            }
            if (last < line.Length) list.Add(new SyntaxToken(line.Substring(last), SyntaxTokenType.PlainText));
        }

        public static List<TextMatch> FindAllMatches(RichTextBox rtb, string query, bool matchCase)
        {
            var results = new List<TextMatch>();
            if (rtb == null || string.IsNullOrEmpty(query)) return results;

            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            foreach (var block in rtb.Document.Blocks)
            {
                var p = block as Paragraph;
                if (p == null) continue;

                var pRange = new TextRange(p.ContentStart, p.ContentEnd);
                string pText = pRange.Text;
                if (string.IsNullOrEmpty(pText)) continue;

                int idx = 0;
                while (idx < pText.Length)
                {
                    int found = pText.IndexOf(query, idx, comparison);
                    if (found == -1) break;

                    TextPointer start = GetPointerAtOffset(p.ContentStart, found);
                    TextPointer end = GetPointerAtOffset(start, query.Length);
                    if (start != null && end != null)
                    {
                        var m = new TextMatch();
                        m.Start = start;
                        m.End = end;
                        results.Add(m);
                    }
                    idx = found + Math.Max(1, query.Length);
                }
            }
            return results;
        }

        public static TextPointer GetPointerAtOffset(TextPointer start, int offset)
        {
            if (start == null) return null;
            TextPointer pos = start;
            int remaining = offset;

            while (pos != null && remaining > 0)
            {
                if (pos.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string text = pos.GetTextInRun(LogicalDirection.Forward);
                    if (remaining <= text.Length)
                    {
                        return pos.GetPositionAtOffset(remaining);
                    }
                    remaining -= text.Length;
                }
                pos = pos.GetNextContextPosition(LogicalDirection.Forward);
            }
            return pos;
        }
    }

    public class MainWindow : Window
    {
        private const string NOTEPAD_ICON_16_BASE64 =
            "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAE/UlEQVR4nO1bT0gzRxT/zWQTY7GHRhNjhRbhE8ylCN6sB7WCUMEeDKgUcxC8SUHEr3gKHr156SW3evHYVjxZPq2RoHizvYSvh1osCmKCWPyfnSlvvxmxHxSaZHY3Yn6w7mbe7puZN++9eTPzBOqoo4466qjjxYK5xIuehcf0OvwCVyPSAeANgF0ACVXGPaD7Cg4gDMAC8B0Aqa6fVFmDy/RQtWZsVdl5wRi7ZYxBShmUUtqK1sg5LwGgy4EbdCkllUMJwXnwSgCMOt/c3PxxoVD4Ukr5AYDPAAQU/VMhxDfqN1MdMUn/RNH/SqfTPy4tLYlKhcAqHXkAnwP4oampKRqJRHB+fo7r62vnhXA4jGg0qkcHpCHFYhFXV1dG6A0NDYjFYjg+Pqafv0Qika+KxeLfqn0VaUI54Nvb26Q5vw4ODsrT09O7+/v7h6mpKaFtdGBgQFDZzc3Nw9XV1QM9p1IpY/T+/n5xd3f3sLu7e9fS0iIZY8skpEo0mlfwvkilUjEAr2ZnZ2U8Hg8Gg0FLSmoHQyAQQKlUYlQWDoedi56FEMbotm2zUChk9fX1BZLJpJBSDlA5ANttATiwbTugRoMJIRxVHRkZce62bWNoaMh5LpVKcJNOaGxs5H7NAoz+cM6dhk5MTCAej+Py8hKjo6PvmFvv2FNjTdOpTqKTgPwSwCPUNIj+/n54TVe2XzF4VV+/1xBST7r8oPuqARrKEflG910ABB2daSdlCqQB5G9qXgCMsart0ktYphjRiFPHyTsfHh463lk7r2qgeQSDQXR3d6MmBSCVylNjU6kU1tbW4AZev36N5eVlo46Q44XDMm33q6urmJ+fd9UEVLhcWz6AKQFQdNbT04PnAuulT4PcOEclBDd4usHXMs1QL1lfbBxwcXGBbDZb9Srtfd6hUAiDg4O0/DWqCZbpOGB8fBybm5twAzMzM8hkMkbjAAuGkUgksLe35zgsUw3VJtXZ2encTUyvxuMArjz0ysoKFhYWjMcBNL22tbXVbhygQY1tb2+HG9BmVvOzgDDoAN1eZVqmGVLn3RCAVv2a1wDOuSsR27OJA46OjrCxsWFcCygOGBsb+9eJUU3GAZOTk9jf34cb2NnZcfYaai4OYE/skvbsz87OHp2hiWmQTIruw8PDtRkHELTdLy4uYm5u7jEOMAEdB5AZ1HwcIIRwTnfdAPE27WAto9yUJrixbNW8TcMyztHAcZWX4NV87NZIe9kGq5qPKVODpiRySF4LQ58O02ZpNYlSVpnvO72MxWK3JycndwcHB410bu/HDpA+Os/n83S7rjRZipVZr67gI8bY71LK5unpadnV1cVIE7wKgfVskMvl7PX19QBj7I2UcuhJ/pLrAmjmnL9NJpORXC4nC4WCk77iFXSQ1dHRYUej0UA2m91ijH0hpSxbAOXC6WVvb++HAApbW1tSSimEoDQdX1BKp9M0ID8rkyjbFlnZIgACnHNbCPF9IpFIZTIZu7W1NeDGZsV/QdeVz+ft6enpQLFYnJNSrkgpSQpmQsT/4Ti/Vur2wBjTKaxeXjptjlZGr1SbuJfTYIhzzih9Vdme15Aq6iTtq7gfVgXf6LTUrBDilhI7/Yr81J7DWwB/epkqS9DelqaeNIAW+IM/AHwL4LdKpkBCNUP3VOJUuR9xsa6z4mzxaqGzuf2C/oeKisEMNcQvIfi/GqujjjrwnPEP+ejBT5zaQBQAAAAASUVORK5CYII=";

        private List<TabDocument> _documents = new List<TabDocument>();
        private TabDocument _currentDoc;
        private double _currentZoom = 1.0;
        private string _selectedFont = "Segoe UI";
        private double _baseFontSize = 15.0;
        private bool _isWordWrap = true;
        private bool _showStatusBar = true;

        // UI Panels
        private Grid _rootGrid;
        private Grid _menuGrid;
        private Button _btnMenuToggle;
        private StackPanel _tabsPanel;
        private Grid _editorContainer;
        private Border _findBar;
        private TextBox _findBox;
        private TextBox _replaceBox;
        private CheckBox _matchCaseBox;
        private TextBlock _findCountText;
        private StackPanel _replaceRow;
        private Button _btnMaxRestore;
        private Grid _statusBarGrid;

        // Notion Floating Popups
        private Popup _floatingToolbar;
        private Popup _emojiPickerPopup;
        private TextBox _emojiSearchBox;
        private WrapPanel _emojiGridWrap;
        private Popup _coverPickerPopup;
        private Popup _blockTypePopup;

        // Cover Picker Panels for Tabs
        private ScrollViewer _coverGalleryScroller;
        private UniformGrid _coverGalleryGrid;
        private Grid _coverUploadPanel;
        private Grid _coverLinkPanel;
        private Button _tabBtnGallery;
        private Button _tabBtnUpload;
        private Button _tabBtnLink;

        // Reposition state tracking
        private bool _isRepositioning = false;
        private Point _repositionStartMouse;
        private double _repositionInitialOffset;

        // Status Bar Items
        private TextBlock _statusLineCol;
        private TextBlock _statusChars;
        private Button _statusLanguageBtn;
        private TextBlock _statusZoom;
        private TextBlock _statusFormat;
        private TextBlock _statusEncoding;

        // Syntax Highlighting & Search State
        private DispatcherTimer _highlightDebounceTimer;
        private bool _isApplyingHighlighting = false;
        private bool _rehighlightFullDocPending = false;
        private List<TextMatch> _currentMatches = new List<TextMatch>();
        private int _currentMatchIndex = -1;

        // Colors
        private SolidColorBrush _brushTopBarBg = new SolidColorBrush(Color.FromRgb(31, 31, 31));       // #1F1F1F
        private SolidColorBrush _brushTabActive = new SolidColorBrush(Color.FromRgb(38, 38, 38));      // #262626
        private SolidColorBrush _brushTabInactive = new SolidColorBrush(Color.FromRgb(31, 31, 31));    // #1F1F1F
        private SolidColorBrush _brushMenuBarBg = new SolidColorBrush(Color.FromRgb(38, 38, 38));      // #262626
        private SolidColorBrush _brushCanvasBg = new SolidColorBrush(Color.FromRgb(25, 25, 25));       // #191919 Notion Dark Canvas
        private SolidColorBrush _brushStatusBarBg = new SolidColorBrush(Color.FromRgb(34, 34, 34));    // #222222
        private SolidColorBrush _brushBorder = new SolidColorBrush(Color.FromRgb(51, 51, 51));         // #333333
        private SolidColorBrush _brushText = new SolidColorBrush(Color.FromRgb(240, 240, 240));        // #F0F0F0
        private SolidColorBrush _brushSubtext = new SolidColorBrush(Color.FromRgb(150, 150, 150));     // #969696
        private SolidColorBrush _brushPlaceholder = new SolidColorBrush(Color.FromRgb(75, 75, 75));   // #4B4B4B
        private SolidColorBrush _brushFloatingBg = new SolidColorBrush(Color.FromRgb(36, 36, 36));     // #242424
        private SolidColorBrush _brushCloseRed = new SolidColorBrush(Color.FromRgb(232, 17, 35));
        private SolidColorBrush _brushBlueAccent = new SolidColorBrush(Color.FromRgb(35, 131, 226));  // Notion Blue Accent

        private FontFamily _fontMdl2 = new FontFamily("Segoe MDL2 Assets");
        private FontFamily _fontUi = new FontFamily("Segoe UI Variable Text, Segoe UI, sans-serif");
        private BitmapImage _officialIconImage;

        private List<EmojiData> _allEmojis = new List<EmojiData>();
        private List<CoverItem> _coverItems = new List<CoverItem>();

        public MainWindow(string[] initialFiles)
        {
            Title = "NotionPad";
            Width = 1060;
            Height = 750;
            MinWidth = 550;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = _brushTopBarBg;
            Foreground = _brushText;
            FontFamily = _fontUi;
            AllowDrop = true;

            InitDatabase();
            LoadOfficialIcon();

            var chrome = new WindowChrome
            {
                CaptionHeight = 40,
                CornerRadius = new CornerRadius(8),
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(6),
                UseAeroCaptionButtons = false
            };
            WindowChrome.SetWindowChrome(this, chrome);

            ApplyDarkThemeStyles();

            BuildInterface();
            BuildFloatingToolbar();
            BuildEmojiPicker();
            BuildCoverPicker();

            if (initialFiles != null && initialFiles.Length > 0)
            {
                foreach (string f in initialFiles)
                {
                    if (File.Exists(f)) OpenFileInTab(f);
                }
            }

            if (_documents.Count == 0)
            {
                AddNewTab(""); // Clean, pristine page
            }

            KeyDown += MainWindow_KeyDown;
            Closing += MainWindow_Closing;
            StateChanged += (s, e) =>
            {
                if (_btnMaxRestore != null)
                {
                    _btnMaxRestore.Content = (WindowState == WindowState.Maximized) ? "\uE923" : "\uE922";
                }
            };

            Drop += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null)
                    {
                        foreach (string file in files)
                        {
                            if (File.Exists(file)) OpenFileInTab(file);
                        }
                    }
                }
            };

            _highlightDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _highlightDebounceTimer.Tick += HighlightDebounceTimer_Tick;
        }

        private void HighlightDebounceTimer_Tick(object sender, EventArgs e)
        {
            _highlightDebounceTimer.Stop();
            if (_currentDoc == null || _currentDoc.Editor == null) return;
            if (_currentDoc.Language == "Texto") return;

            try
            {
                var rtb = _currentDoc.Editor;
                if (_rehighlightFullDocPending)
                {
                    _rehighlightFullDocPending = false;
                    var tr = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                    string txt = tr.Text;
                    _isApplyingHighlighting = true;
                    SyntaxHighlighter.ApplyHighlightingToDocument(rtb.Document, _currentDoc.Language, txt);
                    _isApplyingHighlighting = false;
                }
                else
                {
                    var p = rtb.CaretPosition.Paragraph;
                    if (p != null)
                    {
                        int caretOffset = new TextRange(p.ContentStart, rtb.CaretPosition).Text.Length;
                        _isApplyingHighlighting = true;
                        SyntaxHighlighter.ApplyHighlightingToParagraph(p, _currentDoc.Language);
                        _isApplyingHighlighting = false;

                        TextPointer newCaret = SyntaxHighlighter.GetPointerAtOffset(p.ContentStart, caretOffset);
                        if (newCaret != null)
                        {
                            rtb.CaretPosition = newCaret;
                        }
                    }
                }
            }
            catch
            {
                _isApplyingHighlighting = false;
                _rehighlightFullDocPending = false;
            }
        }

        private void InitDatabase()
        {
            // Curated Emojis
            _allEmojis.Add(new EmojiData("😋", "feliz sorriso comida gostoso linguinha"));
            _allEmojis.Add(new EmojiData("😀", "sorriso alegre feliz contente"));
            _allEmojis.Add(new EmojiData("😃", "sorriso aberto feliz"));
            _allEmojis.Add(new EmojiData("😄", "sorriso olhos fechados"));
            _allEmojis.Add(new EmojiData("😁", "sorriso dentes"));
            _allEmojis.Add(new EmojiData("😆", "gargalhada rindo"));
            _allEmojis.Add(new EmojiData("😅", "suor sorriso alivio"));
            _allEmojis.Add(new EmojiData("😂", "rir chorando de rir rolar"));
            _allEmojis.Add(new EmojiData("🤣", "rolando no chao rindo gargalhada"));
            _allEmojis.Add(new EmojiData("😊", "fofo bochechas vermelhas amor"));
            _allEmojis.Add(new EmojiData("😇", "anjo inocente"));
            _allEmojis.Add(new EmojiData("🙂", "sorriso leve calmo"));
            _allEmojis.Add(new EmojiData("😉", "piscadela flerte"));
            _allEmojis.Add(new EmojiData("😍", "apaixonado amor olhos coracao"));
            _allEmojis.Add(new EmojiData("🥰", "amado carinho coracoes"));
            _allEmojis.Add(new EmojiData("😎", "oculos de sol legal estilo"));
            _allEmojis.Add(new EmojiData("🥳", "festa comemoracao confete"));
            _allEmojis.Add(new EmojiData("🤔", "pensando duvida reflexao"));
            _allEmojis.Add(new EmojiData("🤫", "segredo silencio"));
            _allEmojis.Add(new EmojiData("😴", "dormindo sono sono profundo"));
            _allEmojis.Add(new EmojiData("🚀", "foguete lancamento nave espaco voar"));
            _allEmojis.Add(new EmojiData("💡", "ideia luz lampada brilhante pensador"));
            _allEmojis.Add(new EmojiData("📝", "anotacao nota texto caderno escrever"));
            _allEmojis.Add(new EmojiData("📌", "fixar alfinete pino lembrete"));
            _allEmojis.Add(new EmojiData("🎯", "alvo meta objetivo foco certeiro"));
            _allEmojis.Add(new EmojiData("⭐", "estrela favorito brilho"));
            _allEmojis.Add(new EmojiData("🌟", "estrela brilhante show"));
            _allEmojis.Add(new EmojiData("✨", "brilhos magia magica encanto"));
            _allEmojis.Add(new EmojiData("🔥", "fogo quente hype brabo"));
            _allEmojis.Add(new EmojiData("💻", "computador notebook pc dev programador"));
            _allEmojis.Add(new EmojiData("📂", "pasta arquivos folder"));
            _allEmojis.Add(new EmojiData("📁", "pasta amarela"));
            _allEmojis.Add(new EmojiData("📄", "documento folha papel"));
            _allEmojis.Add(new EmojiData("🎨", "arte desenho paleta cores pintura"));
            _allEmojis.Add(new EmojiData("☕", "cafe cafezinho pausa coffee descanso"));
            _allEmojis.Add(new EmojiData("🧠", "cerebro mente inteligencia pensar"));
            _allEmojis.Add(new EmojiData("📖", "livro leitura estudar estudo"));
            _allEmojis.Add(new EmojiData("⚡", "raio eletricidade energia veloz"));
            _allEmojis.Add(new EmojiData("🛠️", "ferramentas manutencao conserto chave"));
            _allEmojis.Add(new EmojiData("📊", "grafico relatorio dados financeiro"));
            _allEmojis.Add(new EmojiData("🔍", "lupa busca pesquisar encontrar"));
            _allEmojis.Add(new EmojiData("🔒", "cadeado seguranca tranca chave"));
            _allEmojis.Add(new EmojiData("🎉", "festa confete comemorar vitoria"));
            _allEmojis.Add(new EmojiData("❤️", "coracao vermelho amor paixao"));
            _allEmojis.Add(new EmojiData("👍", "joia like positivo ok bom"));
            _allEmojis.Add(new EmojiData("🏆", "trofeu campeao premio primeiro"));
            _allEmojis.Add(new EmojiData("📅", "calendario data agenda"));
            _allEmojis.Add(new EmojiData("💰", "dinheiro grana riqueza saco"));
            _allEmojis.Add(new EmojiData("💼", "maleta trabalho emprego pasta"));
            _allEmojis.Add(new EmojiData("🌐", "globo internet web mundo"));
            _allEmojis.Add(new EmojiData("🎵", "musica cancao som nota"));
            _allEmojis.Add(new EmojiData("🍎", "maca fruta comida saude"));
            _allEmojis.Add(new EmojiData("🍕", "pizza comida lanche"));
            _allEmojis.Add(new EmojiData("⚽", "futebol bola esporte jogo"));

            // Exact Notion Gallery Palette (from Anexo 2: Cor e Gradiente)
            _coverItems.Add(new CoverItem("Coral Sólido", "#EB5757"));
            _coverItems.Add(new CoverItem("Amarelo Ouro", "#F2C94C"));
            _coverItems.Add(new CoverItem("Azul Céu", "#2D9CDB"));
            _coverItems.Add(new CoverItem("Creme Suave", "#F8F4EC"));
            _coverItems.Add(new CoverItem("Degradê Ciano Suave", "#48C6EF", "#6F86D6"));
            _coverItems.Add(new CoverItem("Degradê Magenta", "#FA709A", "#FEE140"));
            _coverItems.Add(new CoverItem("Degradê Sunset", "#FF416C", "#FF4B2B"));
            _coverItems.Add(new CoverItem("Degradê Peach Glow", "#FBC2EB", "#A6C1EE"));
            _coverItems.Add(new CoverItem("Degradê Oceano Escuro", "#2E3192", "#1BFFFF"));
            _coverItems.Add(new CoverItem("Degradê Berry", "#654EA3", "#EAAFC8"));
            _coverItems.Add(new CoverItem("Degradê Aurora Boreal", "#0F2027", "#2C5364"));
            _coverItems.Add(new CoverItem("Degradê Meia-noite", "#141E30", "#243B55"));
            // Texturelabs Presets
            _coverItems.Add(new CoverItem("Areia Deserto", "#B79878", "#93734F"));
            _coverItems.Add(new CoverItem("Mármore Ardósia", "#3A464A", "#1D2326"));
            _coverItems.Add(new CoverItem("Dunas Douradas", "#D4AC0D", "#7D6608"));
            _coverItems.Add(new CoverItem("Cosmos Galáxia", "#1B1464", "#0C092A"));
        }

        private void LoadOfficialIcon()
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(NOTEPAD_ICON_16_BASE64);
                using (var ms = new MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    _officialIconImage = bmp;
                    Icon = bmp;
                }
            }
            catch { }
        }

        private void ApplyDarkThemeStyles()
        {
            string xaml = @"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">

    <SolidColorBrush x:Key=""DarkMenuBg"" Color=""#262626""/>
    <SolidColorBrush x:Key=""DarkSubmenuBg"" Color=""#242424""/>
    <SolidColorBrush x:Key=""DarkSubmenuBorder"" Color=""#333333""/>
    <SolidColorBrush x:Key=""DarkHoverBg"" Color=""#333333""/>
    <SolidColorBrush x:Key=""DarkTextNormal"" Color=""#E0E0E0""/>
    <SolidColorBrush x:Key=""DarkTextHover"" Color=""#FFFFFF""/>
    <SolidColorBrush x:Key=""DarkTextMuted"" Color=""#9E9E9E""/>

    <Style TargetType=""{x:Type Separator}"">
        <Setter Property=""Height"" Value=""1""/>
        <Setter Property=""Margin"" Value=""6,3,6,3""/>
        <Setter Property=""Background"" Value=""#333333""/>
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Separator}"">
                    <Border Background=""{TemplateBinding Background}"" Height=""1""/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key=""TopMenuItem"" TargetType=""{x:Type MenuItem}"">
        <Setter Property=""Foreground"" Value=""{StaticResource DarkTextNormal}""/>
        <Setter Property=""FontSize"" Value=""12""/>
        <Setter Property=""Padding"" Value=""8,4,8,4""/>
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type MenuItem}"">
                    <Grid>
                        <Border x:Name=""Border"" Background=""Transparent"" CornerRadius=""4"" Padding=""{TemplateBinding Padding}"">
                            <ContentPresenter ContentSource=""Header"" RecognizesAccessKey=""True""/>
                        </Border>
                        <Popup x:Name=""PART_Popup""
                               IsOpen=""{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}""
                               Placement=""Bottom"" Focusable=""False"" AllowsTransparency=""True""
                               PopupAnimation=""Fade"">
                            <Border Background=""{StaticResource DarkSubmenuBg}""
                                    BorderBrush=""{StaticResource DarkSubmenuBorder}""
                                    BorderThickness=""1""
                                    CornerRadius=""6""
                                    Padding=""4,4,4,4""
                                    SnapsToDevicePixels=""True"">
                                <StackPanel IsItemsHost=""True"" KeyboardNavigation.DirectionalNavigation=""Cycle""/>
                            </Border>
                        </Popup>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsHighlighted"" Value=""True"">
                            <Setter TargetName=""Border"" Property=""Background"" Value=""{StaticResource DarkHoverBg}""/>
                            <Setter Property=""Foreground"" Value=""{StaticResource DarkTextHover}""/>
                        </Trigger>
                        <Trigger Property=""IsSubmenuOpen"" Value=""True"">
                            <Setter TargetName=""Border"" Property=""Background"" Value=""{StaticResource DarkHoverBg}""/>
                            <Setter Property=""Foreground"" Value=""{StaticResource DarkTextHover}""/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key=""SubMenuItem"" TargetType=""{x:Type MenuItem}"">
        <Setter Property=""Foreground"" Value=""{StaticResource DarkTextNormal}""/>
        <Setter Property=""FontSize"" Value=""12""/>
        <Setter Property=""Padding"" Value=""8,5,16,5""/>
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type MenuItem}"">
                    <Border x:Name=""Border"" Background=""Transparent"" CornerRadius=""4"" Margin=""2,1,2,1"" Padding=""{TemplateBinding Padding}"">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""20""/>
                                <ColumnDefinition Width=""*""/>
                                <ColumnDefinition Width=""Auto""/>
                            </Grid.ColumnDefinitions>
                            <TextBlock x:Name=""CheckMark"" Text=""✓"" Foreground=""{StaticResource DarkTextHover}""
                                       VerticalAlignment=""Center"" HorizontalAlignment=""Center"" Visibility=""Collapsed"" FontSize=""11""/>
                            <ContentPresenter Grid.Column=""1"" ContentSource=""Header"" RecognizesAccessKey=""True""/>
                            <TextBlock Grid.Column=""2"" Text=""{TemplateBinding InputGestureText}""
                                       Foreground=""{StaticResource DarkTextMuted}"" Margin=""24,0,0,0""
                                       VerticalAlignment=""Center""/>
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsChecked"" Value=""True"">
                            <Setter TargetName=""CheckMark"" Property=""Visibility"" Value=""Visible""/>
                        </Trigger>
                        <Trigger Property=""IsHighlighted"" Value=""True"">
                            <Setter TargetName=""Border"" Property=""Background"" Value=""{StaticResource DarkHoverBg}""/>
                            <Setter Property=""Foreground"" Value=""{StaticResource DarkTextHover}""/>
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter Property=""Foreground"" Value=""#666666""/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key=""SubMenuHeader"" TargetType=""{x:Type MenuItem}"">
        <Setter Property=""Foreground"" Value=""{StaticResource DarkTextNormal}""/>
        <Setter Property=""FontSize"" Value=""12""/>
        <Setter Property=""Padding"" Value=""8,5,12,5""/>
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type MenuItem}"">
                    <Grid>
                        <Border x:Name=""Border"" Background=""Transparent"" CornerRadius=""4"" Margin=""2,1,2,1"" Padding=""{TemplateBinding Padding}"">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width=""20""/>
                                    <ColumnDefinition Width=""*""/>
                                    <ColumnDefinition Width=""Auto""/>
                                </Grid.ColumnDefinitions>
                                <ContentPresenter Grid.Column=""1"" ContentSource=""Header"" RecognizesAccessKey=""True""/>
                                <TextBlock Grid.Column=""2"" Text=""▶"" FontSize=""9""
                                           Foreground=""{StaticResource DarkTextMuted}"" Margin=""24,0,0,0""
                                           VerticalAlignment=""Center""/>
                            </Grid>
                        </Border>
                        <Popup x:Name=""PART_Popup""
                               IsOpen=""{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}""
                               Placement=""Right"" Focusable=""False"" AllowsTransparency=""True""
                               PopupAnimation=""Fade"">
                            <Border Background=""{StaticResource DarkSubmenuBg}""
                                    BorderBrush=""{StaticResource DarkSubmenuBorder}""
                                    BorderThickness=""1""
                                    CornerRadius=""6""
                                    Padding=""4,4,4,4""
                                    SnapsToDevicePixels=""True"">
                                <StackPanel IsItemsHost=""True"" KeyboardNavigation.DirectionalNavigation=""Cycle""/>
                            </Border>
                        </Popup>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsHighlighted"" Value=""True"">
                            <Setter TargetName=""Border"" Property=""Background"" Value=""{StaticResource DarkHoverBg}""/>
                            <Setter Property=""Foreground"" Value=""{StaticResource DarkTextHover}""/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{x:Type ScrollBar}"">
        <Setter Property=""Background"" Value=""Transparent""/>
        <Setter Property=""Foreground"" Value=""#444444""/>
        <Setter Property=""Width"" Value=""8""/>
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ScrollBar}"">
                    <Grid Background=""Transparent"" SnapsToDevicePixels=""true"">
                        <Track x:Name=""PART_Track"" IsDirectionReversed=""true"">
                            <Track.Thumb>
                                <Thumb>
                                    <Thumb.Template>
                                        <ControlTemplate TargetType=""{x:Type Thumb}"">
                                            <Border CornerRadius=""4"" Background=""#3E3E3E"" Margin=""1,0,1,0""/>
                                        </ControlTemplate>
                                    </Thumb.Template>
                                </Thumb>
                            </Track.Thumb>
                        </Track>
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key=""CleanDarkButton"" TargetType=""{x:Type Button}"">
        <Setter Property=""Focusable"" Value=""False""/>
        <Setter Property=""FocusVisualStyle"" Value=""{x:Null}""/>
        <Setter Property=""Background"" Value=""Transparent""/>
        <Setter Property=""Foreground"" Value=""{StaticResource DarkTextNormal}""/>
        <Setter Property=""BorderThickness"" Value=""0""/>
        <Setter Property=""Cursor"" Value=""Hand""/>
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Button}"">
                    <Border x:Name=""bd"" Background=""{TemplateBinding Background}""
                            CornerRadius=""4"" SnapsToDevicePixels=""True"">
                        <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""bd"" Property=""Background"" Value=""{StaticResource DarkHoverBg}""/>
                            <Setter Property=""Foreground"" Value=""{StaticResource DarkTextHover}""/>
                        </Trigger>
                        <Trigger Property=""IsPressed"" Value=""True"">
                            <Setter TargetName=""bd"" Property=""Background"" Value=""#424242""/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

</ResourceDictionary>";

            try
            {
                var dict = (ResourceDictionary)XamlReader.Parse(xaml);
                Resources.MergedDictionaries.Add(dict);
            }
            catch { }
        }

        private void BuildInterface()
        {
            _rootGrid = new Grid();
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) }); // 0: Titlebar & Tabs
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });    // 1: Menu Bar Row
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });    // 2: Find & Replace Bar
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 3: Notion Editor Area
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // 4: Status Bar

            Content = _rootGrid;

            BuildTitleBar();
            BuildMenuBar();
            BuildFindBar();

            _editorContainer = new Grid { Background = _brushCanvasBg };
            Grid.SetRow(_editorContainer, 3);
            _rootGrid.Children.Add(_editorContainer);

            BuildStatusBar();
        }

        private void BuildTitleBar()
        {
            var topGrid = new Grid { Background = _brushTopBarBg };
            Grid.SetRow(topGrid, 0);

            topGrid.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            };

            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(6, 0, 0, 0)
            };
            WindowChrome.SetIsHitTestVisibleInChrome(leftContainer, true);

            _btnMenuToggle = new Button
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(2, 6, 4, 6),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "Alternar barra de menus (Arquivo, Editar, Exibir)",
                Focusable = false,
                FocusVisualStyle = null,
                VerticalAlignment = VerticalAlignment.Center
            };
            ApplyCleanButtonStyle(_btnMenuToggle);
            WindowChrome.SetIsHitTestVisibleInChrome(_btnMenuToggle, true);

            if (_officialIconImage != null)
            {
                var img = new Image
                {
                    Source = _officialIconImage,
                    Width = 16,
                    Height = 16,
                    SnapsToDevicePixels = true,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                _btnMenuToggle.Content = img;
            }
            else
            {
                _btnMenuToggle.Content = new Border
                {
                    Width = 15,
                    Height = 15,
                    CornerRadius = new CornerRadius(3),
                    Background = _brushBlueAccent
                };
            }

            _btnMenuToggle.Click += (s, e) => ToggleMenuBar();
            leftContainer.Children.Add(_btnMenuToggle);

            _tabsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(2, 0, 0, 0)
            };
            WindowChrome.SetIsHitTestVisibleInChrome(_tabsPanel, true);
            leftContainer.Children.Add(_tabsPanel);

            Grid.SetColumn(leftContainer, 0);
            topGrid.Children.Add(leftContainer);

            var btnNewTab = new Button
            {
                Content = "\uE710",
                FontFamily = _fontMdl2,
                FontSize = 10,
                Foreground = _brushSubtext,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Width = 28,
                Height = 28,
                Margin = new Thickness(4, 0, 0, 4),
                VerticalAlignment = VerticalAlignment.Bottom,
                Cursor = Cursors.Hand,
                ToolTip = "Nova guia (Ctrl+T)",
                Focusable = false
            };
            ApplyCleanButtonStyle(btnNewTab);
            btnNewTab.Click += (s, e) => AddNewTab("");
            WindowChrome.SetIsHitTestVisibleInChrome(btnNewTab, true);

            var plusContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            plusContainer.Children.Add(btnNewTab);
            Grid.SetColumn(plusContainer, 1);
            topGrid.Children.Add(plusContainer);

            var captionButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top
            };
            WindowChrome.SetIsHitTestVisibleInChrome(captionButtons, true);

            var btnMin = CreateCaptionButton("\uE921", 10, (s, e) => WindowState = WindowState.Minimized);
            _btnMaxRestore = CreateCaptionButton("\uE922", 10, (s, e) =>
            {
                WindowState = (WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
            });
            var btnClose = CreateCaptionButton("\uE8BB", 10, (s, e) => Close());
            SetCloseButtonHover(btnClose);

            captionButtons.Children.Add(btnMin);
            captionButtons.Children.Add(_btnMaxRestore);
            captionButtons.Children.Add(btnClose);

            Grid.SetColumn(captionButtons, 2);
            topGrid.Children.Add(captionButtons);

            _rootGrid.Children.Add(topGrid);
        }

        private Button CreateCaptionButton(string glyph, double fontSize, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Content = glyph,
                FontFamily = _fontMdl2,
                FontSize = fontSize,
                Width = 46,
                Height = 32,
                Foreground = _brushSubtext,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null
            };
            ApplyCleanButtonStyle(btn);
            btn.Click += handler;
            return btn;
        }

        private void SetCloseButtonHover(Button btn)
        {
            btn.MouseEnter += (s, e) => { btn.Background = _brushCloseRed; btn.Foreground = Brushes.White; };
            btn.MouseLeave += (s, e) => { btn.Background = Brushes.Transparent; btn.Foreground = _brushSubtext; };
        }

        private void ApplyCleanButtonStyle(Button btn)
        {
            if (Resources.Contains("CleanDarkButton"))
            {
                btn.Style = (Style)Resources["CleanDarkButton"];
            }
        }

        public void ToggleMenuBar()
        {
            if (_menuGrid == null) return;
            _menuGrid.Visibility = (_menuGrid.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BuildMenuBar()
        {
            _menuGrid = new Grid
            {
                Background = _brushMenuBarBg,
                Margin = new Thickness(8, 2, 8, 2),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(_menuGrid, 1);

            _menuGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _menuGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var menu = new Menu
            {
                Background = Brushes.Transparent,
                Foreground = _brushText,
                FontSize = 12
            };

            var mFile = CreateMenuHeader("Arquivo");
            AddMenuItem(mFile, "Nova guia", "Ctrl+T", (s, e) => AddNewTab(""));
            AddMenuItem(mFile, "Nova janela", "Ctrl+N", (s, e) => OpenNewWindow());
            AddMenuItem(mFile, "Abrir...", "Ctrl+O", (s, e) => OpenFileDialog());
            AddMenuItem(mFile, "Salvar", "Ctrl+S", (s, e) => SaveCurrentFile(false));
            AddMenuItem(mFile, "Salvar como...", "Ctrl+Shift+S", (s, e) => SaveCurrentFile(true));
            mFile.Items.Add(new Separator());
            AddMenuItem(mFile, "Fechar guia", "Ctrl+W", (s, e) => CloseTab(_currentDoc));
            AddMenuItem(mFile, "Sair", "Alt+F4", (s, e) => Close());
            menu.Items.Add(mFile);

            var mEdit = CreateMenuHeader("Editar");
            AddMenuItem(mEdit, "Desfazer", "Ctrl+Z", (s, e) => { if (_currentDoc != null && _currentDoc.Editor.CanUndo) _currentDoc.Editor.Undo(); });
            AddMenuItem(mEdit, "Refazer", "Ctrl+Y", (s, e) => { if (_currentDoc != null && _currentDoc.Editor.CanRedo) _currentDoc.Editor.Redo(); });
            mEdit.Items.Add(new Separator());
            AddMenuItem(mEdit, "Recortar", "Ctrl+X", (s, e) => { if (_currentDoc != null) _currentDoc.Editor.Cut(); });
            AddMenuItem(mEdit, "Copiar", "Ctrl+C", (s, e) => { if (_currentDoc != null) _currentDoc.Editor.Copy(); });
            AddMenuItem(mEdit, "Colar", "Ctrl+V", (s, e) => { if (_currentDoc != null) _currentDoc.Editor.Paste(); });
            AddMenuItem(mEdit, "Excluir", "Del", (s, e) => { if (_currentDoc != null) _currentDoc.Editor.Selection.Text = ""; });
            mEdit.Items.Add(new Separator());
            AddMenuItem(mEdit, "Localizar...", "Ctrl+F", (s, e) => ShowFindBar(false));
            AddMenuItem(mEdit, "Substituir...", "Ctrl+H", (s, e) => ShowFindBar(true));
            AddMenuItem(mEdit, "Selecionar tudo", "Ctrl+A", (s, e) => { if (_currentDoc != null) _currentDoc.Editor.SelectAll(); });
            AddMenuItem(mEdit, "Hora/Data", "F5", (s, e) => InsertDateTime());
            menu.Items.Add(mEdit);

            var mView = CreateMenuHeader("Exibir");
            var mZoom = CreateSubmenuHeader("Zoom");
            AddMenuItem(mZoom, "Ampliar", "Ctrl+Mais", (s, e) => ChangeZoom(0.1));
            AddMenuItem(mZoom, "Reduzir", "Ctrl+Menos", (s, e) => ChangeZoom(-0.1));
            AddMenuItem(mZoom, "Restaurar zoom padrão", "Ctrl+0", (s, e) => SetZoom(1.0));
            mView.Items.Add(mZoom);

            var itemWordWrap = CreateCheckableMenuItem("Quebra automática de linha", _isWordWrap);
            itemWordWrap.Click += (s, e) =>
            {
                _isWordWrap = !_isWordWrap;
                itemWordWrap.IsChecked = _isWordWrap;
            };
            mView.Items.Add(itemWordWrap);

            var itemStatusBar = CreateCheckableMenuItem("Barra de status", _showStatusBar);
            itemStatusBar.Click += (s, e) =>
            {
                _showStatusBar = !_showStatusBar;
                itemStatusBar.IsChecked = _showStatusBar;
                _statusBarGrid.Visibility = _showStatusBar ? Visibility.Visible : Visibility.Collapsed;
            };
            mView.Items.Add(itemStatusBar);

            menu.Items.Add(mView);

            Grid.SetColumn(menu, 0);
            _menuGrid.Children.Add(menu);

            // Right side: Settings Gear ⚙
            var btnSettings = new Button
            {
                Content = "\uE713",
                FontFamily = _fontMdl2,
                FontSize = 13,
                Foreground = _brushSubtext,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Width = 32,
                Height = 26,
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand,
                ToolTip = "Configurações",
                Focusable = false,
                FocusVisualStyle = null
            };
            ApplyCleanButtonStyle(btnSettings);
            btnSettings.Click += (s, e) => ShowSettingsDialog();

            Grid.SetColumn(btnSettings, 1);
            _menuGrid.Children.Add(btnSettings);

            _rootGrid.Children.Add(_menuGrid);
        }

        private MenuItem CreateMenuHeader(string header)
        {
            var item = new MenuItem { Header = header };
            if (Resources.Contains("TopMenuItem")) item.Style = (Style)Resources["TopMenuItem"];
            return item;
        }

        private MenuItem CreateSubmenuHeader(string header)
        {
            var item = new MenuItem { Header = header };
            if (Resources.Contains("SubMenuHeader")) item.Style = (Style)Resources["SubMenuHeader"];
            return item;
        }

        private MenuItem CreateCheckableMenuItem(string header, bool isChecked)
        {
            var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = isChecked };
            if (Resources.Contains("SubMenuItem")) item.Style = (Style)Resources["SubMenuItem"];
            return item;
        }

        private void AddMenuItem(MenuItem parent, string header, string shortcut, RoutedEventHandler handler)
        {
            var item = new MenuItem { Header = header };
            if (!string.IsNullOrEmpty(shortcut)) item.InputGestureText = shortcut;
            if (Resources.Contains("SubMenuItem")) item.Style = (Style)Resources["SubMenuItem"];
            item.Click += handler;
            parent.Items.Add(item);
        }

        private void BuildFindBar()
        {
            _findBar = new Border
            {
                Visibility = Visibility.Collapsed,
                Background = _brushTabActive,
                BorderBrush = _brushBorder,
                BorderThickness = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(14, 8, 14, 8)
            };
            Grid.SetRow(_findBar, 2);

            var stack = new StackPanel { Orientation = Orientation.Vertical };

            var rowFind = new StackPanel { Orientation = Orientation.Horizontal };
            rowFind.Children.Add(new TextBlock { Text = "Localizar:", VerticalAlignment = VerticalAlignment.Center, Foreground = _brushText, Margin = new Thickness(0, 0, 8, 0) });
            _findBox = new TextBox
            {
                Width = 220,
                Height = 26,
                Background = _brushMenuBarBg,
                Foreground = _brushText,
                CaretBrush = _brushText,
                BorderBrush = _brushBorder,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0)
            };
            _findBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) FindNext(false);
                    else FindNext(true);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    _findBar.Visibility = Visibility.Collapsed;
                    _currentMatches.Clear();
                    _currentMatchIndex = -1;
                    if (_currentDoc != null) _currentDoc.Editor.Focus();
                    e.Handled = true;
                }
            };
            _findBox.TextChanged += (s, e) => UpdateFindSearch(true);
            rowFind.Children.Add(_findBox);

            var btnPrev = CreateSmallButton("\uE74A", (s, e) => FindNext(false), true);
            btnPrev.ToolTip = "Anterior (Shift+Enter / Shift+F3)";
            rowFind.Children.Add(btnPrev);

            var btnNext = CreateSmallButton("\uE74B", (s, e) => FindNext(true), true);
            btnNext.ToolTip = "Próximo (Enter / F3)";
            rowFind.Children.Add(btnNext);

            _findCountText = new TextBlock
            {
                Text = "",
                Foreground = _brushSubtext,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 0),
                MinWidth = 80
            };
            rowFind.Children.Add(_findCountText);

            _matchCaseBox = new CheckBox
            {
                Content = "Diferenciar maiúsculas de minúsculas",
                Foreground = _brushText,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _matchCaseBox.Click += (s, e) => UpdateFindSearch(true);
            rowFind.Children.Add(_matchCaseBox);

            var btnClose = CreateSmallButton("\uE8BB", (s, e) =>
            {
                _findBar.Visibility = Visibility.Collapsed;
                _currentMatches.Clear();
                _currentMatchIndex = -1;
                if (_currentDoc != null) _currentDoc.Editor.Focus();
            }, true);
            btnClose.ToolTip = "Fechar (Esc)";
            btnClose.Margin = new Thickness(16, 0, 0, 0);
            rowFind.Children.Add(btnClose);

            stack.Children.Add(rowFind);

            _replaceRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            _replaceRow.Children.Add(new TextBlock { Text = "Substituir:", VerticalAlignment = VerticalAlignment.Center, Foreground = _brushText, Margin = new Thickness(0, 0, 8, 0) });
            _replaceBox = new TextBox
            {
                Width = 220,
                Height = 26,
                Background = _brushMenuBarBg,
                Foreground = _brushText,
                CaretBrush = _brushText,
                BorderBrush = _brushBorder,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0)
            };
            _replaceBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ReplaceOne();
                    e.Handled = true;
                }
            };
            _replaceRow.Children.Add(_replaceBox);

            var btnReplace = CreateSmallButton("Substituir", (s, e) => ReplaceOne(), false);
            btnReplace.ToolTip = "Substituir ocorrência atual e avançar";
            _replaceRow.Children.Add(btnReplace);

            var btnReplaceAll = CreateSmallButton("Substituir tudo", (s, e) => ReplaceAll(), false);
            btnReplaceAll.ToolTip = "Substituir todas as ocorrências no documento";
            _replaceRow.Children.Add(btnReplaceAll);

            stack.Children.Add(_replaceRow);
            _findBar.Child = stack;

            _rootGrid.Children.Add(_findBar);
        }

        private Button CreateSmallButton(string text, RoutedEventHandler handler, bool isMdl2)
        {
            var btn = new Button
            {
                Content = text,
                Height = 24,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(6, 0, 0, 0),
                Background = _brushMenuBarBg,
                Foreground = _brushText,
                BorderBrush = _brushBorder,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Focusable = false
            };
            if (isMdl2)
            {
                btn.FontFamily = _fontMdl2;
                btn.FontSize = 9.5;
                btn.Width = 26;
                btn.Padding = new Thickness(0);
            }
            ApplyCleanButtonStyle(btn);
            btn.Click += handler;
            return btn;
        }

        private void BuildStatusBar()
        {
            _statusBarGrid = new Grid
            {
                Background = _brushStatusBarBg,
                Margin = new Thickness(14, 0, 14, 0)
            };
            Grid.SetRow(_statusBarGrid, 4);

            _statusBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _statusBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _statusLineCol = new TextBlock { Text = "Ln 1, Col 1", Foreground = _brushSubtext, FontSize = 12, Margin = new Thickness(0, 0, 24, 0) };
            _statusChars = new TextBlock { Text = "0 caracteres", Foreground = _brushSubtext, FontSize = 12 };
            leftStack.Children.Add(_statusLineCol);
            leftStack.Children.Add(_statusChars);
            Grid.SetColumn(leftStack, 0);
            _statusBarGrid.Children.Add(leftStack);

            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _statusLanguageBtn = new Button
            {
                Content = "Texto \u25BE",
                FontSize = 12,
                Foreground = _brushSubtext,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 18, 0),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null,
                ToolTip = "Linguagem / Realce de Sintaxe"
            };
            ApplyCleanButtonStyle(_statusLanguageBtn);
            _statusLanguageBtn.Click += (s, e) => ShowLanguageMenu();
            rightStack.Children.Add(_statusLanguageBtn);

            _statusZoom = new TextBlock { Text = "100%", Foreground = _brushSubtext, FontSize = 12, Margin = new Thickness(0, 0, 24, 0) };
            _statusFormat = new TextBlock { Text = "Windows (CRLF)", Foreground = _brushSubtext, FontSize = 12, Margin = new Thickness(0, 0, 24, 0) };
            _statusEncoding = new TextBlock { Text = "UTF-8", Foreground = _brushSubtext, FontSize = 12 };
            rightStack.Children.Add(_statusZoom);
            rightStack.Children.Add(_statusFormat);
            rightStack.Children.Add(_statusEncoding);
            Grid.SetColumn(rightStack, 1);
            _statusBarGrid.Children.Add(rightStack);

            _rootGrid.Children.Add(_statusBarGrid);
        }

        // ==================== NOTION FLOATING TOOLBAR ====================
        private void BuildFloatingToolbar()
        {
            _floatingToolbar = new Popup
            {
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                Placement = PlacementMode.Absolute,
                StaysOpen = true
            };

            var border = new Border
            {
                Background = _brushFloatingBg,
                BorderBrush = _brushBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 4, 6, 4),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 14,
                    Opacity = 0.5,
                    ShadowDepth = 2,
                    Color = Colors.Black
                }
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            var btnBlockType = CreateToolbarButton("T Texto normal ▾", 12, false, () =>
            {
                if (_blockTypePopup != null)
                {
                    _blockTypePopup.Placement = PlacementMode.Bottom;
                    _blockTypePopup.PlacementTarget = _floatingToolbar.Child;
                    _blockTypePopup.IsOpen = !_blockTypePopup.IsOpen;
                }
            });
            btnBlockType.Width = 110;
            btnBlockType.Padding = new Thickness(6, 2, 6, 2);
            stack.Children.Add(btnBlockType);

            stack.Children.Add(CreateToolbarSeparator());

            var btnColor = CreateToolbarButton("A", 13, true, () => ToggleHighlight());
            btnColor.ToolTip = "Destaque (Marca-texto Notion)";
            stack.Children.Add(btnColor);

            var btnBold = CreateToolbarButton("B", 13, true, () =>
            {
                if (_currentDoc != null) EditingCommands.ToggleBold.Execute(null, _currentDoc.Editor);
            });
            btnBold.FontWeight = FontWeights.Bold;
            btnBold.ToolTip = "Negrito (Ctrl+B)";
            stack.Children.Add(btnBold);

            var btnItalic = CreateToolbarButton("I", 13, true, () =>
            {
                if (_currentDoc != null) EditingCommands.ToggleItalic.Execute(null, _currentDoc.Editor);
            });
            btnItalic.FontStyle = FontStyles.Italic;
            btnItalic.ToolTip = "Itálico (Ctrl+I)";
            stack.Children.Add(btnItalic);

            var btnUnderline = CreateToolbarButton("U", 13, true, () =>
            {
                if (_currentDoc != null) EditingCommands.ToggleUnderline.Execute(null, _currentDoc.Editor);
            });
            btnUnderline.ToolTip = "Sublinhado (Ctrl+U)";
            stack.Children.Add(btnUnderline);

            var btnStrike = CreateToolbarButton("S", 12, true, () => ToggleStrikethrough());
            btnStrike.ToolTip = "Tachado";
            stack.Children.Add(btnStrike);

            stack.Children.Add(CreateToolbarSeparator());

            var btnLink = CreateToolbarButton("🔗", 11, true, () => InsertLink());
            btnLink.ToolTip = "Link";
            stack.Children.Add(btnLink);

            border.Child = stack;
            _floatingToolbar.Child = border;

            BuildBlockTypePopup();
        }

        private Separator CreateToolbarSeparator()
        {
            return new Separator { Width = 1, Height = 16, Margin = new Thickness(4, 2, 4, 2), Background = _brushBorder };
        }

        private Button CreateToolbarButton(string text, double fontSize, bool fixedWidth, Action action)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = fontSize,
                Foreground = _brushText,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Height = 26,
                Margin = new Thickness(1, 0, 1, 0),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null
            };
            if (fixedWidth) btn.Width = 28;
            ApplyCleanButtonStyle(btn);

            btn.PreviewMouseDown += (s, e) =>
            {
                e.Handled = true;
                action();
                if (_currentDoc != null && _currentDoc.Editor != null)
                {
                    _currentDoc.Editor.Focus();
                }
            };
            return btn;
        }

        private void BuildBlockTypePopup()
        {
            _blockTypePopup = new Popup
            {
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                Placement = PlacementMode.Bottom,
                StaysOpen = false
            };

            var border = new Border
            {
                Background = _brushFloatingBg,
                BorderBrush = _brushBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(4),
                Width = 200,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, Opacity = 0.5, Color = Colors.Black }
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };

            AddBlockTypeItem(stack, "T  Texto normal", () => ApplyBlockStyle(15, FontWeights.Normal));
            AddBlockTypeItem(stack, "H1 Título 1", () => ApplyBlockStyle(26, FontWeights.Bold));
            AddBlockTypeItem(stack, "H2 Título 2", () => ApplyBlockStyle(21, FontWeights.Bold));
            AddBlockTypeItem(stack, "H3 Título 3", () => ApplyBlockStyle(18, FontWeights.SemiBold));
            stack.Children.Add(new Separator { Background = _brushBorder, Margin = new Thickness(2, 4, 2, 4) });
            AddBlockTypeItem(stack, "•≡ Lista com marcadores", () => ApplyListStyle(TextMarkerStyle.Disc));
            AddBlockTypeItem(stack, "1. Lista numerada", () => ApplyListStyle(TextMarkerStyle.Decimal));
            AddBlockTypeItem(stack, "❝ Citação", () => ApplyQuoteStyle());
            AddBlockTypeItem(stack, "</> Código", () => ApplyCodeStyle());

            border.Child = stack;
            _blockTypePopup.Child = border;
        }

        private void AddBlockTypeItem(StackPanel panel, string label, Action action)
        {
            var btn = new Button
            {
                Content = label,
                FontSize = 12,
                Foreground = _brushText,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Height = 28,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 0, 8, 0),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null
            };
            ApplyCleanButtonStyle(btn);
            btn.PreviewMouseDown += (s, e) =>
            {
                e.Handled = true;
                _blockTypePopup.IsOpen = false;
                action();
                if (_currentDoc != null && _currentDoc.Editor != null)
                {
                    _currentDoc.Editor.Focus();
                }
            };
            panel.Children.Add(btn);
        }

        private void ApplyBlockStyle(double fontSize, FontWeight weight)
        {
            if (_currentDoc == null || _currentDoc.Editor.Selection.IsEmpty) return;
            var sel = _currentDoc.Editor.Selection;
            sel.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
            sel.ApplyPropertyValue(TextElement.FontWeightProperty, weight);
            _currentDoc.IsModified = true;
        }

        private void ApplyListStyle(TextMarkerStyle marker)
        {
            if (_currentDoc == null) return;
            try
            {
                EditingCommands.ToggleBullets.Execute(null, _currentDoc.Editor);
                _currentDoc.IsModified = true;
            }
            catch { }
        }

        private void ApplyQuoteStyle()
        {
            if (_currentDoc == null || _currentDoc.Editor.Selection.IsEmpty) return;
            var sel = _currentDoc.Editor.Selection;
            sel.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
            sel.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(180, 180, 180)));
            _currentDoc.IsModified = true;
        }

        private void ApplyCodeStyle()
        {
            if (_currentDoc == null || _currentDoc.Editor.Selection.IsEmpty) return;
            var sel = _currentDoc.Editor.Selection;
            sel.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily("Consolas, Courier New"));
            sel.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(Color.FromRgb(50, 50, 50)));
            sel.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(255, 120, 120)));
            _currentDoc.IsModified = true;
        }

        private void ToggleHighlight()
        {
            if (_currentDoc == null || _currentDoc.Editor.Selection.IsEmpty) return;
            var sel = _currentDoc.Editor.Selection;
            var cur = sel.GetPropertyValue(TextElement.BackgroundProperty);
            var yellow = new SolidColorBrush(Color.FromArgb(140, 245, 158, 11));

            if (cur != null && !cur.Equals(Brushes.Transparent) && !cur.Equals(DependencyProperty.UnsetValue))
            {
                sel.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
            }
            else
            {
                sel.ApplyPropertyValue(TextElement.BackgroundProperty, yellow);
            }
            _currentDoc.IsModified = true;
        }

        private void ToggleStrikethrough()
        {
            if (_currentDoc == null || _currentDoc.Editor.Selection.IsEmpty) return;
            var sel = _currentDoc.Editor.Selection;
            var cur = sel.GetPropertyValue(Inline.TextDecorationsProperty);

            if (cur != null && cur.Equals(TextDecorations.Strikethrough))
            {
                sel.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
            }
            else
            {
                sel.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Strikethrough);
            }
            _currentDoc.IsModified = true;
        }

        private void InsertLink()
        {
            if (_currentDoc == null || _currentDoc.Editor.Selection.IsEmpty) return;
            var sel = _currentDoc.Editor.Selection;
            sel.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
            sel.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(82, 156, 253)));
            _currentDoc.IsModified = true;
        }

        // ==================== NOTION EMOJI PICKER (ANEXO 5) ====================
        private void BuildEmojiPicker()
        {
            _emojiPickerPopup = new Popup
            {
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                Placement = PlacementMode.Bottom,
                StaysOpen = false
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
                BorderBrush = _brushBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Width = 350,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, Opacity = 0.6, Color = Colors.Black }
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };

            var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tabTitle = new TextBlock
            {
                Text = "Emoji",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tabTitle, 0);
            headerRow.Children.Add(tabTitle);

            var btnRemove = new Button
            {
                Content = "Remover",
                FontSize = 12,
                Foreground = _brushSubtext,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null
            };
            btnRemove.MouseEnter += (s, e) => btnRemove.Foreground = Brushes.White;
            btnRemove.MouseLeave += (s, e) => btnRemove.Foreground = _brushSubtext;
            btnRemove.Click += (s, e) =>
            {
                SetPageEmoji("");
                _emojiPickerPopup.IsOpen = false;
            };
            Grid.SetColumn(btnRemove, 1);
            headerRow.Children.Add(btnRemove);

            stack.Children.Add(headerRow);

            var searchBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(25, 25, 25)),
                BorderBrush = _brushBlueAccent,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var searchIcon = new TextBlock
            {
                Text = "🔍",
                FontSize = 11,
                Foreground = _brushSubtext,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(searchIcon, 0);
            searchGrid.Children.Add(searchIcon);

            _emojiSearchBox = new TextBox
            {
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            _emojiSearchBox.TextChanged += (s, e) => FilterEmojis(_emojiSearchBox.Text);
            Grid.SetColumn(_emojiSearchBox, 1);
            searchGrid.Children.Add(_emojiSearchBox);

            searchBorder.Child = searchGrid;
            stack.Children.Add(searchBorder);

            var emojiScroller = new ScrollViewer
            {
                Height = 220,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            _emojiGridWrap = new WrapPanel { Orientation = Orientation.Horizontal, Width = 320 };
            PopulateEmojiGrid(_allEmojis);

            emojiScroller.Content = _emojiGridWrap;
            stack.Children.Add(emojiScroller);

            border.Child = stack;
            _emojiPickerPopup.Child = border;
        }

        private void PopulateEmojiGrid(IEnumerable<EmojiData> list)
        {
            _emojiGridWrap.Children.Clear();
            foreach (var item in list)
            {
                var btn = new Border
                {
                    Width = 34,
                    Height = 34,
                    CornerRadius = new CornerRadius(4),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(1)
                };

                var txt = new TextBlock
                {
                    Text = item.Glyph,
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                btn.Child = txt;

                btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(Color.FromRgb(50, 50, 50));
                btn.MouseLeave += (s, e) => btn.Background = Brushes.Transparent;

                string glyph = item.Glyph;
                btn.MouseLeftButtonDown += (s, e) =>
                {
                    SetPageEmoji(glyph);
                    _emojiPickerPopup.IsOpen = false;
                };

                _emojiGridWrap.Children.Add(btn);
            }
        }

        private void FilterEmojis(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                PopulateEmojiGrid(_allEmojis);
                return;
            }

            string q = query.Trim().ToLowerInvariant();
            var matches = new List<EmojiData>();
            foreach (var item in _allEmojis)
            {
                if (item.Glyph == q || item.Keywords.ToLowerInvariant().Contains(q))
                {
                    matches.Add(item);
                }
            }
            PopulateEmojiGrid(matches);
        }

        private void SetPageEmoji(string emo)
        {
            if (_currentDoc == null) return;
            _currentDoc.EmojiIcon = emo;
            _currentDoc.UpdateHeaderState();
            _currentDoc.UpdateTitle(_currentDoc.Title, true);
        }

        // ==================== NOTION COVER PICKER (EXACT REPLICA DE ANEXO 2 & 3) ====================
        private void BuildCoverPicker()
        {
            _coverPickerPopup = new Popup
            {
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                Placement = PlacementMode.Bottom,
                StaysOpen = false
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
                BorderBrush = _brushBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Width = 520,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, Opacity = 0.6, Color = Colors.Black }
            };

            var mainStack = new StackPanel { Orientation = Orientation.Vertical };

            // Top Tab Navigation Bar ("Galeria", "Carregar", "Link", "Remover") - Anexo 2
            var topNavGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            topNavGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topNavGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tabsRow = new StackPanel { Orientation = Orientation.Horizontal };

            _tabBtnGallery = CreateCoverTabBtn("Galeria", true, () => SwitchCoverTab(0));
            _tabBtnUpload = CreateCoverTabBtn("Carregar", false, () => SwitchCoverTab(1));
            _tabBtnLink = CreateCoverTabBtn("Link", false, () => SwitchCoverTab(2));

            tabsRow.Children.Add(_tabBtnGallery);
            tabsRow.Children.Add(_tabBtnUpload);
            tabsRow.Children.Add(_tabBtnLink);
            Grid.SetColumn(tabsRow, 0);
            topNavGrid.Children.Add(tabsRow);

            var btnRemove = new Button
            {
                Content = "Remover",
                FontSize = 12,
                Foreground = _brushSubtext,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null
            };
            btnRemove.MouseEnter += (s, e) => btnRemove.Foreground = new SolidColorBrush(Color.FromRgb(240, 80, 80));
            btnRemove.MouseLeave += (s, e) => btnRemove.Foreground = _brushSubtext;
            btnRemove.Click += (s, e) =>
            {
                _coverPickerPopup.IsOpen = false;
                RemovePageCover();
            };
            Grid.SetColumn(btnRemove, 1);
            topNavGrid.Children.Add(btnRemove);

            mainStack.Children.Add(topNavGrid);

            // Container for Tab Contents
            var contentArea = new Grid();

            // 1. Tab Galeria (Cores e Gradientes - Anexo 2)
            _coverGalleryScroller = new ScrollViewer
            {
                Height = 260,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var galleryStack = new StackPanel { Orientation = Orientation.Vertical };

            galleryStack.Children.Add(new TextBlock
            {
                Text = "Cor e Gradiente",
                FontSize = 12,
                Foreground = _brushSubtext,
                Margin = new Thickness(2, 0, 0, 8)
            });

            _coverGalleryGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 16) };
            for (int i = 0; i < 12; i++)
            {
                AddGalleryCard(_coverGalleryGrid, _coverItems[i]);
            }
            galleryStack.Children.Add(_coverGalleryGrid);

            galleryStack.Children.Add(new TextBlock
            {
                Text = "Texturelabs",
                FontSize = 12,
                Foreground = _brushSubtext,
                Margin = new Thickness(2, 0, 0, 8)
            });

            var textureGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 8) };
            for (int i = 12; i < _coverItems.Count; i++)
            {
                AddGalleryCard(textureGrid, _coverItems[i]);
            }
            galleryStack.Children.Add(textureGrid);

            _coverGalleryScroller.Content = galleryStack;
            contentArea.Children.Add(_coverGalleryScroller);

            // 2. Tab Carregar (Upload)
            _coverUploadPanel = new Grid { Visibility = Visibility.Collapsed, Height = 260 };
            var uploadBox = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 28)),
                BorderBrush = _brushBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(24),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var uploadStack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
            uploadStack.Children.Add(new TextBlock
            {
                Text = "📁",
                FontSize = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });
            var btnUpload = new Button
            {
                Content = "Carregar imagem do computador",
                FontSize = 13,
                Foreground = Brushes.White,
                Background = _brushBlueAccent,
                Padding = new Thickness(16, 6, 16, 6),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null
            };
            ApplyCleanButtonStyle(btnUpload);
            btnUpload.Click += (s, e) =>
            {
                _coverPickerPopup.IsOpen = false;
                PickLocalCoverImage();
            };
            uploadStack.Children.Add(btnUpload);
            uploadStack.Children.Add(new TextBlock
            {
                Text = "Tamanho recomendado: 1500 x 600 pixels (PNG, JPG, BMP)",
                FontSize = 11,
                Foreground = _brushSubtext,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            uploadBox.Child = uploadStack;
            _coverUploadPanel.Children.Add(uploadBox);
            contentArea.Children.Add(_coverUploadPanel);

            // 3. Tab Link (URL)
            _coverLinkPanel = new Grid { Visibility = Visibility.Collapsed, Height = 260 };
            var linkBox = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 20, 0) };
            linkBox.Children.Add(new TextBlock
            {
                Text = "Cole a URL da imagem da web:",
                FontSize = 12,
                Foreground = _brushSubtext,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var linkInput = new TextBox
            {
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 24)),
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                BorderBrush = _brushBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 12
            };
            linkBox.Children.Add(linkInput);

            var btnSubmitLink = new Button
            {
                Content = "Inserir imagem",
                FontSize = 12,
                Foreground = Brushes.White,
                Background = _brushBlueAccent,
                Height = 30,
                Margin = new Thickness(0, 12, 0, 0),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null
            };
            ApplyCleanButtonStyle(btnSubmitLink);
            btnSubmitLink.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(linkInput.Text))
                {
                    _coverPickerPopup.IsOpen = false;
                    SetPageCoverFromUrl(linkInput.Text.Trim());
                }
            };
            linkBox.Children.Add(btnSubmitLink);
            _coverLinkPanel.Children.Add(linkBox);
            contentArea.Children.Add(_coverLinkPanel);

            mainStack.Children.Add(contentArea);

            border.Child = mainStack;
            _coverPickerPopup.Child = border;
        }

        private Button CreateCoverTabBtn(string label, bool isSelected, Action action)
        {
            var btn = new Button
            {
                Content = label,
                FontSize = 12,
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isSelected ? Brushes.White : _brushSubtext,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 16, 0),
                Padding = new Thickness(0, 0, 0, 4),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null
            };
            btn.Click += (s, e) => action();
            return btn;
        }

        private void SwitchCoverTab(int index)
        {
            _tabBtnGallery.Foreground = (index == 0) ? Brushes.White : _brushSubtext;
            _tabBtnGallery.FontWeight = (index == 0) ? FontWeights.SemiBold : FontWeights.Normal;

            _tabBtnUpload.Foreground = (index == 1) ? Brushes.White : _brushSubtext;
            _tabBtnUpload.FontWeight = (index == 1) ? FontWeights.SemiBold : FontWeights.Normal;

            _tabBtnLink.Foreground = (index == 2) ? Brushes.White : _brushSubtext;
            _tabBtnLink.FontWeight = (index == 2) ? FontWeights.SemiBold : FontWeights.Normal;

            if (_coverGalleryScroller != null) _coverGalleryScroller.Visibility = (index == 0) ? Visibility.Visible : Visibility.Collapsed;
            if (_coverUploadPanel != null) _coverUploadPanel.Visibility = (index == 1) ? Visibility.Visible : Visibility.Collapsed;
            if (_coverLinkPanel != null) _coverLinkPanel.Visibility = (index == 2) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddGalleryCard(UniformGrid grid, CoverItem item)
        {
            var card = new Border
            {
                Height = 52,
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(6),
                Background = item.DisplayBrush,
                Cursor = Cursors.Hand,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1.5),
                ToolTip = item.Name
            };

            card.MouseEnter += (s, e) => card.BorderBrush = Brushes.White;
            card.MouseLeave += (s, e) => card.BorderBrush = Brushes.Transparent;

            card.MouseLeftButtonDown += (s, e) =>
            {
                _coverPickerPopup.IsOpen = false;
                ApplyGalleryCover(item);
            };

            grid.Children.Add(card);
        }

        private void ApplyGalleryCover(CoverItem item)
        {
            if (_currentDoc == null) return;
            _currentDoc.CoverType = item.Type;
            _currentDoc.CoverValue1 = item.Color1;
            _currentDoc.CoverValue2 = item.Color2;
            _currentDoc.CoverOffsetY = 0.0;

            if (_currentDoc.CoverImageElement != null)
            {
                _currentDoc.CoverImageElement.Visibility = Visibility.Collapsed;
            }
            if (_currentDoc.CoverBanner != null)
            {
                _currentDoc.CoverBanner.Background = item.DisplayBrush;
            }
            _currentDoc.UpdateHeaderState();
            _currentDoc.IsModified = true;
        }

        private void SetPageCoverFromUrl(string url)
        {
            if (_currentDoc == null) return;
            try
            {
                var bmp = new BitmapImage(new Uri(url, UriKind.Absolute));
                ApplyImageToCover(bmp, url, "url");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível carregar a imagem da URL:\n" + ex.Message, "NotionPad", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PickLocalCoverImage()
        {
            if (_currentDoc == null) return;
            var dlg = new OpenFileDialog
            {
                Filter = "Imagens (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Todos os arquivos (*.*)|*.*",
                Title = "Escolha a Imagem de Capa"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var bmp = new BitmapImage(new Uri(dlg.FileName, UriKind.Absolute));
                    ApplyImageToCover(bmp, dlg.FileName, "image");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao carregar imagem: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ApplyImageToCover(BitmapImage bmp, string pathOrUrl, string coverType)
        {
            if (_currentDoc == null) return;
            _currentDoc.CoverType = coverType;
            _currentDoc.CoverValue1 = pathOrUrl;
            _currentDoc.CoverOffsetY = 0.0;

            if (_currentDoc.CoverImageElement != null)
            {
                _currentDoc.CoverImageElement.Source = bmp;
                _currentDoc.CoverImageElement.Visibility = Visibility.Visible;
                if (_currentDoc.CoverTransform != null) _currentDoc.CoverTransform.Y = 0;
            }
            if (_currentDoc.CoverBanner != null)
            {
                _currentDoc.CoverBanner.Background = Brushes.Black;
            }
            _currentDoc.UpdateHeaderState();
            _currentDoc.IsModified = true;
        }

        private void RemovePageCover()
        {
            if (_currentDoc == null) return;
            _currentDoc.CoverType = "none";
            _currentDoc.CoverValue1 = null;
            _currentDoc.CoverValue2 = null;
            _currentDoc.CoverOffsetY = 0.0;

            if (_currentDoc.CoverImageElement != null)
            {
                _currentDoc.CoverImageElement.Visibility = Visibility.Collapsed;
            }
            _currentDoc.UpdateHeaderState();
            _currentDoc.IsModified = true;
        }

        // ==================== REPOSITION COVER FUNCTIONALITY ====================
        private void StartCoverReposition()
        {
            if (_currentDoc == null || _currentDoc.CoverType == "none") return;
            _isRepositioning = true;
            _repositionInitialOffset = _currentDoc.CoverOffsetY;

            if (_currentDoc.CoverHoverBar != null) _currentDoc.CoverHoverBar.Visibility = Visibility.Collapsed;
            if (_currentDoc.RepositionHintBanner != null) _currentDoc.RepositionHintBanner.Visibility = Visibility.Visible;
            if (_currentDoc.RepositionActionsBar != null) _currentDoc.RepositionActionsBar.Visibility = Visibility.Visible;
            if (_currentDoc.CoverBanner != null) _currentDoc.CoverBanner.Cursor = Cursors.SizeAll;
        }

        private void SaveCoverReposition()
        {
            if (_currentDoc == null) return;
            _isRepositioning = false;
            ExitRepositionUI();
            _currentDoc.IsModified = true;
        }

        private void CancelCoverReposition()
        {
            if (_currentDoc == null) return;
            _isRepositioning = false;
            _currentDoc.CoverOffsetY = _repositionInitialOffset;
            if (_currentDoc.CoverTransform != null)
            {
                _currentDoc.CoverTransform.Y = _repositionInitialOffset;
            }
            ExitRepositionUI();
        }

        private void ExitRepositionUI()
        {
            if (_currentDoc == null) return;
            if (_currentDoc.CoverHoverBar != null) _currentDoc.CoverHoverBar.Visibility = Visibility.Visible;
            if (_currentDoc.RepositionHintBanner != null) _currentDoc.RepositionHintBanner.Visibility = Visibility.Collapsed;
            if (_currentDoc.RepositionActionsBar != null) _currentDoc.RepositionActionsBar.Visibility = Visibility.Collapsed;
            if (_currentDoc.CoverBanner != null) _currentDoc.CoverBanner.Cursor = Cursors.Arrow;
        }

        // ==================== TAB & NOTION PAGE CREATION ====================
        public void AddNewTab(string title, string content = "", string filePath = null)
        {
            var doc = new TabDocument(title) { FilePath = filePath };

            // Page Scroller
            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = _brushCanvasBg,
                Visibility = Visibility.Collapsed
            };

            // Main Stack: Full-width Cover on top + Centered Content below!
            var pageMainStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // 0: Full-Width Cover Banner (Height 210px, matching Notion Anexo 1)
            var coverBanner = new Border
            {
                Height = 210,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Visibility = Visibility.Collapsed,
                ClipToBounds = true,
                Background = Brushes.Transparent
            };

            var coverInnerGrid = new Grid();

            // Cover Image Element for Drag & Reposition
            var coverImg = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed
            };
            var coverTransform = new TranslateTransform(0, 0);
            coverImg.RenderTransform = coverTransform;
            doc.CoverImageElement = coverImg;
            doc.CoverTransform = coverTransform;
            coverInnerGrid.Children.Add(coverImg);

            // Reposition Hint ("Arraste a imagem para reposicionar")
            var repositionHint = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(190, 15, 15, 15)),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(14, 6, 14, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            repositionHint.Child = new TextBlock
            {
                Text = "Arraste a imagem para reposicionar",
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.Medium
            };
            coverInnerGrid.Children.Add(repositionHint);
            doc.RepositionHintBanner = repositionHint;

            // Cover Hover Actions Pill Bar (Alterar | Reposicionar) - Anexo 1
            var coverHoverPill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 12, 16, 0),
                Opacity = 0.0
            };

            var pillStack = new StackPanel { Orientation = Orientation.Horizontal };

            var btnChangeCover = CreateCoverPillBtn("Alterar capa", (s, e) =>
            {
                if (_coverPickerPopup != null)
                {
                    _coverPickerPopup.PlacementTarget = (UIElement)s;
                    _coverPickerPopup.IsOpen = true;
                }
            });

            var btnReposition = CreateCoverPillBtn("Reposicionar", (s, e) => StartCoverReposition());

            pillStack.Children.Add(btnChangeCover);
            pillStack.Children.Add(new Separator { Width = 1, Height = 14, Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), Margin = new Thickness(2, 4, 2, 4) });
            pillStack.Children.Add(btnReposition);
            coverHoverPill.Child = pillStack;
            coverInnerGrid.Children.Add(coverHoverPill);
            doc.CoverHoverBar = pillStack;

            // Reposition Action Buttons (Salvar posição | Cancelar)
            var reposActionsBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 16, 12),
                Visibility = Visibility.Collapsed
            };
            var btnSavePos = CreateGlassPillBtn("Salvar posição", _brushBlueAccent, (s, e) => SaveCoverReposition());
            var btnCancelPos = CreateGlassPillBtn("Cancelar", new SolidColorBrush(Color.FromArgb(180, 40, 40, 40)), (s, e) => CancelCoverReposition());
            reposActionsBar.Children.Add(btnSavePos);
            reposActionsBar.Children.Add(btnCancelPos);
            coverInnerGrid.Children.Add(reposActionsBar);
            doc.RepositionActionsBar = reposActionsBar;

            // Mouse Drag handling for Reposition
            coverBanner.MouseLeftButtonDown += (s, e) =>
            {
                if (_isRepositioning)
                {
                    _repositionStartMouse = e.GetPosition(coverBanner);
                    _repositionInitialOffset = doc.CoverOffsetY;
                    coverBanner.CaptureMouse();
                }
            };
            coverBanner.MouseMove += (s, e) =>
            {
                if (_isRepositioning && coverBanner.IsMouseCaptured)
                {
                    var curPos = e.GetPosition(coverBanner);
                    double deltaY = curPos.Y - _repositionStartMouse.Y;
                    double newOffset = _repositionInitialOffset + deltaY;
                    newOffset = Math.Min(50, Math.Max(-180, newOffset)); // Bounded vertical drag
                    doc.CoverOffsetY = newOffset;
                    if (doc.CoverTransform != null)
                    {
                        doc.CoverTransform.Y = newOffset;
                    }
                }
            };
            coverBanner.MouseLeftButtonUp += (s, e) =>
            {
                if (coverBanner.IsMouseCaptured)
                {
                    coverBanner.ReleaseMouseCapture();
                }
            };

            coverBanner.MouseEnter += (s, e) => { if (!_isRepositioning) coverHoverPill.Opacity = 1.0; };
            coverBanner.MouseLeave += (s, e) => { if (!_isRepositioning) coverHoverPill.Opacity = 0.0; };

            coverBanner.Child = coverInnerGrid;
            pageMainStack.Children.Add(coverBanner);
            doc.CoverBanner = coverBanner;

            // Centered Page Content Container (MaxWidth = 860px)
            var contentContainer = new Grid
            {
                MaxWidth = 860,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 860,
                Margin = new Thickness(48, 0, 48, 120)
            };
            contentContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Header Area (Hover buttons & emoji)
            contentContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Title (watermarked)
            contentContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 2: Body Editor

            // 0: Header Area (Contextual Hover Buttons & Large Emoji Badge)
            var headerArea = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            headerArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            headerArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var actionsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8),
                Opacity = 0.0 // INVISIBLE AT REST! Revealed on header hover (Anexo 4)
            };

            var btnAddIcon = CreateContextualHeaderBtn("🙂 Adicionar ícone", (s, e) =>
            {
                if (_emojiPickerPopup != null)
                {
                    _emojiSearchBox.Text = "";
                    PopulateEmojiGrid(_allEmojis);
                    _emojiPickerPopup.PlacementTarget = (UIElement)s;
                    _emojiPickerPopup.IsOpen = true;
                }
            });

            var btnAddCover = CreateContextualHeaderBtn("🖼️ Adicionar capa", (s, e) =>
            {
                // When clicking "Adicionar capa", apply default gallery cover immediately (Anexo 1)
                ApplyGalleryCover(_coverItems[6]); // Sunset gradient by default!
            });

            actionsRow.Children.Add(btnAddIcon);
            actionsRow.Children.Add(btnAddCover);
            Grid.SetRow(actionsRow, 0);
            headerArea.Children.Add(actionsRow);

            doc.BtnAddIcon = btnAddIcon;
            doc.BtnAddCover = btnAddCover;
            doc.ActionsRow = actionsRow;

            headerArea.MouseEnter += (s, e) => actionsRow.Opacity = 1.0;
            headerArea.MouseLeave += (s, e) => actionsRow.Opacity = 0.0;

            // Large Emoji Badge (Overlaps cover when cover exists)
            var btnEmoji = new Button
            {
                FontSize = 48,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand,
                ToolTip = "Clique para trocar o ícone",
                Margin = new Thickness(0, 4, 0, 6),
                Visibility = Visibility.Collapsed,
                Focusable = false,
                FocusVisualStyle = null
            };
            ApplyCleanButtonStyle(btnEmoji);
            btnEmoji.Click += (s, e) =>
            {
                if (_emojiPickerPopup != null)
                {
                    _emojiSearchBox.Text = "";
                    PopulateEmojiGrid(_allEmojis);
                    _emojiPickerPopup.PlacementTarget = btnEmoji;
                    _emojiPickerPopup.IsOpen = true;
                }
            };
            Grid.SetRow(btnEmoji, 1);
            headerArea.Children.Add(btnEmoji);
            doc.EmojiButton = btnEmoji;

            Grid.SetRow(headerArea, 0);
            contentContainer.Children.Add(headerArea);
            doc.HeaderHoverArea = headerArea;

            // 1: Notion Large Title (with "Nova página" placeholder - Anexo 3)
            var titleGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };

            var titleWatermark = new TextBlock
            {
                Text = "Nova página",
                FontSize = 36,
                FontWeight = FontWeights.Bold,
                Foreground = _brushPlaceholder,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Visibility = string.IsNullOrEmpty(title) ? Visibility.Visible : Visibility.Collapsed
            };
            titleGrid.Children.Add(titleWatermark);
            doc.TitlePlaceholder = titleWatermark;

            var titleBox = new TextBox
            {
                Text = title,
                FontSize = 36,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                CaretBrush = Brushes.White,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleBox.TextChanged += (s, e) =>
            {
                doc.TitlePlaceholder.Visibility = string.IsNullOrEmpty(titleBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                doc.UpdateTitle(titleBox.Text, true);
                Title = (doc.IsModified ? "* " : "") + (string.IsNullOrEmpty(doc.Title) ? "Sem título" : doc.Title) + " - NotionPad C#";
            };
            titleGrid.Children.Add(titleBox);
            doc.TitleEditor = titleBox;

            Grid.SetRow(titleGrid, 1);
            contentContainer.Children.Add(titleGrid);

            // 2: Notion RichText Editor Body (with "Clique na barra de espaço..." placeholder - Anexo 3)
            var editorGrid = new Grid();

            var editorWatermark = new TextBlock
            {
                Text = "Clique na barra de espaço para ativar a IA ou \"/\" para acessar os comandos",
                FontSize = 15,
                Foreground = _brushPlaceholder,
                IsHitTestVisible = false,
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = string.IsNullOrEmpty(content) ? Visibility.Visible : Visibility.Collapsed
            };
            editorGrid.Children.Add(editorWatermark);
            doc.EditorPlaceholder = editorWatermark;

            doc.Language = SyntaxHighlighter.DetectLanguage(filePath, content);

            var rtb = new RichTextBox
            {
                Background = Brushes.Transparent,
                Foreground = _brushText,
                CaretBrush = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = _baseFontSize * _currentZoom,
                FontFamily = new FontFamily(_selectedFont),
                AcceptsReturn = true,
                AcceptsTab = true,
                Margin = new Thickness(0),
                IsInactiveSelectionHighlightEnabled = true
            };
            rtb.Document.PagePadding = new Thickness(0);

            if (!string.IsNullOrEmpty(content))
            {
                _isApplyingHighlighting = true;
                SyntaxHighlighter.ApplyHighlightingToDocument(rtb.Document, doc.Language, content);
                _isApplyingHighlighting = false;
            }
            else
            {
                rtb.Document.Blocks.Clear();
                rtb.Document.Blocks.Add(new Paragraph { Margin = new Thickness(0) });
            }

            rtb.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Space)
                {
                    HandleMarkdownShortcuts(rtb);
                }
            };

            rtb.SelectionChanged += (s, e) =>
            {
                UpdateStatus();
                HandleEditorSelectionChanged(rtb);
            };

            rtb.TextChanged += (s, e) =>
            {
                if (_isApplyingHighlighting) return;

                var tr = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                bool isEmpty = string.IsNullOrWhiteSpace(tr.Text);
                doc.EditorPlaceholder.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;

                if (!doc.IsModified)
                {
                    doc.UpdateTitle(doc.Title, true);
                }
                UpdateStatus();

                if (doc.Language != "Texto")
                {
                    bool wasPaste = false;
                    foreach (var change in e.Changes)
                    {
                        if (change.AddedLength > 80) { wasPaste = true; break; }
                    }

                    if (wasPaste)
                    {
                        _rehighlightFullDocPending = true;
                    }

                    _highlightDebounceTimer.Stop();
                    _highlightDebounceTimer.Start();
                }
            };

            rtb.PreviewMouseWheel += (s, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (e.Delta > 0) ChangeZoom(0.1);
                    else if (e.Delta < 0) ChangeZoom(-0.1);
                    e.Handled = true;
                }
            };

            editorGrid.Children.Add(rtb);
            Grid.SetRow(editorGrid, 2);
            contentContainer.Children.Add(editorGrid);
            doc.Editor = rtb;

            pageMainStack.Children.Add(contentContainer);

            scroller.Content = pageMainStack;
            doc.PageScroller = scroller;
            _editorContainer.Children.Add(scroller);

            // Tab Item
            var tabBorder = new Border
            {
                Height = 34,
                CornerRadius = new CornerRadius(7, 7, 0, 0),
                Background = _brushTabActive,
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(10, 0, 8, 0),
                Cursor = Cursors.Hand
            };

            var tabGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            FrameworkElement iconElem;
            if (_officialIconImage != null)
            {
                var img = new Image
                {
                    Source = _officialIconImage,
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand,
                    ToolTip = "Alternar barra de menus"
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                iconElem = img;
            }
            else
            {
                iconElem = new Border
                {
                    Width = 14,
                    Height = 15,
                    CornerRadius = new CornerRadius(2),
                    Background = new SolidColorBrush(Color.FromRgb(0, 140, 255)),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand,
                    ToolTip = "Alternar barra de menus"
                };
            }
            iconElem.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                ToggleMenuBar();
            };
            Grid.SetColumn(iconElem, 0);
            tabGrid.Children.Add(iconElem);

            var titleText = new TextBlock
            {
                Text = string.IsNullOrEmpty(title) ? "Sem título" : title,
                FontSize = 12,
                Foreground = _brushText,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                MaxWidth = 180,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            doc.TitleText = titleText;
            Grid.SetColumn(titleText, 1);
            tabGrid.Children.Add(titleText);

            var closeBtn = new Button
            {
                Content = "\uE8BB",
                FontFamily = _fontMdl2,
                FontSize = 8.5,
                Width = 20,
                Height = 20,
                Foreground = _brushSubtext,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null
            };
            ApplyCleanButtonStyle(closeBtn);
            closeBtn.Click += (s, e) =>
            {
                CloseTab(doc);
                e.Handled = true;
            };
            doc.CloseBtn = closeBtn;
            Grid.SetColumn(closeBtn, 2);
            tabGrid.Children.Add(closeBtn);

            tabBorder.Child = tabGrid;
            tabBorder.MouseLeftButtonDown += (s, e) => SelectTab(doc);
            tabBorder.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    CloseTab(doc);
                    e.Handled = true;
                }
            };

            doc.TabBorder = tabBorder;

            _documents.Add(doc);
            _tabsPanel.Children.Add(tabBorder);

            doc.UpdateHeaderState();
            SelectTab(doc);
            doc.UpdateTitle(title, false);
            rtb.Focus();
        }

        private Button CreateCoverPillBtn(string label, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Content = label,
                FontSize = 12,
                Foreground = _brushText,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 3, 8, 3),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null
            };
            ApplyCleanButtonStyle(btn);
            btn.Click += handler;
            return btn;
        }

        private Button CreateGlassPillBtn(string label, Brush bg, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Content = label,
                FontSize = 12,
                Foreground = Brushes.White,
                Background = bg,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null
            };
            ApplyCleanButtonStyle(btn);
            btn.Click += handler;
            return btn;
        }

        private Button CreateContextualHeaderBtn(string label, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Content = label,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 3, 6, 3),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null
            };
            ApplyCleanButtonStyle(btn);
            btn.Click += handler;
            return btn;
        }

        private void HandleMarkdownShortcuts(RichTextBox rtb)
        {
            try
            {
                var curParagraph = rtb.CaretPosition.Paragraph;
                if (curParagraph == null) return;

                var textRange = new TextRange(curParagraph.ContentStart, rtb.CaretPosition);
                string text = textRange.Text;

                if (text == "#")
                {
                    textRange.Text = "";
                    curParagraph.FontSize = 26;
                    curParagraph.FontWeight = FontWeights.Bold;
                }
                else if (text == "##")
                {
                    textRange.Text = "";
                    curParagraph.FontSize = 21;
                    curParagraph.FontWeight = FontWeights.Bold;
                }
                else if (text == "###")
                {
                    textRange.Text = "";
                    curParagraph.FontSize = 18;
                    curParagraph.FontWeight = FontWeights.SemiBold;
                }
                else if (text == "-" || text == "*")
                {
                    textRange.Text = "";
                    EditingCommands.ToggleBullets.Execute(null, rtb);
                }
            }
            catch { }
        }

        private void HandleEditorSelectionChanged(RichTextBox rtb)
        {
            if (_floatingToolbar == null) return;

            if (!rtb.Selection.IsEmpty && !string.IsNullOrWhiteSpace(rtb.Selection.Text))
            {
                try
                {
                    var startRect = rtb.Selection.Start.GetCharacterRect(LogicalDirection.Forward);
                    var screenPt = rtb.PointToScreen(new Point(startRect.Left, startRect.Top));

                    double x = Math.Max(20, screenPt.X - 30);
                    double y = Math.Max(20, screenPt.Y - 48);

                    _floatingToolbar.HorizontalOffset = x;
                    _floatingToolbar.VerticalOffset = y;
                    _floatingToolbar.IsOpen = true;
                }
                catch
                {
                    _floatingToolbar.IsOpen = false;
                }
            }
            else
            {
                _floatingToolbar.IsOpen = false;
                if (_blockTypePopup != null) _blockTypePopup.IsOpen = false;
            }
        }

        private void SelectTab(TabDocument doc)
        {
            if (doc == null) return;
            _currentDoc = doc;

            foreach (var d in _documents)
            {
                bool isSelected = (d == doc);
                d.TabBorder.Background = isSelected ? _brushTabActive : _brushTabInactive;
                d.TitleText.Foreground = isSelected ? _brushText : _brushSubtext;
                d.PageScroller.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
            }

            string displayTitle = string.IsNullOrEmpty(doc.Title) ? "Sem título" : doc.Title;
            Title = (doc.IsModified ? "* " : "") + displayTitle + " - NotionPad C#";
            if (_statusLanguageBtn != null)
            {
                string lang = string.IsNullOrEmpty(doc.Language) ? "Texto" : doc.Language;
                _statusLanguageBtn.Content = (lang == "Texto" ? "Texto" : lang) + " \u25BE";
            }
            if (_findBar != null && _findBar.Visibility == Visibility.Visible)
            {
                UpdateFindSearch(false);
            }
            UpdateStatus();
            doc.Editor.Focus();
        }

        private void CloseTab(TabDocument doc)
        {
            if (doc == null) return;

            if (doc.IsModified)
            {
                string displayTitle = string.IsNullOrEmpty(doc.Title) ? "Sem título" : doc.Title;
                var res = MessageBox.Show(string.Format("Deseja salvar as alterações em '{0}'?", displayTitle),
                    "NotionPad", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (res == MessageBoxResult.Cancel) return;
                if (res == MessageBoxResult.Yes)
                {
                    if (!SaveFile(doc, false)) return;
                }
            }

            int idx = _documents.IndexOf(doc);
            _documents.Remove(doc);
            _tabsPanel.Children.Remove(doc.TabBorder);
            _editorContainer.Children.Remove(doc.PageScroller);

            if (_documents.Count > 0)
            {
                int nextIdx = Math.Min(idx, _documents.Count - 1);
                SelectTab(_documents[nextIdx]);
            }
            else
            {
                AddNewTab("");
            }
        }

        private void OpenFileDialog()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Todos os arquivos de texto e código|*.txt;*.md;*.html;*.htm;*.css;*.js;*.json;*.xml;*.py;*.cs;*.sql;*.log|Documento de Texto (*.txt)|*.txt|Documento Markdown (*.md)|*.md|Páginas HTML (*.html;*.htm)|*.html;*.htm|Folhas de Estilo CSS (*.css)|*.css|JavaScript (*.js)|*.js|Arquivos JSON (*.json)|*.json|Arquivos XML (*.xml)|*.xml|Scripts Python (*.py)|*.py|Código C# (*.cs)|*.cs|Scripts SQL (*.sql)|*.sql|Todos os arquivos (*.*)|*.*",
                Title = "Abrir"
            };
            if (dlg.ShowDialog() == true)
            {
                OpenFileInTab(dlg.FileName);
            }
        }

        private void OpenFileInTab(string filePath)
        {
            try
            {
                string title = Path.GetFileNameWithoutExtension(filePath);
                string ext = Path.GetExtension(filePath).ToLowerInvariant();

                string metaTitle = title;
                string metaIcon = "";
                string metaCoverType = "none";
                string metaC1 = "";
                string metaC2 = "";
                double metaOffsetY = 0.0;

                string text = File.ReadAllText(filePath, Encoding.UTF8);
                bool hadInlineTag = false;

                // 1. If file has a legacy inline tag from before, clean it up!
                if (text.StartsWith("<!-- notionpad:") || text.StartsWith("<!--notionpad:"))
                {
                    int endTag = text.IndexOf("-->");
                    if (endTag != -1)
                    {
                        hadInlineTag = true;
                        string header = text.Substring(0, endTag);
                        int colon = header.IndexOf(':');
                        if (colon != -1)
                        {
                            string metaContent = header.Substring(colon + 1);
                            string[] parts = metaContent.Split(';');
                            foreach (string part in parts)
                            {
                                int eq = part.IndexOf('=');
                                if (eq != -1)
                                {
                                    string key = part.Substring(0, eq).Trim().ToLowerInvariant();
                                    string val = part.Substring(eq + 1).Trim();
                                    if (key == "title" && !string.IsNullOrEmpty(val)) metaTitle = Uri.UnescapeDataString(val);
                                    else if (key == "icon") metaIcon = Uri.UnescapeDataString(val);
                                    else if (key == "covertype") metaCoverType = val;
                                    else if (key == "c1") metaC1 = Uri.UnescapeDataString(val);
                                    else if (key == "c2") metaC2 = Uri.UnescapeDataString(val);
                                    else if (key == "offset") double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out metaOffsetY);
                                }
                            }
                        }
                        text = text.Substring(endTag + 3);
                        if (text.StartsWith("\r\n")) text = text.Substring(2);
                        else if (text.StartsWith("\n")) text = text.Substring(1);

                        // Store into local metadata database
                        var imported = new DocMetadata
                        {
                            Title = metaTitle,
                            Icon = metaIcon,
                            CoverType = metaCoverType,
                            CoverValue1 = metaC1,
                            CoverValue2 = metaC2,
                            CoverOffsetY = metaOffsetY
                        };
                        MetadataStore.SaveMetadata(filePath, imported);

                        // Auto-clean the file on disk so other apps see 100% clean text!
                        try
                        {
                            File.WriteAllText(filePath, text, Encoding.UTF8);
                        }
                        catch { }
                    }
                }

                // 2. If no inline tag, lookup invisibly in local MetadataStore!
                if (!hadInlineTag)
                {
                    var stored = MetadataStore.GetMetadata(filePath);
                    if (stored != null)
                    {
                        if (!string.IsNullOrEmpty(stored.Title)) metaTitle = stored.Title;
                        metaIcon = stored.Icon;
                        metaCoverType = stored.CoverType;
                        metaC1 = stored.CoverValue1;
                        metaC2 = stored.CoverValue2;
                        metaOffsetY = stored.CoverOffsetY;
                    }
                }

                AddNewTab(metaTitle, text, filePath);
                ApplyLoadedMetadata(_currentDoc, metaTitle, metaIcon, metaCoverType, metaC1, metaC2, metaOffsetY);
                _currentDoc.IsModified = false;
                _currentDoc.UpdateTitle(metaTitle, false);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao abrir arquivo: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyLoadedMetadata(TabDocument doc, string title, string icon, string coverType, string c1, string c2, double offsetY)
        {
            if (doc == null) return;

            if (!string.IsNullOrEmpty(title))
            {
                doc.Title = title;
                if (doc.TitleEditor != null)
                {
                    doc.TitleEditor.Text = title;
                }
                if (doc.TitlePlaceholder != null)
                {
                    doc.TitlePlaceholder.Visibility = Visibility.Collapsed;
                }
            }

            if (!string.IsNullOrEmpty(icon))
            {
                doc.EmojiIcon = icon;
            }

            if (!string.IsNullOrEmpty(coverType) && coverType != "none")
            {
                doc.CoverType = coverType;
                doc.CoverValue1 = c1;
                doc.CoverValue2 = c2;
                doc.CoverOffsetY = offsetY;

                if (coverType == "solid" && !string.IsNullOrEmpty(c1))
                {
                    try
                    {
                        var col = (Color)ColorConverter.ConvertFromString(c1);
                        if (doc.CoverBanner != null) doc.CoverBanner.Background = new SolidColorBrush(col);
                        if (doc.CoverImageElement != null) doc.CoverImageElement.Visibility = Visibility.Collapsed;
                    }
                    catch { }
                }
                else if (coverType == "gradient" && !string.IsNullOrEmpty(c1))
                {
                    try
                    {
                        var col1 = (Color)ColorConverter.ConvertFromString(c1);
                        var col2 = string.IsNullOrEmpty(c2) ? col1 : (Color)ColorConverter.ConvertFromString(c2);
                        var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                        grad.GradientStops.Add(new GradientStop(col1, 0.0));
                        grad.GradientStops.Add(new GradientStop(col2, 1.0));
                        if (doc.CoverBanner != null) doc.CoverBanner.Background = grad;
                        if (doc.CoverImageElement != null) doc.CoverImageElement.Visibility = Visibility.Collapsed;
                    }
                    catch { }
                }
                else if ((coverType == "image" || coverType == "url") && !string.IsNullOrEmpty(c1))
                {
                    try
                    {
                        var bmp = new BitmapImage(new Uri(c1, UriKind.RelativeOrAbsolute));
                        if (doc.CoverImageElement != null)
                        {
                            doc.CoverImageElement.Source = bmp;
                            doc.CoverImageElement.Visibility = Visibility.Visible;
                            if (doc.CoverTransform != null) doc.CoverTransform.Y = offsetY;
                        }
                        if (doc.CoverBanner != null) doc.CoverBanner.Background = Brushes.Black;
                    }
                    catch { }
                }
            }

            doc.UpdateHeaderState();
            doc.IsModified = false;
            doc.UpdateTitle(doc.Title, false);
        }

        private bool SaveCurrentFile(bool saveAs)
        {
            if (_currentDoc == null) return false;
            return SaveFile(_currentDoc, saveAs);
        }

        private bool SaveFile(TabDocument doc, bool saveAs)
        {
            if (string.IsNullOrEmpty(doc.FilePath) || saveAs)
            {
                int filterIndex = 1;
                string defExt = ".txt";

                if (doc.Language == "Markdown") { filterIndex = 2; defExt = ".md"; }
                else if (doc.Language == "HTML") { filterIndex = 3; defExt = ".html"; }
                else if (doc.Language == "CSS") { filterIndex = 4; defExt = ".css"; }
                else if (doc.Language == "JavaScript") { filterIndex = 5; defExt = ".js"; }
                else if (doc.Language == "JSON") { filterIndex = 6; defExt = ".json"; }
                else if (doc.Language == "XML") { filterIndex = 7; defExt = ".xml"; }
                else if (doc.Language == "Python") { filterIndex = 8; defExt = ".py"; }
                else if (doc.Language == "C#") { filterIndex = 9; defExt = ".cs"; }
                else if (doc.Language == "SQL") { filterIndex = 10; defExt = ".sql"; }

                var dlg = new SaveFileDialog
                {
                    Filter = "Documento de Texto (*.txt)|*.txt|Documento Markdown (*.md)|*.md|Páginas HTML (*.html;*.htm)|*.html;*.htm|Folhas de Estilo CSS (*.css)|*.css|JavaScript (*.js)|*.js|Arquivos JSON (*.json)|*.json|Arquivos XML (*.xml)|*.xml|Scripts Python (*.py)|*.py|Código C# (*.cs)|*.cs|Scripts SQL (*.sql)|*.sql|Todos os arquivos (*.*)|*.*",
                    FilterIndex = filterIndex,
                    DefaultExt = defExt,
                    FileName = string.IsNullOrEmpty(doc.Title) ? "Sem título" : doc.Title
                };
                if (dlg.ShowDialog() == true)
                {
                    doc.FilePath = dlg.FileName;
                    doc.Title = Path.GetFileNameWithoutExtension(dlg.FileName);
                    doc.Language = SyntaxHighlighter.DetectLanguage(dlg.FileName, "");
                }
                else
                {
                    return false;
                }
            }

            try
            {
                var range = new TextRange(doc.Editor.Document.ContentStart, doc.Editor.Document.ContentEnd);

                // 100% PURE CLEAN TEXT (.txt / .md) - ZERO TAGS in the file!
                File.WriteAllText(doc.FilePath, range.Text, Encoding.UTF8);

                // Save visual metadata invisibly in local app database
                var meta = new DocMetadata
                {
                    Title = doc.Title,
                    Icon = doc.EmojiIcon,
                    CoverType = doc.CoverType ?? "none",
                    CoverValue1 = doc.CoverValue1 ?? "",
                    CoverValue2 = doc.CoverValue2 ?? "",
                    CoverOffsetY = doc.CoverOffsetY
                };
                MetadataStore.SaveMetadata(doc.FilePath, meta);

                doc.UpdateTitle(doc.Title, false);
                Title = doc.Title + " - NotionPad C#";
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar arquivo: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void ShowFindBar(bool showReplace)
        {
            _findBar.Visibility = Visibility.Visible;
            _replaceRow.Visibility = showReplace ? Visibility.Visible : Visibility.Collapsed;

            if (_currentDoc != null && _currentDoc.Editor != null && !_currentDoc.Editor.Selection.IsEmpty)
            {
                string sel = _currentDoc.Editor.Selection.Text.Trim();
                if (!string.IsNullOrEmpty(sel) && !sel.Contains("\n") && !sel.Contains("\r"))
                {
                    _findBox.Text = sel;
                }
            }

            _findBox.Focus();
            _findBox.SelectAll();
            UpdateFindSearch(true);
        }

        private void UpdateFindSearch(bool jumpToMatch)
        {
            if (_currentDoc == null || _currentDoc.Editor == null) return;
            string query = _findBox.Text;
            if (string.IsNullOrEmpty(query))
            {
                _currentMatches.Clear();
                _currentMatchIndex = -1;
                _findCountText.Text = "";
                return;
            }

            bool matchCase = _matchCaseBox.IsChecked == true;
            _currentMatches = SyntaxHighlighter.FindAllMatches(_currentDoc.Editor, query, matchCase);

            if (_currentMatches.Count == 0)
            {
                _currentMatchIndex = -1;
                _findCountText.Text = "Nenhum resultado";
                _findCountText.Foreground = new SolidColorBrush(Color.FromRgb(244, 135, 113));
                return;
            }

            _findCountText.Foreground = _brushSubtext;
            _currentMatchIndex = 0;
            TextPointer caret = _currentDoc.Editor.CaretPosition;
            for (int i = 0; i < _currentMatches.Count; i++)
            {
                if (_currentMatches[i].Start.CompareTo(caret) >= 0)
                {
                    _currentMatchIndex = i;
                    break;
                }
            }

            _findCountText.Text = string.Format("{0} de {1}", _currentMatchIndex + 1, _currentMatches.Count);

            if (jumpToMatch)
            {
                HighlightAndScrollToMatch(_currentMatchIndex);
            }
        }

        private void HighlightAndScrollToMatch(int index)
        {
            if (_currentDoc == null || _currentDoc.Editor == null || index < 0 || index >= _currentMatches.Count) return;
            var m = _currentMatches[index];
            _currentDoc.Editor.Selection.Select(m.Start, m.End);
            if (m.Start.Paragraph != null)
            {
                m.Start.Paragraph.BringIntoView();
            }
        }

        private void FindNext(bool forward)
        {
            if (_currentDoc == null || _currentDoc.Editor == null || string.IsNullOrEmpty(_findBox.Text)) return;

            if (_currentMatches.Count == 0)
            {
                UpdateFindSearch(true);
                return;
            }

            if (forward)
            {
                _currentMatchIndex = (_currentMatchIndex + 1) % _currentMatches.Count;
            }
            else
            {
                _currentMatchIndex = (_currentMatchIndex - 1 + _currentMatches.Count) % _currentMatches.Count;
            }

            HighlightAndScrollToMatch(_currentMatchIndex);
            _findCountText.Text = string.Format("{0} de {1}", _currentMatchIndex + 1, _currentMatches.Count);
        }

        private void ReplaceOne()
        {
            if (_currentDoc == null || _currentDoc.Editor == null || string.IsNullOrEmpty(_findBox.Text)) return;
            string target = _findBox.Text;
            string replacement = _replaceBox.Text ?? "";
            var sel = _currentDoc.Editor.Selection;
            bool matchCase = _matchCaseBox.IsChecked == true;
            var comp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            if (!sel.IsEmpty && sel.Text.Equals(target, comp))
            {
                sel.Text = replacement;
                _currentDoc.IsModified = true;
                _currentDoc.UpdateTitle(_currentDoc.Title, true);
            }
            FindNext(true);
        }

        private void ReplaceAll()
        {
            if (_currentDoc == null || _currentDoc.Editor == null || string.IsNullOrEmpty(_findBox.Text)) return;
            string target = _findBox.Text;
            string replacement = _replaceBox.Text ?? "";
            bool matchCase = _matchCaseBox.IsChecked == true;
            var rtb = _currentDoc.Editor;

            var matches = SyntaxHighlighter.FindAllMatches(rtb, target, matchCase);
            if (matches.Count == 0)
            {
                _findCountText.Text = "0 substituídos";
                return;
            }

            int count = matches.Count;
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var tr = new TextRange(matches[i].Start, matches[i].End);
                tr.Text = replacement;
            }
            _currentDoc.IsModified = true;
            _currentDoc.UpdateTitle(_currentDoc.Title, true);
            _findCountText.Text = string.Format("{0} substituídos", count);
            _currentMatches.Clear();
            _currentMatchIndex = -1;
        }

        private void ShowLanguageMenu()
        {
            if (_currentDoc == null || _statusLanguageBtn == null) return;

            var menu = new ContextMenu
            {
                Background = _brushFloatingBg,
                Foreground = _brushText,
                BorderBrush = _brushBorder,
                BorderThickness = new Thickness(1)
            };

            string[] languages = new string[]
            {
                "HTML",
                "JavaScript",
                "CSS",
                "JSON",
                "XML",
                "Python",
                "C#",
                "SQL",
                "Markdown",
                "Texto"
            };

            foreach (string lang in languages)
            {
                string currentLang = lang;
                var item = new MenuItem
                {
                    Header = (currentLang == "Texto" ? "Texto sem formatação" : currentLang) + (_currentDoc.Language == currentLang ? "  ✓" : ""),
                    Foreground = _brushText,
                    Background = Brushes.Transparent
                };
                item.Click += (s, e) =>
                {
                    SetDocumentLanguage(_currentDoc, currentLang);
                };
                menu.Items.Add(item);
            }

            menu.PlacementTarget = _statusLanguageBtn;
            menu.Placement = PlacementMode.Top;
            menu.IsOpen = true;
        }

        private void SetDocumentLanguage(TabDocument doc, string language)
        {
            if (doc == null || doc.Editor == null) return;
            doc.Language = language;
            if (_statusLanguageBtn != null && doc == _currentDoc)
            {
                _statusLanguageBtn.Content = (language == "Texto" ? "Texto" : language) + " \u25BE";
            }

            var range = new TextRange(doc.Editor.Document.ContentStart, doc.Editor.Document.ContentEnd);
            string text = range.Text;
            _isApplyingHighlighting = true;
            SyntaxHighlighter.ApplyHighlightingToDocument(doc.Editor.Document, doc.Language, text);
            _isApplyingHighlighting = false;
        }

        private void ShowSettingsDialog()
        {
            var dlg = new Window
            {
                Title = "Configurações - NotionPad C#",
                Width = 380,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = _brushMenuBarBg,
                Foreground = _brushText
            };

            var stack = new StackPanel { Margin = new Thickness(18) };

            stack.Children.Add(new TextBlock
            {
                Text = "Aparência e Fonte",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 14),
                Foreground = _brushText
            });

            stack.Children.Add(new TextBlock { Text = "Família da Fonte:", Margin = new Thickness(0, 0, 0, 6), Foreground = _brushSubtext });
            var fontCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 16), Height = 26 };
            fontCombo.Items.Add("Segoe UI");
            fontCombo.Items.Add("Consolas");
            fontCombo.Items.Add("Cascadia Code");
            fontCombo.Items.Add("Cascadia Mono");
            fontCombo.Items.Add("Courier New");
            fontCombo.SelectedItem = _selectedFont;
            stack.Children.Add(fontCombo);

            stack.Children.Add(new TextBlock { Text = "Tamanho da Fonte:", Margin = new Thickness(0, 0, 0, 6), Foreground = _brushSubtext });
            var sizeCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 20), Height = 26 };
            sizeCombo.Items.Add("12");
            sizeCombo.Items.Add("14");
            sizeCombo.Items.Add("15");
            sizeCombo.Items.Add("16");
            sizeCombo.Items.Add("18");
            sizeCombo.Items.Add("20");
            sizeCombo.SelectedItem = ((int)_baseFontSize).ToString();
            stack.Children.Add(sizeCombo);

            var btnSave = new Button
            {
                Content = "Salvar alterações",
                Width = 140,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = _brushBlueAccent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null
            };
            ApplyCleanButtonStyle(btnSave);
            btnSave.Click += (s, e) =>
            {
                if (fontCombo.SelectedItem != null) _selectedFont = fontCombo.SelectedItem.ToString();
                double newSize;
                if (sizeCombo.SelectedItem != null && double.TryParse(sizeCombo.SelectedItem.ToString(), out newSize))
                {
                    _baseFontSize = newSize;
                }

                foreach (var d in _documents)
                {
                    if (d.Editor != null)
                    {
                        d.Editor.FontFamily = new FontFamily(_selectedFont);
                        d.Editor.FontSize = _baseFontSize * _currentZoom;
                    }
                }
                dlg.Close();
            };
            stack.Children.Add(btnSave);

            dlg.Content = stack;
            dlg.ShowDialog();
        }

        private void InsertDateTime()
        {
            if (_currentDoc != null && _currentDoc.Editor != null)
            {
                _currentDoc.Editor.Selection.Text = DateTime.Now.ToString("HH:mm dd/MM/yyyy");
            }
        }

        private void ChangeZoom(double delta)
        {
            SetZoom(Math.Max(0.5, Math.Min(3.0, _currentZoom + delta)));
        }

        private void SetZoom(double zoom)
        {
            _currentZoom = zoom;
            foreach (var doc in _documents)
            {
                if (doc.Editor != null) doc.Editor.FontSize = _baseFontSize * _currentZoom;
            }
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (_currentDoc == null || _currentDoc.Editor == null) return;

            var rtb = _currentDoc.Editor;
            try
            {
                TextPointer caret = rtb.CaretPosition;
                int line = 1;
                TextPointer lineStart = caret.GetLineStartPosition(0);
                if (lineStart != null)
                {
                    TextPointer ptr = lineStart;
                    int count = 0;
                    while ((ptr = ptr.GetLineStartPosition(-1)) != null && count < 20000)
                    {
                        line++;
                        count++;
                    }
                    int col = new TextRange(lineStart, caret).Text.Length + 1;
                    _statusLineCol.Text = string.Format("Ln {0}, Col {1}", line, col);
                }
                else
                {
                    _statusLineCol.Text = "Ln 1, Col 1";
                }
            }
            catch
            {
                _statusLineCol.Text = "Ln 1, Col 1";
            }

            var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
            int chars = range.Text.Replace("\r\n", "").Length;

            _statusChars.Text = string.Format("{0} caracteres", chars);
            _statusZoom.Text = string.Format("{0}%", (int)(_currentZoom * 100));

            if (_statusLanguageBtn != null)
            {
                string lang = string.IsNullOrEmpty(_currentDoc.Language) ? "Texto" : _currentDoc.Language;
                _statusLanguageBtn.Content = (lang == "Texto" ? "Texto" : lang) + " \u25BE";
            }
        }

        private void OpenNewWindow()
        {
            var win = new MainWindow(new string[0]);
            win.Show();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.T) { AddNewTab(""); e.Handled = true; }
                else if (e.Key == Key.W) { CloseTab(_currentDoc); e.Handled = true; }
                else if (e.Key == Key.O) { OpenFileDialog(); e.Handled = true; }
                else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { SaveCurrentFile(true); e.Handled = true; }
                else if (e.Key == Key.S) { SaveCurrentFile(false); e.Handled = true; }
                else if (e.Key == Key.N) { OpenNewWindow(); e.Handled = true; }
                else if (e.Key == Key.F) { ShowFindBar(false); e.Handled = true; }
                else if (e.Key == Key.H) { ShowFindBar(true); e.Handled = true; }
                else if (e.Key == Key.Tab)
                {
                    if (_documents.Count > 1 && _currentDoc != null)
                    {
                        int curIdx = _documents.IndexOf(_currentDoc);
                        int nextIdx = (curIdx + 1) % _documents.Count;
                        SelectTab(_documents[nextIdx]);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.OemPlus || e.Key == Key.Add) { ChangeZoom(0.1); e.Handled = true; }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) { ChangeZoom(-0.1); e.Handled = true; }
                else if (e.Key == Key.D0 || e.Key == Key.NumPad0) { SetZoom(1.0); e.Handled = true; }
                else if (e.Key == Key.M) { ToggleMenuBar(); e.Handled = true; }
            }
            else if (e.Key == Key.F3)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) FindNext(false);
                else FindNext(true);
                e.Handled = true;
            }
            else if (e.Key == Key.F10)
            {
                ToggleMenuBar();
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                InsertDateTime();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_isRepositioning)
                {
                    CancelCoverReposition();
                    e.Handled = true;
                    return;
                }
                if (_findBar.Visibility == Visibility.Visible)
                {
                    _findBar.Visibility = Visibility.Collapsed;
                    _currentMatches.Clear();
                    _currentMatchIndex = -1;
                    if (_currentDoc != null) _currentDoc.Editor.Focus();
                    e.Handled = true;
                }
                if (_floatingToolbar != null && _floatingToolbar.IsOpen) _floatingToolbar.IsOpen = false;
                if (_emojiPickerPopup != null && _emojiPickerPopup.IsOpen) _emojiPickerPopup.IsOpen = false;
                if (_coverPickerPopup != null && _coverPickerPopup.IsOpen) _coverPickerPopup.IsOpen = false;
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (var doc in _documents.ToArray())
            {
                if (doc.IsModified)
                {
                    string displayTitle = string.IsNullOrEmpty(doc.Title) ? "Sem título" : doc.Title;
                    var res = MessageBox.Show(string.Format("Deseja salvar as alterações em '{0}'?", displayTitle),
                        "NotionPad", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                    if (res == MessageBoxResult.Cancel) { e.Cancel = true; return; }
                    if (res == MessageBoxResult.Yes)
                    {
                        if (!SaveFile(doc, false)) { e.Cancel = true; return; }
                    }
                }
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            var app = new Application();
            var window = new MainWindow(args);
            app.Run(window);
        }
    }
}
