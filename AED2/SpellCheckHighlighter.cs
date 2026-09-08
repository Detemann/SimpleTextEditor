using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AED2
{
    public class SpellCheckHighlighter
    {
        private const int MaxDistance = 2;
        private const int MaxSuggestions = 7;

        private const int ShortWordLength = 3;

        private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);

        private static readonly Brush MarkBackground = CreateBrush(255, 243, 150);
        private static readonly Brush MarkForeground = CreateBrush(122, 78, 0);

        private readonly RichTextBox box;

        private readonly Dictionary<string, bool> markCache =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> ignored =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<Paragraph> pending = new HashSet<Paragraph>();

        private readonly DispatcherTimer idleTimer;

        private bool applyingChanges;

        public SpellCheckHighlighter(RichTextBox box)
        {
            this.box = box;
            this.box.TextChanged += OnTextChanged;
            this.box.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;

            idleTimer = new DispatcherTimer();
            idleTimer.Interval = IdleDelay;
            idleTimer.Tick += OnIdle;
        }

        private static Brush CreateBrush(byte r, byte g, byte b)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        public string GetText()
        {
            TextRange range = new TextRange(box.Document.ContentStart, box.Document.ContentEnd);
            string text = range.Text;

            if (text.EndsWith("\r\n")) text = text.Substring(0, text.Length - 2);
            return text;
        }

        public void SetText(string text)
        {
            FlowDocument document = new FlowDocument();
            document.FontFamily = box.FontFamily;
            document.FontSize = box.FontSize;

            string[] lines = (text ?? "").Replace("\r\n", "\n").Split('\n');

            foreach (string line in lines)
            {
                Paragraph paragraph = new Paragraph();
                paragraph.Margin = new Thickness(0);
                AppendCheckedRuns(paragraph, line);
                document.Blocks.Add(paragraph);
            }

            box.Document = document;
        }

        private void AppendCheckedRuns(Paragraph paragraph, string line)
        {
            StringBuilder plain = new StringBuilder();
            int i = 0;

            while (i < line.Length)
            {
                int start = i;

                if (IsWordChar(line[i]))
                {
                    while (i < line.Length && IsWordChar(line[i])) i++;
                    string word = line.Substring(start, i - start);

                    if (ShouldMark(word))
                    {
                        Flush(paragraph, plain);

                        Run run = new Run(word);
                        Mark(run);
                        paragraph.Inlines.Add(run);
                    }
                    else
                    {
                        plain.Append(word);
                    }
                }
                else
                {
                    while (i < line.Length && !IsWordChar(line[i])) i++;
                    plain.Append(line, start, i - start);
                }
            }

            Flush(paragraph, plain);
        }

        private static void Flush(Paragraph paragraph, StringBuilder plain)
        {
            if (plain.Length == 0) return;

            paragraph.Inlines.Add(new Run(plain.ToString()));
            plain.Length = 0;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (applyingChanges) return;

            QueueChangedParagraphs(e);

            idleTimer.Stop();
            idleTimer.Start();
        }

        private void QueueChangedParagraphs(TextChangedEventArgs e)
        {
            TextPointer documentStart = box.Document.ContentStart;

            foreach (TextChange change in e.Changes)
            {
                TextPointer start = documentStart.GetPositionAtOffset(change.Offset);
                TextPointer end = documentStart.GetPositionAtOffset(change.Offset + change.AddedLength);

                if (start == null) start = box.Document.ContentStart;
                if (end == null) end = box.Document.ContentEnd;

                Paragraph first = start.Paragraph;
                Paragraph last = end.Paragraph;

                if (first == null) first = box.Document.Blocks.FirstBlock as Paragraph;
                if (last == null) last = box.Document.Blocks.LastBlock as Paragraph;

                if (first == null || last == null) continue;

                Paragraph current = first;
                while (current != null)
                {
                    pending.Add(current);
                    if (current == last) break;
                    current = current.NextBlock as Paragraph;
                }
            }

            if (pending.Count == 0 && box.CaretPosition != null && box.CaretPosition.Paragraph != null)
            {
                pending.Add(box.CaretPosition.Paragraph);
            }
        }

        private void OnIdle(object sender, EventArgs e)
        {
            idleTimer.Stop();

            if (!AutocorrectEngine.IsLoaded)
            {
                pending.Clear();
                return;
            }

            List<Paragraph> paragraphs = new List<Paragraph>(pending);
            pending.Clear();

            applyingChanges = true;
            try
            {
                foreach (Paragraph paragraph in paragraphs)
                {
                    if (paragraph.ContentStart == null) continue;
                    CheckParagraph(paragraph);
                }

                ResetTypingFormat();
            }
            finally
            {
                applyingChanges = false;
            }
        }

        private void CheckParagraph(Paragraph paragraph)
        {
            foreach (TextRange range in CollectWordRanges(paragraph))
            {
                string word = range.Text;
                if (word.Length < 1) continue;

                string accented;
                if (AutocorrectEngine.TryFixAccents(word, out accented))
                {
                    bool caretInside = box.CaretPosition != null
                        && range.Contains(box.CaretPosition);

                    range.Text = accented;
                    Unmark(range);

                    if (caretInside) box.CaretPosition = range.End;
                    continue;
                }

                bool marked = IsMarked(range);
                bool shouldMark = ShouldMark(word);

                if (shouldMark && !marked) Mark(range);
                else if (!shouldMark && marked) Unmark(range);
            }
        }

        private List<TextRange> CollectWordRanges(Paragraph paragraph)
        {
            List<TextRange> ranges = new List<TextRange>();

            TextPointer end = paragraph.ContentEnd;
            TextPointer cursor = paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            TextPointer wordStart = null;

            while (cursor != null && cursor.CompareTo(end) < 0)
            {
                TextPointer next = cursor.GetNextInsertionPosition(LogicalDirection.Forward);
                if (next == null) break;

                string step = new TextRange(cursor, next).Text;
                bool isWord = step.Length == 1 && IsWordChar(step[0]);

                if (isWord)
                {
                    if (wordStart == null) wordStart = cursor;
                }
                else if (wordStart != null)
                {
                    ranges.Add(new TextRange(wordStart, cursor));
                    wordStart = null;
                }

                cursor = next;
            }

            if (wordStart != null && cursor != null)
            {
                ranges.Add(new TextRange(wordStart, cursor));
            }

            return ranges;
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TextPointer position = box.GetPositionFromPoint(e.GetPosition(box), false);
            if (position == null) return;

            TextRange word = GetWordRange(position);
            if (word == null || word.IsEmpty) return;

            if (!IsMarked(word)) return;

            ShowSuggestions(word);
        }

        private static int DistanceFor(string word)
        {
            return word.Length <= ShortWordLength ? 1 : MaxDistance;
        }

        private bool ShouldMark(string word)
        {
            if (word.Length < 1) return false;
            if (!AutocorrectEngine.IsLoaded) return false;
            if (ignored.Contains(word)) return false;
            if (AutocorrectEngine.IsKnown(word)) return false;

            bool mark;
            if (markCache.TryGetValue(word, out mark)) return mark;

            mark = AutocorrectEngine.HasSuggestion(word, DistanceFor(word));
            markCache[word] = mark;
            return mark;
        }

        private void ShowSuggestions(TextRange range)
        {
            string word = range.Text;
            List<string> suggestions = AutocorrectEngine.Suggest(word, DistanceFor(word), MaxSuggestions);

            ContextMenu menu = new ContextMenu();

            foreach (string suggestion in suggestions)
            {
                MenuItem item = new MenuItem();
                item.Header = suggestion;
                item.FontWeight = FontWeights.SemiBold;

                string replacement = suggestion;
                item.Click += delegate { Replace(range, replacement); };

                menu.Items.Add(item);
            }

            if (suggestions.Count == 0)
            {
                MenuItem empty = new MenuItem();
                empty.Header = "(sem sugestões)";
                empty.IsEnabled = false;
                menu.Items.Add(empty);
            }

            menu.Items.Add(new Separator());

            MenuItem add = new MenuItem();
            add.Header = "Adicionar ao dicionário";
            add.Click += delegate
            {
                AutocorrectEngine.Add(word);
                markCache.Remove(word);
                Unmark(range);
            };
            menu.Items.Add(add);

            MenuItem ignore = new MenuItem();
            ignore.Header = "Ignorar";
            ignore.Click += delegate
            {
                ignored.Add(word);
                Unmark(range);
            };
            menu.Items.Add(ignore);

            menu.PlacementTarget = box;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        private void Replace(TextRange range, string replacement)
        {
            range.Text = replacement;
            Unmark(range);
            box.CaretPosition = range.End;
            box.Focus();
        }

        private void Mark(TextRange range)
        {
            range.ApplyPropertyValue(TextElement.BackgroundProperty, MarkBackground);
            range.ApplyPropertyValue(TextElement.ForegroundProperty, MarkForeground);
        }

        private void Mark(Run run)
        {
            run.Background = MarkBackground;
            run.Foreground = MarkForeground;
        }

        private void Unmark(TextRange range)
        {
            range.ApplyPropertyValue(TextElement.BackgroundProperty, null);
            range.ApplyPropertyValue(TextElement.ForegroundProperty, box.Foreground);
        }

        private bool IsMarked(TextRange range)
        {
            object background = range.GetPropertyValue(TextElement.BackgroundProperty);
            return ReferenceEquals(background, MarkBackground);
        }

        private void ResetTypingFormat()
        {
            TextSelection selection = box.Selection;
            if (selection == null || !selection.IsEmpty) return;

            selection.ApplyPropertyValue(TextElement.BackgroundProperty, null);
            selection.ApplyPropertyValue(TextElement.ForegroundProperty, box.Foreground);
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetter(c) || c == '\'' || c == '-';
        }

        private static TextRange GetWordRange(TextPointer position)
        {
            TextPointer insertion = position.GetInsertionPosition(LogicalDirection.Forward);
            if (insertion == null) return null;

            TextPointer start = WalkWord(insertion, LogicalDirection.Backward);
            TextPointer end = WalkWord(insertion, LogicalDirection.Forward);

            return start.CompareTo(end) == 0 ? null : new TextRange(start, end);
        }

        private static TextPointer WalkWord(TextPointer from, LogicalDirection direction)
        {
            TextPointer current = from;

            while (true)
            {
                TextPointer next = current.GetNextInsertionPosition(direction);
                if (next == null) break;

                TextRange step = direction == LogicalDirection.Backward
                    ? new TextRange(next, current)
                    : new TextRange(current, next);

                string text = step.Text;
                if (text.Length != 1 || !IsWordChar(text[0])) break;

                current = next;
            }

            return current;
        }
    }
}
