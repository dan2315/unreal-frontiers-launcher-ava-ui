using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace UnrealFrontiersInstaller;

/// <summary>
/// The installer the user runs: asks where to install, then runs the Velopack Setup.exe embedded in this file with
/// --installto. Silent switches (/S, /SILENT, /VERYSILENT, --silent) skip the question and install to the default folder,
/// which is how launchers from before Velopack update themselves.
/// </summary>
internal static class Program
{
    private static string Meta(string key) =>
        Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value ?? "";

    [STAThread]
    private static int Main(string[] args)
    {
        var title = Meta("AppTitle");
        var folderName = Meta("FolderName");
        var defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), folderName);

        var silent = args.Any(a => a.Equals("/S", StringComparison.OrdinalIgnoreCase) || a.Equals("/SILENT", StringComparison.OrdinalIgnoreCase)
            || a.Equals("/VERYSILENT", StringComparison.OrdinalIgnoreCase) || a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
        var given = ValueOf(args, "--installto");

        string target;
        if (given != null)
        {
            target = InstallPath.Resolve(given, folderName);
        }
        else if (silent)
        {
            target = InstallPath.Resolve(defaultDir, folderName);
        }
        else
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using var form = new InstallerForm(title, folderName, defaultDir);
            if (form.ShowDialog() != DialogResult.OK) return 1;
            target = form.Target;
        }

        return Run(target, silent, title);
    }

    private static int Run(string target, bool silent, string title)
    {
        string temp = Path.Combine(Path.GetTempPath(), "uf-setup-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            var setup = Path.Combine(temp, "Setup.exe");
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Setup.exe"))
            {
                if (resource == null) throw new InvalidOperationException("The installer is damaged: Setup.exe is missing.");
                using var file = File.Create(setup);
                resource.CopyTo(file);
            }

            var info = new ProcessStartInfo(setup, $"--installto \"{target}\"" + (silent ? " --silent" : "")) { UseShellExecute = false };
            using var process = Process.Start(info)!;
            process.WaitForExit();
            if (process.ExitCode != 0 && !silent)
                MessageBox.Show($"Установка не завершилась (код {process.ExitCode}).", title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return process.ExitCode;
        }
        catch (Exception e)
        {
            if (!silent) MessageBox.Show("Не удалось установить:\n" + e.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static string? ValueOf(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }
}

internal sealed class InstallerForm : Form
{
    private readonly TextBox _path = new();
    private readonly Label _effective = new();
    private readonly string _folderName;
    private readonly float _scale;

    private int S(int pixels) => (int)Math.Round(pixels * _scale);

    public string Target { get; private set; } = "";

    public InstallerForm(string title, string folderName, string defaultDir)
    {
        _folderName = folderName;
        // Sizes below are in 96-DPI pixels; S() scales them to the screen, the same way the fonts scale.
        AutoScaleMode = AutoScaleMode.None;
        using (var graphics = CreateGraphics()) _scale = graphics.DpiX / 96f;
        Text = title;
        ClientSize = new Size(S(560), S(230));
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(22, 20, 27);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

        Controls.Add(new Label { Text = "Установка " + title, Left = S(20), Top = S(16), Width = S(520), Height = S(34), Font = new Font("Segoe UI Semibold", 14f) });
        Controls.Add(new Label { Text = "Папка установки:", Left = S(20), Top = S(62), Width = S(520), Height = S(24), ForeColor = Color.FromArgb(185, 189, 194) });

        _path.SetBounds(S(20), S(90), S(410), S(28));
        _path.Text = defaultDir;
        _path.BackColor = Color.FromArgb(38, 35, 44);
        _path.ForeColor = Color.White;
        _path.BorderStyle = BorderStyle.FixedSingle;
        _path.TextChanged += (_, _) => UpdateEffective();
        Controls.Add(_path);

        var browse = new Button { Text = "Обзор…", Left = S(440), Top = S(88), Width = S(100), Height = S(32) };
        Style(browse, false);
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Выберите папку для установки", SelectedPath = _path.Text, ShowNewFolderButton = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) _path.Text = dialog.SelectedPath;
        };
        Controls.Add(browse);

        _effective.SetBounds(S(20), S(130), S(520), S(48));
        _effective.ForeColor = Color.FromArgb(142, 147, 155);
        Controls.Add(_effective);

        var install = new Button { Text = "Установить", Left = S(320), Top = S(184), Width = S(120), Height = S(36), DialogResult = DialogResult.None };
        Style(install, true);
        install.Click += (_, _) => Accept();
        Controls.Add(install);

        var cancel = new Button { Text = "Отмена", Left = S(450), Top = S(184), Width = S(90), Height = S(36), DialogResult = DialogResult.Cancel };
        Style(cancel, false);
        Controls.Add(cancel);

        AcceptButton = install;
        CancelButton = cancel;
        UpdateEffective();
    }

    private static void Style(Button button, bool accent)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = accent ? Color.FromArgb(0, 214, 143) : Color.FromArgb(46, 44, 52);
        button.ForeColor = accent ? Color.FromArgb(10, 31, 24) : Color.White;
        button.Cursor = Cursors.Hand;
    }

    private string? Effective()
    {
        try { return string.IsNullOrWhiteSpace(_path.Text) ? null : InstallPath.Resolve(_path.Text, _folderName); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    private void UpdateEffective()
    {
        var effective = Effective();
        _effective.Text = effective == null ? "Укажите корректный путь."
            : string.Equals(effective, Path.GetFullPath(_path.Text.Trim().Trim('"')), StringComparison.OrdinalIgnoreCase)
                ? "Будет установлено в: " + effective
                : "Папка не пуста, поэтому лаунчер встанет в подпапку:\n" + effective;
    }

    private void Accept()
    {
        var effective = Effective();
        if (effective == null)
        {
            MessageBox.Show(this, "Укажите корректный путь.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try { Directory.CreateDirectory(effective); }
        catch (Exception e)
        {
            MessageBox.Show(this, "Нельзя создать папку:\n" + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Target = effective;
        DialogResult = DialogResult.OK;
    }
}
