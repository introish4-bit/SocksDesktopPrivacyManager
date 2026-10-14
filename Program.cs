using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SocksDesktopPrivacyManager
{
    internal sealed class ProxyProfile
    {
        public string Type { get; set; } = "SOCKS5";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 1080;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string UserAgent { get; set; } = "";
    }

    internal sealed class AppRule
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly ComboBox type = new ComboBox();
        private readonly TextBox host = new TextBox();
        private readonly NumericUpDown port = new NumericUpDown();
        private readonly TextBox username = new TextBox();
        private readonly TextBox password = new TextBox();
        private readonly TextBox userAgent = new TextBox();
        private readonly ListBox apps = new ListBox();
        private readonly Label status = new Label();
        private readonly List<AppRule> rules = new List<AppRule>();
        private readonly string profileFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SocksDesktopPrivacyManager", "profile.json");

        public MainForm()
        {
            Text = "SOCKS Desktop Privacy Manager";
            MinimumSize = new Size(780, 620);
            Size = new Size(900, 700);
            BackColor = Color.FromArgb(20, 25, 35);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9F);
            BuildUi();
            LoadProfile();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(18) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 235));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);

            var title = new Label { Text = "SOCKS DESKTOP  |  PRIVACY MANAGER", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.DeepSkyBlue, TextAlign = ContentAlignment.MiddleLeft };
            root.Controls.Add(title, 0, 0);

            var proxy = new GroupBox { Text = "Proxy profile", Dock = DockStyle.Fill, ForeColor = Color.White, Padding = new Padding(12) };
            root.Controls.Add(proxy, 0, 1);
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            proxy.Controls.Add(grid);

            type.DropDownStyle = ComboBoxStyle.DropDownList;
            type.Items.AddRange(new object[] { "SOCKS5", "SOCKS4", "HTTP", "HTTPS" });
            host.Text = "127.0.0.1";
            port.Minimum = 1; port.Maximum = 65535; port.Value = 1080;
            password.UseSystemPasswordChar = true;
            AddField(grid, "Protocol", type, 0, 0);
            AddField(grid, "Host / IP", host, 2, 0);
            AddField(grid, "Port", port, 0, 1);
            AddField(grid, "Username", username, 2, 1);
            AddField(grid, "Password", password, 0, 2);
            AddField(grid, "User-Agent", userAgent, 2, 2);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            var test = MakeButton("Test proxy", Color.FromArgb(0, 145, 210));
            test.Click += async (s, e) => {
                status.Text = "Testing proxy handshake...";
                try { string result = await System.Threading.Tasks.Task.Run(() => ProxyTester.Test(CurrentProfile())); status.Text = result; }
                catch (Exception ex) { status.Text = "Test failed: " + ex.Message; }
            };
            var save = MakeButton("Save profile", Color.FromArgb(65, 80, 100)); save.Click += (s, e) => SaveProfile();
            var load = MakeButton("Load profile", Color.FromArgb(65, 80, 100)); load.Click += (s, e) => LoadProfile();
            var randomUa = MakeButton("Random User-Agent", Color.FromArgb(65, 80, 100));
            randomUa.Click += (s, e) => userAgent.Text = new[] {
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36"
            }[new Random().Next(3)];
            buttons.Controls.AddRange(new Control[] { test, save, load, randomUa });
            grid.Controls.Add(buttons, 0, 3); grid.SetColumnSpan(buttons, 4);

            var appGroup = new GroupBox { Text = "Application launch rules (select an EXE)", Dock = DockStyle.Fill, ForeColor = Color.White, Padding = new Padding(12) };
            root.Controls.Add(appGroup, 0, 2);
            var appLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            appLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
            appLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            appGroup.Controls.Add(appLayout);
            apps.Dock = DockStyle.Fill; apps.BackColor = Color.FromArgb(30, 37, 49); apps.ForeColor = Color.White;
            apps.SelectedIndexChanged += (s, e) => { };
            appLayout.Controls.Add(apps, 0, 0);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            var add = MakeButton("Add application EXE", Color.FromArgb(0, 145, 210));
            add.Click += (s, e) => AddApp();
            var proxied = MakeButton("Launch via proxy", Color.FromArgb(0, 145, 120));
            proxied.Click += (s, e) => LaunchSelected(true);
            var direct = MakeButton("Launch direct", Color.FromArgb(80, 90, 110));
            direct.Click += (s, e) => LaunchSelected(false);
            var remove = MakeButton("Remove rule", Color.FromArgb(140, 65, 65));
            remove.Click += (s, e) => RemoveSelected();
            var webrtc = MakeButton("WebRTC leak test", Color.FromArgb(80, 90, 110));
            webrtc.Click += (s, e) => Process.Start(new ProcessStartInfo("https://browserleaks.com/webrtc") { UseShellExecute = true });
            actions.Controls.AddRange(new Control[] { add, proxied, direct, remove, webrtc });
            appLayout.Controls.Add(actions, 1, 0);

            status.Text = "Ready. Proxying depends on application support; see README for limitations.";
            status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleLeft; status.ForeColor = Color.LightSkyBlue;
            root.Controls.Add(status, 0, 3);
        }

        private void AddField(TableLayoutPanel grid, string label, Control input, int col, int row)
        {
            var l = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gainsboro };
            input.Dock = DockStyle.Fill;
            if (input is TextBox) { input.BackColor = Color.FromArgb(35, 42, 55); input.ForeColor = Color.White; }
            grid.Controls.Add(l, col, row); grid.Controls.Add(input, col + 1, row);
        }

        private Button MakeButton(string text, Color color)
        {
            return new Button { Text = text, Width = 155, Height = 34, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(4) };
        }

        private ProxyProfile CurrentProfile()
        {
            return new ProxyProfile { Type = Convert.ToString(type.SelectedItem ?? "SOCKS5"), Host = host.Text.Trim(), Port = (int)port.Value, Username = username.Text, Password = password.Text, UserAgent = userAgent.Text.Trim() };
        }

        private void SaveProfile()
        {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(profileFile));
                File.WriteAllText(profileFile, new JavaScriptSerializer().Serialize(CurrentProfile()), Encoding.UTF8);
                status.Text = "Profile saved: " + profileFile + " (credentials are stored locally; protect this Windows account).";
            } catch (Exception ex) { status.Text = "Save failed: " + ex.Message; }
        }

        private void LoadProfile()
        {
            try {
                if (File.Exists(profileFile)) {
                    var p = new JavaScriptSerializer().Deserialize<ProxyProfile>(File.ReadAllText(profileFile, Encoding.UTF8));
                    type.SelectedItem = p.Type; host.Text = p.Host; port.Value = Math.Max(port.Minimum, Math.Min(port.Maximum, p.Port));
                    username.Text = p.Username; password.Text = p.Password; userAgent.Text = p.UserAgent;
                } else type.SelectedItem = "SOCKS5";
            } catch (Exception ex) { status.Text = "Could not load profile: " + ex.Message; }
        }

        private void AddApp()
        {
            using (var d = new OpenFileDialog { Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*", Title = "Choose an application" }) {
                if (d.ShowDialog() == DialogResult.OK) {
                    if (!rules.Exists(x => String.Equals(x.Path, d.FileName, StringComparison.OrdinalIgnoreCase)))
                        rules.Add(new AppRule { Name = Path.GetFileName(d.FileName), Path = d.FileName });
                    RefreshApps();
                }
            }
        }

        private void RefreshApps()
        {
            apps.Items.Clear();
            foreach (var r in rules) apps.Items.Add(r.Name + " — " + r.Path);
        }

        private void RemoveSelected()
        {
            if (apps.SelectedIndex >= 0) { rules.RemoveAt(apps.SelectedIndex); RefreshApps(); }
        }

        private void LaunchSelected(bool useProxy)
        {
            if (apps.SelectedIndex < 0) { status.Text = "Select an application first."; return; }
            var rule = rules[apps.SelectedIndex];
            try {
                var psi = new ProcessStartInfo { FileName = rule.Path, UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(rule.Path) };
                if (useProxy) {
                    var p = CurrentProfile();
                    if (p.Type == "SOCKS4" || p.Type == "SOCKS5")
                        psi.Arguments = "--proxy-server=socks" + (p.Type == "SOCKS4" ? "4" : "5") + "://" + p.Host + ":" + p.Port;
                    else
                        psi.Arguments = "--proxy-server=" + p.Type.ToLowerInvariant() + "://" + p.Host + ":" + p.Port;
                    if (!String.IsNullOrWhiteSpace(p.UserAgent)) psi.Arguments += " --user-agent=\"" + p.UserAgent.Replace("\"", "") + "\"";
                    status.Text = "Launched with proxy arguments. This works only for apps that recognize these arguments (not arbitrary EXEs).";
                } else status.Text = "Launched directly; this app uses the original network unless it has its own proxy settings.";
                Process.Start(psi);
            } catch (Exception ex) { status.Text = "Launch failed: " + ex.Message; }
        }
    }

    internal static class ProxyTester
    {
        public static string Test(ProxyProfile p)
        {
            if (String.IsNullOrWhiteSpace(p.Host)) throw new Exception("Enter a proxy host.");
            using (var c = new TcpClient())
            {
                c.ReceiveTimeout = 5000; c.SendTimeout = 5000;
                c.Connect(p.Host, p.Port);
                using (NetworkStream s = c.GetStream())
                {
                    if (p.Type == "SOCKS5") TestSocks5(s, p);
                    else if (p.Type == "SOCKS4") TestSocks4(s, p);
                    else TestHttpConnect(s, p);
                }
            }
            return p.Type + " handshake succeeded at " + p.Host + ":" + p.Port + ". This does not prove that external traffic or WebRTC is leak-free.";
        }

        private static void TestSocks5(NetworkStream s, ProxyProfile p)
        {
            bool auth = !String.IsNullOrEmpty(p.Username);
            s.Write(new byte[] { 5, 1, (byte)(auth ? 2 : 0) }, 0, 3);
            byte[] r = ReadExact(s, 2);
            if (r[0] != 5 || r[1] == 255) throw new Exception("SOCKS5 authentication method rejected.");
            if (r[1] == 2) {
                byte[] u = Encoding.UTF8.GetBytes(p.Username); byte[] pw = Encoding.UTF8.GetBytes(p.Password);
                if (u.Length > 255 || pw.Length > 255) throw new Exception("SOCKS credentials must be 255 bytes or less.");
                var packet = new List<byte> { 1, (byte)u.Length }; packet.AddRange(u); packet.Add((byte)pw.Length); packet.AddRange(pw);
                byte[] b = packet.ToArray(); s.Write(b, 0, b.Length);
                byte[] ar = ReadExact(s, 2); if (ar[1] != 0) throw new Exception("SOCKS5 username/password authentication failed.");
            } else if (r[1] != 0) throw new Exception("Unsupported SOCKS5 authentication method.");
            byte[] req = new byte[] { 5, 1, 0, 1, 0, 0, 0, 0, 0, 0 };
            s.Write(req, 0, req.Length);
            byte[] head = ReadExact(s, 4);
            if (head[1] != 0) throw new Exception("SOCKS5 connect request rejected (reply " + head[1] + ").");
            int len = head[3] == 1 ? 4 : head[3] == 4 ? 16 : ReadExact(s, 1)[0];
            ReadExact(s, len + 2);
        }

        private static void TestSocks4(NetworkStream s, ProxyProfile p)
        {
            byte[] user = Encoding.ASCII.GetBytes(p.Username ?? "");
            var b = new List<byte> { 4, 1, (byte)(p.Port >> 8), (byte)(p.Port & 255), 0, 0, 0, 1 };
            b.AddRange(user); b.Add(0);
            byte[] domain = Encoding.ASCII.GetBytes(p.Host); b.AddRange(domain); b.Add(0);
            byte[] packet = b.ToArray(); s.Write(packet, 0, packet.Length);
            byte[] r = ReadExact(s, 8);
            if (r[1] != 90) throw new Exception("SOCKS4 request rejected (reply " + r[1] + ").");
        }

        private static void TestHttpConnect(NetworkStream s, ProxyProfile p)
        {
            string auth = "";
            if (!String.IsNullOrEmpty(p.Username)) auth = "Proxy-Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(p.Username + ":" + p.Password)) + "\r\n";
            string req = "CONNECT example.com:443 HTTP/1.1\r\nHost: example.com:443\r\n" + auth + "Proxy-Connection: Keep-Alive\r\n\r\n";
            byte[] b = Encoding.ASCII.GetBytes(req); s.Write(b, 0, b.Length);
            var response = new StringBuilder(); byte[] buf = new byte[1];
            while (response.Length < 4096) {
                int n = s.Read(buf, 0, 1); if (n == 0) break; response.Append((char)buf[0]);
                if (response.ToString().EndsWith("\r\n\r\n")) break;
            }
            string first = response.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
            if (!(first.Contains(" 200 ") || first.EndsWith(" 200"))) throw new Exception("HTTP CONNECT failed: " + first);
        }

        private static byte[] ReadExact(NetworkStream s, int n)
        {
            byte[] b = new byte[n]; int got = 0;
            while (got < n) { int r = s.Read(b, got, n - got); if (r <= 0) throw new Exception("Proxy closed connection unexpectedly."); got += r; }
            return b;
        }
    }
}
