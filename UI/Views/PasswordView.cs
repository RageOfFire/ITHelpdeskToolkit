using System;
using System.Drawing;
using System.Windows.Forms;
using ITHelpdeskToolkit.Services;
using ITHelpdeskToolkit.UI.Controls;
using ITHelpdeskToolkit.UI.Theme;

namespace ITHelpdeskToolkit.UI.Views
{
    public class PasswordView : UserControl
    {
        private readonly TextBox _txtPassword;
        private readonly NumericUpDown _numLength;
        private readonly CheckBox _chkUpper;
        private readonly CheckBox _chkLower;
        private readonly CheckBox _chkNumbers;
        private readonly CheckBox _chkSpecial;
        private readonly Label _lblStatus;
        private readonly ModernButton _btnGenerate;
        private readonly ModernButton _btnCopy;

        public PasswordView()
        {
            DoubleBuffered = true;
            BackColor = DarkColors.Background;
            Dock = DockStyle.Fill;
            Padding = new Padding(25);

            // Title
            Label lblTitle = new()
            {
                Text = "Password Generator",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            Controls.Add(lblTitle);

            Label lblSub = new()
            {
                Text = "Generate cryptographically secure random passwords for administrative IT use.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = DarkColors.TextMuted,
                AutoSize = true,
                Location = new Point(4, 38)
            };
            Controls.Add(lblSub);

            // Main Generator Card Panel
            CardPanel mainCard = new()
            {
                Location = new Point(0, 80),
                Size = new Size(700, 360),
                Padding = new Padding(25)
            };
            Controls.Add(mainCard);

            // Generated Password Box
            _txtPassword = new TextBox
            {
                Location = new Point(25, 25),
                Size = new Size(650, 36),
                Font = new Font("Consolas", 15F, FontStyle.Bold),
                BackColor = DarkColors.InputBg,
                ForeColor = DarkColors.Success,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true
            };
            mainCard.Controls.Add(_txtPassword);

            // Length row
            Label lblLenTitle = new()
            {
                Text = "Password Length:",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(25, 82)
            };
            mainCard.Controls.Add(lblLenTitle);

            _numLength = new NumericUpDown
            {
                Location = new Point(155, 80),
                Size = new Size(75, 26),
                Minimum = 8,
                Maximum = 128,
                Value = 16,
                Font = new Font("Segoe UI", 10F),
                BackColor = DarkColors.InputBg,
                ForeColor = DarkColors.TextMain
            };
            mainCard.Controls.Add(_numLength);

            // Options Group Box Card
            CardPanel optionsCard = new()
            {
                Location = new Point(25, 125),
                Size = new Size(650, 140),
                Padding = new Padding(15)
            };
            mainCard.Controls.Add(optionsCard);

            Label lblOptTitle = new()
            {
                Text = "Character Set Options",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = DarkColors.Primary,
                AutoSize = true,
                Location = new Point(12, 10)
            };
            optionsCard.Controls.Add(lblOptTitle);

            _chkUpper = CreateOptionCheckbox("Uppercase letters (A-Z)", true, 15, 38);
            optionsCard.Controls.Add(_chkUpper);

            _chkLower = CreateOptionCheckbox("Lowercase letters (a-z)", true, 15, 63);
            optionsCard.Controls.Add(_chkLower);

            _chkNumbers = CreateOptionCheckbox("Numeric digits (0-9)", true, 340, 38);
            optionsCard.Controls.Add(_chkNumbers);

            _chkSpecial = CreateOptionCheckbox("Special characters (!@#$%^&*)", true, 340, 63);
            optionsCard.Controls.Add(_chkSpecial);

            // Buttons Flow
            FlowLayoutPanel btnFlow = new()
            {
                Location = new Point(25, 280),
                Size = new Size(650, 45)
            };
            mainCard.Controls.Add(btnFlow);

            _btnGenerate = new ModernButton
            {
                Text = "🔄 Generate Password",
                Style = ButtonStyle.Primary,
                Width = 185,
                Margin = new Padding(0, 0, 12, 0)
            };
            _btnGenerate.Click += (s, e) => GeneratePassword();
            btnFlow.Controls.Add(_btnGenerate);

            _btnCopy = new ModernButton
            {
                Text = "📋 Copy to Clipboard",
                Style = ButtonStyle.Secondary,
                Width = 175
            };
            _btnCopy.Click += (s, e) => CopyPassword();
            btnFlow.Controls.Add(_btnCopy);

            // Status Label
            _lblStatus = new Label
            {
                Location = new Point(25, 330),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                ForeColor = DarkColors.TextMuted
            };
            mainCard.Controls.Add(_lblStatus);

            GeneratePassword();
        }

        private static CheckBox CreateOptionCheckbox(string text, bool checkedState, int x, int y)
        {
            return new CheckBox
            {
                Text = text,
                Checked = checkedState,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = DarkColors.TextMain,
                AutoSize = true,
                Location = new Point(x, y)
            };
        }

        private void GeneratePassword()
        {
            int len = (int)_numLength.Value;
            var (pwd, status) = PasswordService.GeneratePassword(
                len,
                _chkUpper.Checked,
                _chkLower.Checked,
                _chkNumbers.Checked,
                _chkSpecial.Checked
            );

            _txtPassword.Text = pwd;
            _lblStatus.Text = status;
        }

        private void CopyPassword()
        {
            if (!string.IsNullOrEmpty(_txtPassword.Text))
            {
                Clipboard.SetText(_txtPassword.Text);
                _lblStatus.Text = "Password copied to clipboard!";
            }
        }
    }
}
