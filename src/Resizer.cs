// osu!taiko Video Resizer 2 - Ares
// based on the original tool by Khoo Hao Yit and Jerry
// C# 5 only - built by the csc.exe that ships with .NET Framework

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("osu!taiko Video Resizer 2")]
[assembly: AssemblyProduct("osu!taiko Video Resizer 2")]
[assembly: AssemblyCompany("Ares")]
[assembly: AssemblyCopyright("Made by Ares")]
[assembly: AssemblyDescription("Fits videos into the osu!taiko playfield. Made by Ares.")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace TaikoVideoResizer
{
    public class MainForm : Form
    {
        private ListBox lstFiles;
        private Button btnAdd;
        private Button btnRemove;
        private Button btnStart;
        private Button btnOpen;
        private ComboBox cmbLayout;
        private ComboBox cmbQuality;
        private ComboBox cmbSpeed;
        private ComboBox cmbFps;
        private NumericUpDown numBlur;
        private NumericUpDown numSize;
        private CheckBox chkSize;
        private CheckBox chkRanked;
        private CheckBox chkAnime;
        private RadioButton rbMp4;
        private RadioButton rbAvi;
        private RadioButton rbFlv;
        private ToolTip tips;
        private ProgressBar barFile;
        private Label lblStatus;
        private TextBox txtLog;

        private Thread worker;
        private volatile bool cancelling;
        private Process running;
        private string ffmpegPath;
        private string outputDir;

        public MainForm()
        {
            BuildUi();
            outputDir = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath), "output");
            Log("osu!taiko Video Resizer 2 - made by Ares");
            Log("Based on the original tool by Khoo Hao Yit and Jerry.");
            Log("");

            ffmpegPath = FindFfmpeg();
            if (ffmpegPath == null)
            {
                Log("ffmpeg.exe was not found.");
                Log("Put this program in the tool folder next to files\\ffmpeg\\ffmpeg.exe,");
                Log("or drop an ffmpeg.exe beside this program.");
                btnStart.Enabled = false;
            }
            else
            {
                Log("Using " + ffmpegPath);
                Log("Finished videos are written to: " + outputDir);
                Log("Drop video files onto the window, or use Add files.");
            }
        }

        // --- ui

        private void BuildUi()
        {
            Text = "osu!taiko Video Resizer 2";
            ClientSize = new Size(760, 590);
            MinimumSize = new Size(700, 550);
            AllowDrop = true;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            DragEnter += new DragEventHandler(OnDragEnter);
            DragDrop += new DragEventHandler(OnDragDrop);

            Label lblFiles = new Label();
            lblFiles.Text = "Videos to convert  (drag and drop anywhere on this window)";
            lblFiles.SetBounds(12, 10, 500, 18);
            Controls.Add(lblFiles);

            lstFiles = new ListBox();
            lstFiles.SetBounds(12, 30, 590, 120);
            lstFiles.SelectionMode = SelectionMode.MultiExtended;
            lstFiles.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lstFiles.AllowDrop = true;
            lstFiles.DragEnter += new DragEventHandler(OnDragEnter);
            lstFiles.DragDrop += new DragEventHandler(OnDragDrop);
            Controls.Add(lstFiles);

            btnAdd = new Button();
            btnAdd.Text = "Add files...";
            btnAdd.SetBounds(612, 30, 136, 26);
            btnAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAdd.Click += new EventHandler(OnAdd);
            Controls.Add(btnAdd);

            btnRemove = new Button();
            btnRemove.Text = "Remove selected";
            btnRemove.SetBounds(612, 62, 136, 26);
            btnRemove.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRemove.Click += new EventHandler(OnRemove);
            Controls.Add(btnRemove);

            GroupBox box = new GroupBox();
            box.Text = "Settings";
            box.SetBounds(12, 160, 736, 180);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(box);

            AddLabel(box, "Layout", 14, 26);
            cmbLayout = AddCombo(box, 100, 23, 250);
            cmbLayout.Items.Add("Centre (standard)");
            cmbLayout.Items.Add("Right side (for storyboard)");
            cmbLayout.SelectedIndex = 0;

            AddLabel(box, "Quality", 14, 56);
            cmbQuality = AddCombo(box, 100, 53, 250);
            cmbQuality.Items.Add("Best - CRF 18 (largest)");
            cmbQuality.Items.Add("High - CRF 20 (recommended)");
            cmbQuality.Items.Add("Balanced - CRF 23");
            cmbQuality.Items.Add("Small - CRF 26");
            cmbQuality.SelectedIndex = 1;

            AddLabel(box, "Encoder", 14, 86);
            cmbSpeed = AddCombo(box, 100, 83, 250);
            cmbSpeed.Items.Add("medium (fastest)");
            cmbSpeed.Items.Add("slow (recommended)");
            cmbSpeed.Items.Add("veryslow (smallest file)");
            cmbSpeed.SelectedIndex = 1;

            AddLabel(box, "Frame rate", 14, 116);
            cmbFps = AddCombo(box, 100, 113, 250);
            cmbFps.Items.Add("Cap at 30 fps (ranked default)");
            cmbFps.Items.Add("Cap at 60 fps");
            cmbFps.Items.Add("Keep source");
            cmbFps.SelectedIndex = 0;

            AddLabel(box, "Container", 14, 148);
            rbMp4 = AddRadio(box, "mp4", 100, 145, 62);
            rbAvi = AddRadio(box, "avi", 168, 145, 62);
            rbFlv = AddRadio(box, "flv", 236, 145, 62);
            rbMp4.Checked = true;

            tips = new ToolTip();
            tips.InitialDelay = 250;
            tips.ReshowDelay = 100;
            tips.AutoPopDelay = 20000;
            tips.SetToolTip(rbMp4,
                "Default, and what the osu! wiki compression guide produces.\r\n"
                + "Lightest container of the three.\r\n"
                + "Some clients crash or report \"Video playback failed\" on mp4\r\n"
                + "(osu-stable-issues #1038). It is inconsistent - most people\r\n"
                + "never see it.");
            tips.SetToolTip(rbAvi,
                "Fallback if osu! crashes on your mp4 files.\r\n"
                + "Heaviest container: about 1% more than mp4 (64 bytes per\r\n"
                + "frame against 14), which is a few hundred KB on a normal\r\n"
                + "video - not enough to matter.\r\n"
                + "Same H.264 video inside, so still fine for ranked.");
            tips.SetToolTip(rbFlv,
                "osu! plays it, but the format has been dead since Flash and\r\n"
                + "almost nobody uses it, so it is barely tested.\r\n"
                + "Slightly heavier than mp4, lighter than avi.\r\n"
                + "Worth trying only if both mp4 and avi fail on your machine.");

            AddLabel(box, "Blur", 390, 26);
            numBlur = new NumericUpDown();
            numBlur.SetBounds(470, 23, 70, 22);
            numBlur.Minimum = 0;
            numBlur.Maximum = 60;
            numBlur.Value = 10;
            box.Controls.Add(numBlur);
            AddLabel(box, "background blur, 0 = off", 548, 26);

            chkAnime = new CheckBox();
            chkAnime.Text = "Anime / cartoon source (x264 -tune animation)";
            chkAnime.SetBounds(390, 53, 330, 22);
            box.Controls.Add(chkAnime);

            chkSize = new CheckBox();
            chkSize.Text = "Limit file size to";
            chkSize.SetBounds(390, 83, 120, 22);
            chkSize.CheckedChanged += new EventHandler(OnSizeToggle);
            box.Controls.Add(chkSize);

            numSize = new NumericUpDown();
            numSize.SetBounds(516, 83, 70, 22);
            numSize.Minimum = 1;
            numSize.Maximum = 500;
            numSize.Value = 20;
            numSize.Enabled = false;
            box.Controls.Add(numSize);
            AddLabel(box, "MB  (two passes)", 594, 86);

            chkRanked = new CheckBox();
            chkRanked.Text = "osu! ranked limit";
            chkRanked.SetBounds(390, 113, 150, 22);
            chkRanked.CheckedChanged += new EventHandler(OnSizeToggle);
            box.Controls.Add(chkRanked);

            AddLabel(box, "fits the upload limit", 546, 116);

            btnStart = new Button();
            btnStart.Text = "Start";
            btnStart.SetBounds(12, 350, 110, 32);
            btnStart.Click += new EventHandler(OnStart);
            Controls.Add(btnStart);

            btnOpen = new Button();
            btnOpen.Text = "Open output folder";
            btnOpen.SetBounds(130, 350, 150, 32);
            btnOpen.Click += new EventHandler(OnOpenOutput);
            Controls.Add(btnOpen);

            barFile = new ProgressBar();
            barFile.SetBounds(288, 350, 460, 32);
            barFile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(barFile);

            lblStatus = new Label();
            lblStatus.Text = "Idle";
            lblStatus.SetBounds(12, 388, 736, 18);
            lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(lblStatus);

            txtLog = new TextBox();
            txtLog.SetBounds(12, 410, 736, 168);
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.BackColor = Color.White;
            txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(txtLog);
        }

        private void AddLabel(Control parent, string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.SetBounds(x, y, 220, 18);
            l.AutoSize = true;
            parent.Controls.Add(l);
        }

        private RadioButton AddRadio(Control parent, string text, int x, int y, int w)
        {
            RadioButton r = new RadioButton();
            r.Text = text;
            r.SetBounds(x, y, w, 22);
            parent.Controls.Add(r);
            return r;
        }

        private ComboBox AddCombo(Control parent, int x, int y, int w)
        {
            ComboBox c = new ComboBox();
            c.SetBounds(x, y, w, 22);
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            parent.Controls.Add(c);
            return c;
        }

        // --- events

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            AddFiles(paths);
        }

        private void OnAdd(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Multiselect = true;
            d.Title = "Choose video files";
            d.Filter = "Video files|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.flv;*.wmv;*.m4v;*.ts;*.mpg;*.mpeg|All files|*.*";
            if (d.ShowDialog(this) == DialogResult.OK)
                AddFiles(d.FileNames);
        }

        private void OnRemove(object sender, EventArgs e)
        {
            List<object> sel = new List<object>();
            foreach (object o in lstFiles.SelectedItems)
                sel.Add(o);
            foreach (object o in sel)
                lstFiles.Items.Remove(o);
        }

        private void OnSizeToggle(object sender, EventArgs e)
        {
            if (chkRanked.Checked) chkSize.Checked = false;
            numSize.Enabled = chkSize.Checked && !chkRanked.Checked;
            chkSize.Enabled = !chkRanked.Checked;
        }

        public void AddFiles(string[] paths)
        {
            if (paths == null) return;
            for (int i = 0; i < paths.Length; i++)
            {
                string p = paths[i];
                if (Directory.Exists(p))
                {
                    string[] inner = Directory.GetFiles(p);
                    AddFiles(inner);
                    continue;
                }
                if (!File.Exists(p)) continue;
                if (!lstFiles.Items.Contains(p))
                    lstFiles.Items.Add(p);
            }
        }

        private void OnStart(object sender, EventArgs e)
        {
            if (worker != null && worker.IsAlive)
            {
                cancelling = true;
                lblStatus.Text = "Cancelling...";
                try
                {
                    if (running != null && !running.HasExited)
                        running.Kill();
                }
                catch (Exception) { }
                return;
            }

            if (lstFiles.Items.Count == 0)
            {
                MessageBox.Show(this, "Add at least one video first.", "Nothing to do",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> files = new List<string>();
            foreach (object o in lstFiles.Items)
                files.Add(o.ToString());

            Job job = new Job();
            job.Files = files;
            job.RightSide = (cmbLayout.SelectedIndex == 1);
            job.Crf = QualityToCrf(cmbQuality.SelectedIndex);
            job.Preset = PresetName(cmbSpeed.SelectedIndex);
            job.Fps = FpsCap(cmbFps.SelectedIndex);
            job.Blur = (int)numBlur.Value;
            job.Anime = chkAnime.Checked;
            job.Ext = rbAvi.Checked ? ".avi" : (rbFlv.Checked ? ".flv" : ".mp4");
            job.LimitMb = chkSize.Checked ? (int)numSize.Value : 0;
            job.Ranked = chkRanked.Checked;

            cancelling = false;
            btnStart.Text = "Cancel";
            SetInputsEnabled(false);
            txtLog.Clear();

            worker = new Thread(new ParameterizedThreadStart(RunJob));
            worker.IsBackground = true;
            worker.Start(job);
        }

        private void SetInputsEnabled(bool on)
        {
            btnAdd.Enabled = on;
            btnRemove.Enabled = on;
            cmbLayout.Enabled = on;
            cmbSpeed.Enabled = on;
            cmbFps.Enabled = on;
            numBlur.Enabled = on;
            chkAnime.Enabled = on;
            rbMp4.Enabled = on;
            rbAvi.Enabled = on;
            rbFlv.Enabled = on;
            chkSize.Enabled = on;
            numSize.Enabled = on && chkSize.Checked && !chkRanked.Checked;
            chkRanked.Enabled = on;
            cmbQuality.Enabled = on;
            lstFiles.Enabled = on;
        }

        private static int QualityToCrf(int index)
        {
            if (index == 0) return 18;
            if (index == 2) return 23;
            if (index == 3) return 26;
            return 20;
        }

        private static string PresetName(int index)
        {
            if (index == 0) return "medium";
            if (index == 2) return "veryslow";
            return "slow";
        }

        private static int FpsCap(int index)
        {
            if (index == 0) return 30;
            if (index == 1) return 60;
            return 0;
        }

        // --- job

        private class Job
        {
            public List<string> Files;
            public bool RightSide;
            public int Crf;
            public string Preset;
            public int Fps;
            public int Blur;
            public bool Anime;
            public string Ext;
            public int LimitMb;
            public bool Ranked;
        }

        private void RunJob(object state)
        {
            Job job = (Job)state;
            int ok = 0;
            int failed = 0;

            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                Log("Could not create the output folder:");
                Log("   " + outputDir);
                Log("   " + ex.Message);
                Log("Move the program somewhere you can write to, such as your Desktop.");
                Status("Stopped - no output folder");
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        btnStart.Text = "Start";
                        SetInputsEnabled(true);
                    }));
                }
                catch (Exception) { }
                return;
            }

            for (int i = 0; i < job.Files.Count; i++)
            {
                if (cancelling) break;

                string input = job.Files[i];
                string output = MakeOutputPath(job.Ext);

                Status(string.Format(CultureInfo.InvariantCulture,
                    "[{0}/{1}] {2}", i + 1, job.Files.Count, Path.GetFileName(input)));
                Log("");
                Log("=== " + Path.GetFileName(input));

                ProbeInfo info = Probe(input);
                double duration = info.Duration;
                if (duration <= 0)
                    Log("   (could not read the duration - progress bar will not move)");

                // cap down only. forcing 24fps anime up to 30 = dup frames + judder
                int applyFps = 0;
                if (job.Fps > 0)
                {
                    if (info.Fps <= 0)
                    {
                        Log("   (could not read the frame rate - leaving it alone)");
                    }
                    else if (info.Fps > job.Fps + 0.01)
                    {
                        applyFps = job.Fps;
                        Log(string.Format(CultureInfo.InvariantCulture,
                            "   {0:0.###} fps source -> capped at {1} fps", info.Fps, job.Fps));
                    }
                    else
                    {
                        Log(string.Format(CultureInfo.InvariantCulture,
                            "   {0:0.###} fps source is already at or below the cap - kept as is", info.Fps));
                    }
                }

                double budgetMb = 0;
                if (duration > 0)
                {
                    if (job.Ranked)
                        budgetMb = RankedBudgetMb(duration);
                    else if (job.LimitMb > 0)
                        budgetMb = job.LimitMb;
                }
                else if (job.Ranked || job.LimitMb > 0)
                {
                    Log("   size limit ignored - the duration is needed to work out the bitrate");
                }

                // crf first - aiming at the budget would bloat small videos
                bool good = EncodeSinglePass(job, input, output, duration, applyFps);

                if (good && budgetMb > 0 && !cancelling)
                {
                    double sizeMb = FileSizeMb(output);

                    if (sizeMb > budgetMb)
                    {
                        Log(string.Format(CultureInfo.InvariantCulture,
                            "   {0:0.0} MB is over the {1:0.0} MB budget - re-encoding with two passes",
                            sizeMb, budgetMb));

                        double fpsUsed = applyFps > 0 ? applyFps : info.Fps;
                        double target = budgetMb - ContainerOverheadMb(job.Ext, duration, fpsUsed);
                        if (target < 0.5) target = 0.5;

                        good = EncodeTwoPass(job, input, output, duration, applyFps, target);

                        // x264 overshoots its own target at low bitrates, so check
                        if (good && !cancelling)
                        {
                            double after = FileSizeMb(output);
                            if (after > budgetMb && after > 0)
                            {
                                Log(string.Format(CultureInfo.InvariantCulture,
                                    "   still {0:0.00} MB over budget", after));
                                double tighter = target * (budgetMb / after) * 0.98;
                                if (tighter < 0.5) tighter = 0.5;
                                if (tighter < target * 0.98)
                                {
                                    target = tighter;
                                    good = EncodeTwoPass(job, input, output, duration, applyFps, target);
                                    after = FileSizeMb(output);
                                }
                            }
                            if (good && after > budgetMb)
                                Log(string.Format(CultureInfo.InvariantCulture,
                                    "   WARNING: could not get under the budget ({0:0.00} MB vs {1:0.00} MB). "
                                    + "Raise the CRF or cap the frame rate.", after, budgetMb));
                        }
                    }
                    else if (sizeMb > 0)
                    {
                        Log(string.Format(CultureInfo.InvariantCulture,
                            "   {0:0.0} MB fits the {1:0.0} MB budget", sizeMb, budgetMb));
                    }
                }

                if (cancelling)
                {
                    Log("   cancelled");
                    break;
                }

                if (good)
                {
                    ok++;
                    double mb = FileSizeMb(output);
                    if (mb > 0)
                        Log(string.Format(CultureInfo.InvariantCulture,
                            "   done -> {0}  ({1:0.0} MB)", Path.GetFileName(output), mb));
                    else
                        Log("   done -> " + Path.GetFileName(output));
                }
                else
                {
                    failed++;
                    Log("   FAILED");
                }
            }

            string summary;
            if (cancelling)
                summary = "Cancelled. " + ok + " finished.";
            else
                summary = "Finished. " + ok + " converted, " + failed + " failed.";

            Status(summary);
            Progress(0);
            try
            {
                BeginInvoke(new Action(delegate
                {
                    btnStart.Text = "Start";
                    SetInputsEnabled(true);
                }));
            }
            catch (Exception) { }
        }

        // 5MB + 10MB per minute, max 100MB, and that covers the whole set
        private double RankedBudgetMb(double seconds)
        {
            double allowance = 5.0 + 10.0 * (seconds / 60.0);
            if (allowance > 100.0) allowance = 100.0;
            double reserve = 1.5 * (seconds / 60.0) + 2.0;   // mp3 ~192kbps, bg, hitsounds
            double budget = allowance - reserve;
            if (budget < 2.0) budget = 2.0;
            Log(string.Format(CultureInfo.InvariantCulture,
                "   ranked: {0:0.0} MB allowed for the set, {1:0.0} MB reserved for audio/bg -> video budget {2:0.0} MB",
                allowance, reserve, budget));
            return budget;
        }

        // measured by remuxing one h264 stream: mp4 14 B/frame, flv 21, avi 64
        private static double ContainerOverheadMb(string ext, double seconds, double fps)
        {
            if (seconds <= 0) return 0;
            if (fps <= 0) fps = 30;
            double perFrame = 14.0;
            if (ext == ".avi") perFrame = 64.0;
            else if (ext == ".flv") perFrame = 21.0;
            return perFrame * fps * seconds / 1048576.0;
        }

        private static double FileSizeMb(string path)
        {
            try
            {
                FileInfo fi = new FileInfo(path);
                if (fi.Exists) return fi.Length / 1048576.0;
            }
            catch (Exception) { }
            return 0;
        }

        // always "output", numbered if taken so a batch does not eat itself
        private string MakeOutputPath(string ext)
        {
            string candidate = Path.Combine(outputDir, "output" + ext);
            int n = 2;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(outputDir,
                    "output (" + n.ToString(CultureInfo.InvariantCulture) + ")" + ext);
                n++;
            }
            return candidate;
        }

        private void OnOpenOutput(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(outputDir);
                Process.Start(outputDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not open the folder",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool EncodeSinglePass(Job job, string input, string output, double duration, int applyFps)
        {
            StringBuilder a = new StringBuilder();
            a.Append("-y -hide_banner -loglevel info -nostats -progress pipe:1 ");
            a.Append("-i ").Append(Q(input)).Append(' ');
            a.Append("-filter_complex ").Append(Q(BuildFilter(job, applyFps))).Append(' ');
            a.Append("-map \"[v]\" -an -map_metadata -1 ");
            a.Append(EncoderArgs(job)).Append(' ');
            a.Append("-crf ").Append(job.Crf).Append(' ');
            a.Append(Q(output));
            return Run(a.ToString(), duration, 0.0, 1.0);
        }

        private bool EncodeTwoPass(Job job, string input, string output, double duration, int applyFps, double budgetMb)
        {
            long bits = (long)(budgetMb * 1024.0 * 1024.0 * 8.0 * 0.97);
            int kbps = (int)(bits / duration / 1000.0);
            if (kbps < 100) kbps = 100;
            Log(string.Format(CultureInfo.InvariantCulture,
                "   target {0:0.0} MB -> {1} kbit/s, two passes", budgetMb, kbps));

            string passlog = Path.Combine(Path.GetTempPath(), "taikoresizer_" + Guid.NewGuid().ToString("N"));

            StringBuilder p1 = new StringBuilder();
            p1.Append("-y -hide_banner -loglevel info -nostats -progress pipe:1 ");
            p1.Append("-i ").Append(Q(input)).Append(' ');
            p1.Append("-filter_complex ").Append(Q(BuildFilter(job, applyFps))).Append(' ');
            p1.Append("-map \"[v]\" -an ");
            p1.Append(EncoderArgs(job)).Append(' ');
            p1.Append("-b:v ").Append(kbps).Append("k -pass 1 -passlogfile ").Append(Q(passlog)).Append(' ');
            p1.Append("-f null NUL");

            if (!Run(p1.ToString(), duration, 0.0, 0.5)) return false;
            if (cancelling) return false;

            StringBuilder p2 = new StringBuilder();
            p2.Append("-y -hide_banner -loglevel info -nostats -progress pipe:1 ");
            p2.Append("-i ").Append(Q(input)).Append(' ');
            p2.Append("-filter_complex ").Append(Q(BuildFilter(job, applyFps))).Append(' ');
            p2.Append("-map \"[v]\" -an -map_metadata -1 ");
            p2.Append(EncoderArgs(job)).Append(' ');
            p2.Append("-b:v ").Append(kbps).Append("k -pass 2 -passlogfile ").Append(Q(passlog)).Append(' ');
            p2.Append(Q(output));

            bool result = Run(p2.ToString(), duration, 0.5, 1.0);

            try
            {
                string[] leftovers = Directory.GetFiles(Path.GetTempPath(),
                    Path.GetFileName(passlog) + "*");
                for (int i = 0; i < leftovers.Length; i++)
                    File.Delete(leftovers[i]);
            }
            catch (Exception) { }

            return result;
        }

        private string EncoderArgs(Job job)
        {
            StringBuilder s = new StringBuilder();
            s.Append("-c:v libx264 -preset ").Append(job.Preset);
            if (job.Anime) s.Append(" -tune animation");
            s.Append(" -pix_fmt yuv420p -profile:v high -level 4.1");
            if (job.Ext == ".mp4") s.Append(" -movflags +faststart");
            s.Append(" -aspect 16:9");
            return s.ToString();
        }

        // layout numbers kept from the old tool so output still lines up
        private string BuildFilter(Job job, int applyFps)
        {
            StringBuilder s = new StringBuilder();
            s.Append("[0:v]format=yuv420p10le,split=2[bg][fg];");
            s.Append("[bg]scale=1280:340:force_original_aspect_ratio=increase:flags=lanczos,");
            // 334 at y=386, both even. pad snaps odd offsets in yuv420p and
            // leaves the strip 2 rows short of the bottom
            s.Append("crop=w=1280:h=334:x=(iw-1280)/2:y=(ih-340)/2");
            if (job.Blur > 0)
                s.Append(",gblur=sigma=").Append(job.Blur.ToString(CultureInfo.InvariantCulture));
            s.Append(",pad=1280:720:0:386:black[base];");

            if (job.RightSide)
            {
                s.Append("[fg]scale=1280:260:force_original_aspect_ratio=decrease:flags=lanczos[fgs];");
                s.Append("[base][fgs]overlay=x=(1280-900)/2+900-w-(333-h)/2:y=387+(333-h)/2");
            }
            else
            {
                s.Append("[fg]scale=1280:340:force_original_aspect_ratio=decrease:flags=lanczos[fgs];");
                s.Append("[base][fgs]overlay=(W-w)/2:387");
            }

            if (applyFps > 0)
                s.Append(",fps=").Append(applyFps.ToString(CultureInfo.InvariantCulture));

            s.Append("[v]");
            return s.ToString();
        }

        // --- ffmpeg

        private static string Q(string s)
        {
            return "\"" + s + "\"";
        }

        private string FindFfmpeg()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            string[] candidates = new string[]
            {
                Path.Combine(dir, "files\\ffmpeg\\ffmpeg.exe"),
                Path.Combine(dir, "ffmpeg.exe"),
                Path.Combine(dir, "..\\files\\ffmpeg\\ffmpeg.exe")
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                    return Path.GetFullPath(candidates[i]);
            }
            try
            {
                Process p = NewProcess("ffmpeg", "-version");
                p.Start();
                p.StandardError.ReadToEnd();
                p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode == 0) return "ffmpeg";
            }
            catch (Exception) { }
            return null;
        }

        private Process NewProcess(string exe, string args)
        {
            Process p = new Process();
            p.StartInfo.FileName = exe;
            p.StartInfo.Arguments = args;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            return p;
        }

        private class ProbeInfo
        {
            public double Duration;
            public double Fps;
        }

        private ProbeInfo Probe(string input)
        {
            ProbeInfo info = new ProbeInfo();
            info.Duration = 0;
            info.Fps = 0;
            try
            {
                Process p = NewProcess(ffmpegPath, "-hide_banner -i " + Q(input));
                p.Start();
                string err = p.StandardError.ReadToEnd();
                p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                Match m = Regex.Match(err, @"Duration:\s*(\d+):(\d\d):(\d\d(?:\.\d+)?)");
                if (m.Success)
                {
                    double h = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    double mm = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                    double ss = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                    info.Duration = h * 3600.0 + mm * 60.0 + ss;
                }

                Match v = Regex.Match(err, @"Stream #[^\n]*Video:[^\n]*");
                if (v.Success)
                {
                    Match f = Regex.Match(v.Value, @"(\d+(?:\.\d+)?)\s+fps");
                    if (f.Success)
                    {
                        double fps;
                        if (double.TryParse(f.Groups[1].Value, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out fps))
                            info.Fps = fps;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("   could not probe input: " + ex.Message);
            }
            return info;
        }

        private bool Run(string args, double duration, double from, double to)
        {
            try
            {
                Process p = NewProcess(ffmpegPath, args);
                List<string> tail = new List<string>();

                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    if (!e.Data.StartsWith("out_time=")) return;
                    if (duration <= 0) return;
                    double sec = ParseTime(e.Data.Substring(9));
                    if (sec < 0) return;
                    double frac = sec / duration;
                    if (frac > 1) frac = 1;
                    Progress((int)((from + (to - from) * frac) * 100));
                };

                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    string line = e.Data.Trim();
                    if (line.Length == 0) return;
                    lock (tail)
                    {
                        tail.Add(line);
                        if (tail.Count > 8) tail.RemoveAt(0);
                    }
                };

                running = p;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                int code = p.ExitCode;
                running = null;

                if (code != 0 && !cancelling)
                {
                    lock (tail)
                    {
                        for (int i = 0; i < tail.Count; i++)
                            Log("   " + tail[i]);
                    }
                }
                return code == 0;
            }
            catch (Exception ex)
            {
                Log("   error: " + ex.Message);
                running = null;
                return false;
            }
        }

        private static double ParseTime(string v)
        {
            v = v.Trim();
            if (v.Length == 0 || v == "N/A") return -1;
            string[] parts = v.Split(':');
            if (parts.Length != 3) return -1;
            double h, m, s;
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out h)) return -1;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out m)) return -1;
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out s)) return -1;
            return h * 3600.0 + m * 60.0 + s;
        }

        // --- helpers

        private void Log(string text)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(Log), new object[] { text }); }
                catch (Exception) { }
                return;
            }
            if (IsDisposed) return;
            txtLog.AppendText(text + Environment.NewLine);
        }

        private void Status(string text)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(Status), new object[] { text }); }
                catch (Exception) { }
                return;
            }
            if (IsDisposed) return;
            lblStatus.Text = text;
        }

        private void Progress(int percent)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<int>(Progress), new object[] { percent }); }
                catch (Exception) { }
                return;
            }
            if (IsDisposed) return;
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            barFile.Value = percent;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (worker != null && worker.IsAlive && e.CloseReason == CloseReason.UserClosing)
            {
                DialogResult answer = MessageBox.Show(this,
                    "A video is still being converted. Close anyway?",
                    "Still working", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                {
                    e.Cancel = true;
                    base.OnFormClosing(e);
                    return;
                }
            }

            cancelling = true;
            try
            {
                if (running != null && !running.HasExited)
                    running.Kill();
            }
            catch (Exception) { }
            base.OnFormClosing(e);
        }

        // --- main

        [STAThread]
        public static void Main(string[] argv)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm f = new MainForm();
            if (argv != null && argv.Length > 0)
                f.AddFiles(argv);
            Application.Run(f);
        }
    }
}
