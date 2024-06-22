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

        public MainWindow()
        {
            InitializeComponent();
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

            if(fileDialogName != "")
            {
                fileNameBlock.Text = "";
                fileSpaceBox.Text = "";
                fileNameBlock.Text = fileDialogName;
                readtext = File.ReadAllLines(fileDialogName);

                for(int i = 0; i < readtext.Length; i++)
                {
                    fileSpaceBox.Text += readtext[i] + '\n';
                }

            }
            
        }

        private void saveFileBtn_Click(object sender, RoutedEventArgs e)
        {
            if(fileDialogName != "")
            {
                File.WriteAllText(fileDialogName, fileSpaceBox.Text);
            } else
            {
                var fileDialog = new SaveFileDialog();
                fileDialog.InitialDirectory = sampleOpenFolder;
                fileDialog.DefaultExt = "txt";
                fileDialog.Filter = "Arquivos de texto (*.txt)|*.txt"; // Corrigido o filtro
                var result = fileDialog.ShowDialog();

                fileDialogName = fileDialog.FileName;
                File.WriteAllText(fileDialogName, fileSpaceBox.Text);
        
            }
        }

        private void fileSpaceBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var caretIndex = fileSpaceBox.CaretIndex;
                fileSpaceBox.Text = fileSpaceBox.Text.Insert(caretIndex, "\n");
                fileSpaceBox.CaretIndex = caretIndex + 1;
            }
        }

    }
}
