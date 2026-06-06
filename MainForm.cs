using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System;

namespace MIDI2VIPI
{
    public class MainForm : Form, IMessageFilter
    {
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == 0x0201 || m.Msg == 0x00A1)
            {
                if (_entHotkey != null && _entHotkey.Focused)
                {
                    Point pt = _entHotkey.PointToClient(Cursor.Position);
                    if (pt.X < 0 || pt.X >= _entHotkey.Width || pt.Y < 0 || pt.Y >= _entHotkey.Height)
                        this.ActiveControl = null;
                }
                if (_entPauseKey != null && _entPauseKey.Focused)
                {
                    Point pt = _entPauseKey.PointToClient(Cursor.Position);
                    if (pt.X < 0 || pt.X >= _entPauseKey.Width || pt.Y < 0 || pt.Y >= _entPauseKey.Height)
                        this.ActiveControl = null;
                }
            }
            return false;
        }

        private MidiEngine _eng;
        private string _midiPath;
        private List<MidiEngine.TrackInfo> _trackInfos;
        private Dictionary<int, DarkCheckBox> _trackChecks = new Dictionary<int, DarkCheckBox>();

        private Label _lblFile, _lblTime, _lblWarn, _lblVol, _lblBpm, _lblTrans;
        private AccentButton _btnSynth, _btnMacro, _btnPause, _btnStop;
        private DarkTrackBar _sliderProg, _sliderVol, _sliderBpm, _sliderTrans;
        private TextBox _entHotkey, _entPauseKey;
        private FlowLayoutPanel _tracksPanel;
        private int _initialBpm = 120;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int useImmersiveDarkMode = 1;
                UIHelper.DwmSetWindowAttribute(Handle, 20, ref useImmersiveDarkMode, sizeof(int));
                UIHelper.DwmSetWindowAttribute(Handle, 19, ref useImmersiveDarkMode, sizeof(int));
                int captionColor = ColorTranslator.ToWin32(Theme.BG);
                UIHelper.DwmSetWindowAttribute(Handle, 35, ref captionColor, sizeof(int));
            }
            catch { }
        }

        public MainForm()
        {
            Text = "MIDI2VIPI"; Size = new Size(720, 640); MinimumSize = new Size(600, 580);
            BackColor = Theme.BG; ForeColor = Theme.Ink; DoubleBuffered = true;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            _eng = new MidiEngine();
            _eng.OnProgress = OnProgress;
            _eng.OnFinish = OnFinish;

            Application.AddMessageFilter(this);
            BuildUI();

            var thread = new Thread(HotkeyLoop) { IsBackground = true };
            thread.Start();

            var layoutTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            layoutTimer.Tick += delegate { CheckLayout(); };
            layoutTimer.Start();
        }

        private void BuildUI()
        {
            SuspendLayout();
            Padding = new Padding(15, 0, 15, 15);

            var accentLine = new Panel { BackColor = Theme.Accent, Height = 3, Dock = DockStyle.Top };

            var header = new Panel { Height = 56, Dock = DockStyle.Top, BackColor = Color.Transparent };
            var lblTitle = new Label { Text = "MIDI2VIPI", Font = Theme.Title, ForeColor = Theme.Ink, AutoSize = true, Location = new Point(0, 12) };
            var lblSub = new Label { Text = "MIDI macro & synth", UseMnemonic = false, Font = Theme.Main, ForeColor = Theme.InkDim, AutoSize = true, Location = new Point(140, 21) };

            Image ghIcon = null;
            try
            {
                byte[] ghBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAABHNCSVQICAgIfAhkiAAAAAlwSFlzAAAA3QAAAN0BcFOiBwAAABl0RVh0U29mdHdhcmUAd3d3Lmlua3NjYXBlLm9yZ5vuPBoAAARCSURBVFiFrZdPbFRVFMZ/584AAkFShQoVDNHSiBV0berMtG6sGmDTWkP/0CYG2g4oiTGNmzIJCkvDtDTRBNLpBGybGDEmJC6caa2sMETF1jRt3CCEUmWhAYrvveNiptCZ3kdHOmc173znft9377vz7j2iqhQa1bF0cMssVajsQNiMshkA4TrKdUQnrm5gLNUdcQrllEIMNHekwhowbaK8pfDEQwnhLxW+Edc7nThVPbIsAy0d3+/yxDuBULukS1soF4yarv5Tr/7sV2L8gMbOdJdnvMuPLA4g1HrGu9zYme7yLclfgdbW9GPOGvkcofGRhW2hJIO39d0zZyJ3F6ZzViAWwzhrZbDo4gBCo7NWBmOxXM2ch+nZ0Y+B3UUXfxC7sxqLDbREU3tUNeddKXJs5Z3g44oeAa79D6Frih7JjJVjOZyqXS3R1J75Z1HVzP/7pvwKVOQUizQk46FBgMOHL6y65a1pA3YCU4hMqciUcVVBy4FyNZSjXCkxt0+fPFk7B9AcTTcoci7P4OTVjVqZ6o44QYCts6ZN0Yq8IgKuTMz/zhL2+cx4wiePGp3Ak/x0xdZZ0wZ8ZgDU0w+sgwPOZj/iQkPdwFPWfFbT7I+mKhG2W4uQueUaEOPdswNs3x9NVRpHAnutBUpyIB5JL9fAQDySRknaMEcCe42oVtkNyifLFV+KS1SrDFBmwe7cvXljslgGslx3LFCZATYttsb00FCdWywDQ0N1LsK0BdpkgHWL0srWYokvwbnO+Dhb3/L+D88USzvLtX4RIEwblHHbIHXcF4tlwJdLGTeIWr9iHt4bxTLgyyU6YRRjNSBIR/Oh0ZrlijcfGq0RpMOGKWbCBMW5BNjuZaKqidb29LZHFW9tT29T1QSw6DAANCjOJVFVmqIj54CGrK3LCBuBLdnCOUH6cIPHE32vzBQi3Nx+sVTNvY8QOQis8in7YqAn/I6oKi3RsQoPdxwIKHJePdNqjNsAxIHAvGPgNxHGntsQPtjdjbeQTQTZ15mOC1IDPO8z6/lwDYEX+nuqJg1Af0/VJJAAEHSPMe7ZgZ5wnyDvLdQAdqAE8sUBVFHU/A7sWEIcIJHVfHAj8nBjwPzp93pTdLQ+0RPqBT4EbgB/A2OqetyX1vDtEsIAc1mtzKwW3oqbDo3uRnUYWAnMuEFn19lPX7sRi2GOHkVVrZv1fhw48OOK2yv+sR+/mfjX4NX191SftxqwmLioIgeT8dAvBcwMEaSxc2TR65kXR7R+IB75KmeMrTPKM6HAT2Q24+pkb7jCbyXq64cDq0pLbX2hIypvJ3pDX+YD1s5oIB76WkRqEa6Q2VAvk7mMltfVDft2UyUlz9qwcVV50ybuawAgEQ99V74h/JKK7AOm7gOVlQG/MbfKVi/c/dMq2lS+Mbwz2Rvy3ZwFdcfVsXTw6RlpBX0y2Rs58bDaTB8of/5RqmcKadP/A4/ywkXO22xSAAAAAElFTkSuQmCC");
                var bmp = new Bitmap(Image.FromStream(new MemoryStream(ghBytes)), new Size(20, 20));
                for (int y = 0; y < bmp.Height; y++)
                {
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        Color cc = bmp.GetPixel(x, y);
                        if (cc.A > 0) bmp.SetPixel(x, y, Color.FromArgb(cc.A, Theme.Ink));
                    }
                }
                ghIcon = bmp;
            }
            catch { }

            var btnGH = new AccentButton { Text = ghIcon == null ? "GH" : "", IconImg = ghIcon, BaseColor = Theme.Surface2, HoverCol = Theme.Border, ForeColor = Theme.Ink, Size = new Size(32, 32), Radius = 16 };
            btnGH.ClickEvent += delegate { Process.Start("https://github.com/1nfys/MIDI2VIPI"); };
            header.Controls.AddRange(new Control[] { lblTitle, lblSub, btnGH });
            header.Resize += delegate { btnGH.Location = new Point(header.Width - 32, 12); };

            var bottom = new DarkPanel { BackColor = Theme.Surface, Height = 126, Dock = DockStyle.Bottom, Padding = new Padding(15) };

            var progRow = new Panel { Height = 26, Dock = DockStyle.Top, BackColor = Color.Transparent };
            _lblTime = new Label { Text = "0:00 / 0:00", Font = Theme.MonoSm, ForeColor = Theme.InkDim, AutoSize = false, Width = 90, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Right, Padding = new Padding(0, 0, 0, 0) };
            _sliderProg = new DarkTrackBar { Dock = DockStyle.Fill, Height = 20 };
            _sliderProg.ValueChanged += delegate (double v) { if (_eng.Duration > 0) _eng.SeekReq = v * _eng.Duration; };
            progRow.Controls.Add(_sliderProg);
            progRow.Controls.Add(_lblTime);

            var btnRow = new TableLayoutPanel { Height = 34, Dock = DockStyle.Top, BackColor = Color.Transparent, ColumnCount = 4, Margin = new Padding(0) };
            for (int i = 0; i < 4; i++) btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            _btnSynth = new AccentButton { Text = "Synth", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
            _btnMacro = new AccentButton { Text = "Macro", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
            _btnPause = new AccentButton { Text = "Pause", BaseColor = Theme.Surface2, HoverCol = Theme.Border, ForeColor = Theme.Ink, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
            _btnStop = new AccentButton { Text = "Stop", BaseColor = Theme.Surface2, HoverCol = Theme.Border, ForeColor = Theme.Ink, Dock = DockStyle.Fill, Margin = new Padding(0) };
            _btnSynth.ClickEvent += delegate { PlaySynth(); };
            _btnMacro.ClickEvent += delegate { PlayMacro(); };
            _btnPause.ClickEvent += delegate { Pause(); };
            _btnStop.ClickEvent += delegate { StopPlay(); };
            btnRow.Controls.Add(_btnSynth, 0, 0); btnRow.Controls.Add(_btnMacro, 1, 0); btnRow.Controls.Add(_btnPause, 2, 0); btnRow.Controls.Add(_btnStop, 3, 0);

            var slRow = new TableLayoutPanel { Height = 34, Dock = DockStyle.Top, BackColor = Color.Transparent, ColumnCount = 3, Margin = new Padding(0) };
            for (int i = 0; i < 3; i++) slRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

            var pVol = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _lblVol = new Label { Text = "VOL 100%", Font = Theme.Bold, ForeColor = Theme.InkDim, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
            _sliderVol = new DarkTrackBar { Minimum = 0, Maximum = 100, Value = 100 };
            _sliderVol.ValueChanged += delegate (double v) { _lblVol.Text = "VOL " + (int)v + "%"; _eng.Volume = v / 100; };
            pVol.Controls.AddRange(new Control[] { _lblVol, _sliderVol });
            pVol.Resize += delegate
            {
                _lblVol.Bounds = new Rectangle(0, 10, 70, 20);
                _sliderVol.Bounds = new Rectangle(70, 10, pVol.Width - 76, 20);
            };

            var pBpm = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _lblBpm = new Label { Text = "BPM 120", Font = Theme.Bold, ForeColor = Theme.InkDim, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
            _sliderBpm = new DarkTrackBar { Minimum = 40, Maximum = 280, Value = 120 };
            _sliderBpm.ValueChanged += delegate (double v) { _lblBpm.Text = "BPM " + (int)v; _eng.Bpm = (int)v; };
            var btnRBpm = new AccentButton { Text = "R", BaseColor = Theme.Surface2, HoverCol = Theme.Border, ForeColor = Theme.Ink, Font = new Font("Segoe UI", 7, FontStyle.Bold), Radius = 4 };
            btnRBpm.ClickEvent += delegate { _sliderBpm.Value = _initialBpm; _lblBpm.Text = "BPM " + _initialBpm; _eng.Bpm = _initialBpm; };
            pBpm.Controls.AddRange(new Control[] { _lblBpm, _sliderBpm, btnRBpm });
            pBpm.Resize += delegate
            {
                _lblBpm.Bounds = new Rectangle(6, 10, 66, 20);
                _sliderBpm.Bounds = new Rectangle(72, 10, pBpm.Width - 72 - 28, 20);
                btnRBpm.Bounds = new Rectangle(pBpm.Width - 22, 10, 22, 20);
            };

            var pTrans = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _lblTrans = new Label { Text = "SHIFT 0", Font = Theme.Bold, ForeColor = Theme.InkDim, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
            _sliderTrans = new DarkTrackBar { Minimum = -24, Maximum = 24, Value = 0 };
            _sliderTrans.ValueChanged += delegate (double v) { int val = (int)v; string sign = val > 0 ? "+" : ""; _lblTrans.Text = "SHIFT " + sign + val; _eng.Transpose = val; };
            var btnRTrans = new AccentButton { Text = "R", BaseColor = Theme.Surface2, HoverCol = Theme.Border, ForeColor = Theme.Ink, Font = new Font("Segoe UI", 7, FontStyle.Bold), Radius = 4 };
            btnRTrans.ClickEvent += delegate { _sliderTrans.Value = 0; _lblTrans.Text = "SHIFT 0"; _eng.Transpose = 0; };
            pTrans.Controls.AddRange(new Control[] { _lblTrans, _sliderTrans, btnRTrans });
            pTrans.Resize += delegate
            {
                _lblTrans.Bounds = new Rectangle(6, 10, 66, 20);
                _sliderTrans.Bounds = new Rectangle(72, 10, pTrans.Width - 72 - 28, 20);
                btnRTrans.Bounds = new Rectangle(pTrans.Width - 22, 10, 22, 20);
            };

            slRow.Controls.Add(pVol, 0, 0); slRow.Controls.Add(pBpm, 1, 0); slRow.Controls.Add(pTrans, 2, 0);

            var bottomSpacer = new Panel { Height = 10, Dock = DockStyle.Top, BackColor = Color.Transparent };

            bottom.Controls.Add(slRow);
            bottom.Controls.Add(bottomSpacer);
            bottom.Controls.Add(btnRow);
            bottom.Controls.Add(progRow);

            var content = new DarkPanel { BackColor = Theme.Surface, Dock = DockStyle.Fill, Padding = new Padding(15) };

            var fileRow = new Panel { Height = 34, Dock = DockStyle.Top, BackColor = Color.Transparent };
            var btnOpen = new AccentButton { Text = "Open MIDI", Size = new Size(110, 30), Location = new Point(0, 0) };
            btnOpen.ClickEvent += delegate { BrowseFile(); };
            _lblFile = new Label { Text = "No file loaded", Font = Theme.Main, ForeColor = Theme.InkDim, AutoSize = false, Location = new Point(120, 5), Size = new Size(400, 20), Anchor = AnchorStyles.Left | AnchorStyles.Right };
            fileRow.Controls.AddRange(new Control[] { btnOpen, _lblFile });

            var sep = new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Theme.Border };
            var sepSpacer = new Panel { Height = 16, Dock = DockStyle.Top, BackColor = Color.Transparent };

            var tracksHeader = new Panel { Height = 28, Dock = DockStyle.Top, BackColor = Color.Transparent };
            var lblTracks = new Label { Text = "Tracks", Font = Theme.Bold, ForeColor = Theme.Ink, AutoSize = true, Location = new Point(0, 3) };
            var btnAll = new AccentButton { Text = "All", BaseColor = Theme.Surface2, HoverCol = Theme.Border, ForeColor = Theme.Ink, Size = new Size(42, 22) };
            var btnNone = new AccentButton { Text = "None", BaseColor = Theme.Surface2, HoverCol = Theme.Border, ForeColor = Theme.Ink, Size = new Size(42, 22) };
            btnAll.ClickEvent += delegate { SetAllTracks(true); };
            btnNone.ClickEvent += delegate { SetAllTracks(false); };
            tracksHeader.Controls.AddRange(new Control[] { lblTracks, btnNone, btnAll });
            tracksHeader.Resize += delegate { btnAll.Location = new Point(tracksHeader.Width - 42, 0); btnNone.Location = new Point(tracksHeader.Width - 90, 0); };

            var macroRow = new Panel { Height = 30, Dock = DockStyle.Bottom, BackColor = Color.Transparent };
            var lblMacro = new Label { Text = "Macro hotkey", Font = Theme.Bold, ForeColor = Theme.Ink, AutoSize = true, Location = new Point(0, 6) };
            _entHotkey = new TextBox
            {
                Text = "-",
                Width = 40,
                Height = 22,
                Font = Theme.Mono,
                BackColor = Theme.Surface2,
                ForeColor = Theme.Ink,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Center,
                Location = new Point(96, 4),
                MaxLength = 1
            };
            var lblPause = new Label { Text = "Pause hotkey", Font = Theme.Bold, ForeColor = Theme.Ink, AutoSize = true, Location = new Point(146, 6) };
            _entPauseKey = new TextBox
            {
                Text = "=",
                Width = 40,
                Height = 22,
                Font = Theme.Mono,
                BackColor = Theme.Surface2,
                ForeColor = Theme.Ink,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Center,
                Location = new Point(236, 4),
                MaxLength = 1
            };
            _lblWarn = new Label { Text = "", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Theme.Warn, AutoSize = true, Location = new Point(286, 7) };
            macroRow.Controls.AddRange(new Control[] { lblMacro, _entHotkey, lblPause, _entPauseKey, _lblWarn });

            var tracksContainer = new DarkPanel { Dock = DockStyle.Fill, BackColor = Theme.BG, Radius = 6, Padding = new Padding(2) };
            _tracksPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.BG,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(4)
            };
            tracksContainer.Controls.Add(_tracksPanel);

            var tracksSpacerTop = new Panel { Height = 8, Dock = DockStyle.Top, BackColor = Color.Transparent };
            var tracksSpacerBot = new Panel { Height = 12, Dock = DockStyle.Bottom, BackColor = Color.Transparent };

            content.Controls.Add(tracksContainer);
            content.Controls.Add(tracksSpacerBot);
            content.Controls.Add(macroRow);
            content.Controls.Add(tracksSpacerTop);
            content.Controls.Add(tracksHeader);
            content.Controls.Add(sepSpacer);
            content.Controls.Add(sep);
            content.Controls.Add(fileRow);

            var mainSpacer = new Panel { Height = 12, Dock = DockStyle.Bottom, BackColor = Color.Transparent };

            Controls.Add(content);
            Controls.Add(mainSpacer);
            Controls.Add(bottom);
            Controls.Add(header);
            Controls.Add(accentLine);

            ResumeLayout(true);
            this.Shown += delegate { this.ActiveControl = null; };
        }

        private void BrowseFile()
        {
            using (var dlg = new OpenFileDialog { Filter = "MIDI|*.mid;*.midi|All|*.*" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                StopPlay();
                _midiPath = dlg.FileName;
                _lblFile.Text = Path.GetFileName(_midiPath);
                _lblFile.ForeColor = Theme.Ink;
                try
                {
                    int bpm;
                    _trackInfos = MidiEngine.ParseMeta(_midiPath, out bpm);
                    _initialBpm = bpm;
                    _sliderBpm.Value = bpm;
                    _lblBpm.Text = "BPM " + bpm;
                    _tracksPanel.Controls.Clear();
                    _trackChecks.Clear();
                    foreach (var t in _trackInfos)
                    {
                        var cb = new DarkCheckBox
                        {
                            Text = "T" + t.Index + "  " + t.Name + "  \u00B7  " + t.InstrDesc + "  [" + t.NoteCount + "]",
                            Checked = true,
                            AutoSize = true,
                            Margin = new Padding(4, 3, 4, 3)
                        };
                        cb.CheckedChanged += delegate { OnTrackToggle(); };
                        _tracksPanel.Controls.Add(cb);
                        _trackChecks[t.Index] = cb;
                    }
                    OnTrackToggle();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not load MIDI: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SetAllTracks(bool val)
        {
            foreach (var cb in _trackChecks.Values) cb.Checked = val;
            OnTrackToggle();
        }

        private HashSet<int> GetActiveTracks()
        {
            var set = new HashSet<int>();
            foreach (var kv in _trackChecks) if (kv.Value.Checked) set.Add(kv.Key);
            return set;
        }

        private void OnTrackToggle()
        {
            if (_midiPath == null) return;
            var active = GetActiveTracks();
            if (active.Count > 0 && !_eng.Playing)
            {
                _eng.Load(_midiPath, active, (int)_sliderBpm.Value);
                int tm = (int)_eng.Duration / 60, ts = (int)_eng.Duration % 60;
                _lblTime.Text = "0:00 / " + tm + ":" + ts.ToString("D2");
            }
        }

        private void PlaySynth()
        {
            _btnSynth.Text = "Playing..."; _btnMacro.Enabled = false;
            _eng.Macro = false;
            BeginInvoke(new Action(StartPlayback));
        }

        private void PlayMacro()
        {
            _btnMacro.Text = "Active..."; _btnSynth.Enabled = false;
            _eng.Macro = true;
            BeginInvoke(new Action(StartPlayback));
        }

        private void StartPlayback()
        {
            var active = GetActiveTracks();
            if (_midiPath == null || active.Count == 0)
            {
                MessageBox.Show("Load a file and select at least one track.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ResetUI(); return;
            }
            _eng.Bpm = (int)_sliderBpm.Value;
            _eng.Volume = _sliderVol.Value / 100;
            if (_eng.Playing)
            {
                if (_eng.Paused)
                {
                    _eng.Start();
                    var btn = _eng.Macro ? _btnMacro : _btnSynth;
                    btn.Text = _eng.Macro ? "Active..." : "Playing...";
                    btn.BaseColor = Theme.Accent;
                    btn.Invalidate();
                }
                return;
            }
            if (_eng.Load(_midiPath, active, _eng.Bpm)) _eng.Start();
            else { MessageBox.Show("Failed to parse MIDI notes.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); ResetUI(); }
        }

        private void Pause()
        {
            var btn = _eng.Macro ? _btnMacro : _btnSynth;
            bool willPause = !_eng.Paused;
            if (willPause) { btn.Text = "Paused"; btn.BaseColor = Theme.Amber; }
            else { btn.Text = _eng.Macro ? "Active..." : "Playing..."; btn.BaseColor = Theme.Accent; }
            btn.Invalidate();
            _eng.TogglePause();
        }

        private void StopPlay() { ResetUI(); _eng.Stop(); }

        private void ResetUI()
        {
            _btnSynth.Text = "Synth"; _btnSynth.Enabled = true; _btnSynth.BaseColor = Theme.Accent; _btnSynth.Invalidate();
            _btnMacro.Text = "Macro"; _btnMacro.Enabled = true; _btnMacro.BaseColor = Theme.Accent; _btnMacro.Invalidate();
            _sliderProg.Value = 0;
            if (_eng.Duration > 0) { int tm = (int)_eng.Duration / 60, ts = (int)_eng.Duration % 60; _lblTime.Text = "0:00 / " + tm + ":" + ts.ToString("D2"); }
            else _lblTime.Text = "0:00 / 0:00";
        }

        private void OnProgress(double cur, double total)
        {
            if (InvokeRequired) { try { BeginInvoke(new Action<double, double>(OnProgress), cur, total); } catch { } return; }
            double pct = total > 0 ? cur / total : 0;
            _sliderProg.Value = pct;
            int cm = (int)cur / 60, cs = (int)cur % 60, tm = (int)total / 60, ts = (int)total % 60;
            _lblTime.Text = cm + ":" + cs.ToString("D2") + " / " + tm + ":" + ts.ToString("D2");
        }

        private void OnFinish()
        {
            if (InvokeRequired) { try { BeginInvoke(new Action(OnFinish)); } catch { } return; }
            ResetUI();
        }

        private void HotkeyLoop()
        {
            bool lastMacro = false;
            bool lastPause = false;
            while (true)
            {
                Thread.Sleep(50);
                string hkMac = "", hkPau = "";
                bool isMacFoc = false, isPauFoc = false;
                try { Invoke(new Action(delegate { hkMac = _entHotkey.Text; hkPau = _entPauseKey.Text; isMacFoc = _entHotkey.Focused; isPauFoc = _entPauseKey.Focused; })); } catch { continue; }

                int vkMac = Win32Input.GetVk(hkMac);
                int vkPau = Win32Input.GetVk(hkPau);

                bool curMac = (Win32Input.GetAsyncKeyState(vkMac) & 0x8000) != 0;
                bool curPau = (Win32Input.GetAsyncKeyState(vkPau) & 0x8000) != 0;

                if (isMacFoc) curMac = lastMacro;
                if (isPauFoc) curPau = lastPause;

                if (curMac && !lastMacro)
                {
                    if (_eng.Playing) try { BeginInvoke(new Action(StopPlay)); } catch { }
                    else if (_midiPath != null) try { BeginInvoke(new Action(PlayMacro)); } catch { }
                }

                if (curPau && !lastPause)
                {
                    if (_eng.Playing) try { BeginInvoke(new Action(Pause)); } catch { }
                }

                lastMacro = curMac;
                lastPause = curPau;
            }
        }

        private void CheckLayout()
        {
            try
            {
                IntPtr hwnd = Win32Input.GetForegroundWindow();
                uint tid = Win32Input.GetWindowThreadProcessId(hwnd, IntPtr.Zero);
                IntPtr lid = Win32Input.GetKeyboardLayout(tid);
                bool isEn = ((int)lid & 0xFF) == 0x09;
                _lblWarn.Text = isEn ? "" : "[!] Switch to EN layout";
            }
            catch { _lblWarn.Text = ""; }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Application.RemoveMessageFilter(this);
            base.OnFormClosed(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _eng.Cleanup();
            base.OnFormClosing(e);
        }
    }

}
