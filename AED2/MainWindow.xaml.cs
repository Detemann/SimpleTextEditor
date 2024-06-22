using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace AED2
{
    public partial class MainWindow : Window
    {
        private string sampleOpenFolder = @"C:\";
        private string fileDialogName = "";
        private HashSet<string> dictionary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public MainWindow()
        {
            InitializeComponent();
            LoadDictionary();
        }

        private void LoadDictionary()
        {
            string filePath = "D:\\Cursos\\Csharp\\AED2\\AED2\\Palavras em Portugues.txt";

            if (File.Exists(filePath))
            {
                foreach (var word in File.ReadAllLines(filePath))
                {
                    dictionary.Add(word.Trim());
                }
            }
            else
            {
                MessageBox.Show("O arquivo 'Palavras em Portugues.txt' não foi encontrado.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void openFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new OpenFileDialog
            {
                InitialDirectory = sampleOpenFolder,
                DefaultExt = "txt",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                Multiselect = false
            };

            if (fileDialog.ShowDialog() == true)
            {
                fileDialogName = fileDialog.FileName;
                fileNameBlock.Text = fileDialogName;
                fileSpaceBox.Document.Blocks.Clear();
                string fileContent = File.ReadAllText(fileDialogName);
                fileSpaceBox.Document.Blocks.Add(new Paragraph(new Run(fileContent)));
                HighlightText();
            }
        }

        private void saveFileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(fileDialogName))
            {
                var fileDialog = new SaveFileDialog
                {
                    InitialDirectory = sampleOpenFolder,
                    DefaultExt = "txt",
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
                };

                if (fileDialog.ShowDialog() == true)
                {
                    fileDialogName = fileDialog.FileName;
                }
            }

            if (!string.IsNullOrEmpty(fileDialogName))
            {
                File.WriteAllText(fileDialogName, new TextRange(fileSpaceBox.Document.ContentStart, fileSpaceBox.Document.ContentEnd).Text);
            }
        }

        private void fileSpaceBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var caretPosition = fileSpaceBox.CaretPosition;
                fileSpaceBox.CaretPosition.InsertTextInRun("\n");
                HighlightText();
                e.Handled = true;
            }
        }

        private void HighlightText()
        {
            TextRange textRange = new TextRange(fileSpaceBox.Document.ContentStart, fileSpaceBox.Document.ContentEnd);
            textRange.ClearAllProperties();

            var text = textRange.Text;

            foreach (var word in text.Split(new[] { ' ', '\n', '\r', '.', ',', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var start = fileSpaceBox.Document.ContentStart;
                while (start != null && start.CompareTo(fileSpaceBox.Document.ContentEnd) < 0)
                {
                    if (start.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                    {
                        var wordStart = start.GetTextInRun(LogicalDirection.Forward).IndexOf(word, StringComparison.OrdinalIgnoreCase);
                        if (wordStart != -1)
                        {
                            var startPointer = start.GetPositionAtOffset(wordStart);
                            var endPointer = startPointer.GetPositionAtOffset(word.Length);

                            var range = new TextRange(startPointer, endPointer);
                            if (!dictionary.Contains(word))
                            {
                                // Aplicando sublinhado em palavras não reconhecidas
                                range.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
                            }
                        }
                    }
                    start = start.GetNextContextPosition(LogicalDirection.Forward);
                }
            }
        }

        private void fileSpaceBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TextPointer position = fileSpaceBox.GetPositionFromPoint(e.GetPosition(fileSpaceBox), true);
            if (position != null)
            {
                TextPointer wordStart = position.GetPositionAtOffset(0, LogicalDirection.Backward);
                TextPointer wordEnd = position.GetPositionAtOffset(0, LogicalDirection.Forward);

                while (wordStart != null && !wordStart.IsAtInsertionPosition)
                {
                    wordStart = wordStart.GetNextInsertionPosition(LogicalDirection.Backward);
                }

                while (wordEnd != null && !wordEnd.IsAtInsertionPosition)
                {
                    wordEnd = wordEnd.GetNextInsertionPosition(LogicalDirection.Forward);
                }

                TextRange wordRange = new TextRange(wordStart, wordEnd);

                // Verifica se a palavra está sublinhada
                if (wordRange.GetPropertyValue(Inline.TextDecorationsProperty) != DependencyProperty.UnsetValue)
                {
                    TextDecorationCollection decorations = (TextDecorationCollection)wordRange.GetPropertyValue(Inline.TextDecorationsProperty);
                    if (decorations != null && decorations.Count > 0 && decorations[0] == TextDecorations.Underline[0])
                    {
                        string word = wordRange.Text.Trim();
                        MessageBoxResult result = MessageBox.Show($"Deseja adicionar '{word}' ao dicionário?", "Adicionar Palavra", MessageBoxButton.YesNo);
                        if (result == MessageBoxResult.Yes)
                        {
                            dictionary.Add(word);
                            HighlightText();
                        }
                    }
                }
            }
        }

    }
}
