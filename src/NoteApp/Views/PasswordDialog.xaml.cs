using System.Windows;

namespace NoteApp.Views;

public partial class PasswordDialog : Window
{
    public string Password => PasswordBox.Password;
    public bool IsSetMode { get; }

    public PasswordDialog(bool isSetMode)
    {
        InitializeComponent();
        IsSetMode = isSetMode;

        if (isSetMode)
        {
            TitleText.Text = "Set Password";
            PromptText.Text = "Choose a password to protect this note. You will need it to open the note later.";
        }
        else
        {
            ConfirmPasswordBox.Visibility = Visibility.Collapsed;
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PasswordBox.Password))
        {
            ShowError("Password cannot be empty.");
            return;
        }

        if (IsSetMode && PasswordBox.Password != ConfirmPasswordBox.Password)
        {
            ShowError("Passwords do not match.");
            return;
        }

        if (IsSetMode && PasswordBox.Password.Length < 4)
        {
            ShowError("Password must be at least 4 characters.");
            return;
        }

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
