#nullable enable
using System.Windows;

namespace N_m3u8DL_RE_GUI.Views;

public enum FileConflictChoice
{
    Cancel,
    Rename,
    Overwrite
}

public partial class FileConflictWindow : Window
{
    public FileConflictChoice Choice { get; private set; } = FileConflictChoice.Cancel;

    public FileConflictWindow(string existingFiles)
    {
        InitializeComponent();
        Text_ExistingFile.Text = existingFiles;
    }

    private void Overwrite_Click(object sender, RoutedEventArgs e)
    {
        Choice = FileConflictChoice.Overwrite;
        DialogResult = true;
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        Choice = FileConflictChoice.Rename;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Choice = FileConflictChoice.Cancel;
        DialogResult = false;
    }
}