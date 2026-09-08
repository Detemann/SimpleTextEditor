using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.IO;

namespace AED2
{
    /// <summary>
    /// Interação lógica para MainWindow.xam
    /// </summary>
    public partial class MainWindow : Window
    {
        private string sampleOpenFolder = @"C:\";

        public string fileDialogName = "";
        public string[] readtext = new string[1000];

        private SpellCheckHighlighter highlighter;

        public MainWindow()
        {
            InitializeComponent();
            loadDictionary();
            highlighter = new SpellCheckHighlighter(fileSpaceBox);
        }

        private void openFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new OpenFileDialog();
            fileDialog.InitialDirectory = sampleOpenFolder;
            fileDialog.DefaultExt = "txt";
            fileDialog.Filter = "(*.txt)|";
            fileDialog.Multiselect = false;
            fileDialog.ShowDialog();
            fileDialogName = fileDialog.FileName;

            if (fileDialogName != "")
            {
                fileNameBlock.Text = fileDialogName;
                readtext = File.ReadAllLines(fileDialogName);

                highlighter.SetText(string.Join("\n", readtext));
            }
        }

        private void saveFileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (fileDialogName != "")
            {
                File.WriteAllText(fileDialogName, highlighter.GetText());
            }
            else
            {
                var fileDialog = new SaveFileDialog();
                fileDialog.InitialDirectory = sampleOpenFolder;
                fileDialog.DefaultExt = "txt";
                fileDialog.Filter = "Arquivos de texto (*.txt)|*.txt";
                var result = fileDialog.ShowDialog();

                fileDialogName = fileDialog.FileName;
                File.WriteAllText(fileDialogName, highlighter.GetText());
            }
        }

        private void loadDictionary()
        {
            string filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Palvras em Portugues.txt");

            try
            {
                int total = AutocorrectEngine.LoadDictionary(filePath);
                Console.WriteLine("Total de palavras no dicionário: " + total);
            }
            catch (Exception e)
            {
                Console.WriteLine("Ocorreu um erro ao ler o arquivo:");
                Console.WriteLine(e.Message);
            }
        }
    }
}
