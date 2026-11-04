using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCCheck
{
    /// <summary>
    /// メイン画面: 概要 / リアルタイム / セキュリティ の 3 タブ
    /// 메인 화면: 개요 / 실시간 / 보안 3개 탭
    /// </summary>
    public class MainForm : Form
    {
        public const string Version = "1.0.1";

        private readonly float S;   // 画面の拡大率（125% なら 1.25） / 화면 배율 (125%면 1.25)

        // ----- データ / 데이터 -----
        private OverviewData _ov;
        private SecurityData _sec;
        private DateTime _at;
        private List<ProcInfo> _procs = new List<ProcInfo>();
        private double _cpuNow;
        private Native.MEMORYSTATUSEX _mem;
        private readonly CpuSampler _cpuSampler = new CpuSampler();
        private readonly ProcSampler _procSampler = new ProcSampler();
        private bool _loading, _procBusy;
        private int _tick;

        // ----- 画面部品 / 화면 부품 -----
        private MenuStrip _menu;
        private ToolStripMenuItem _mFile, _mRefresh, _mCopy, _mCsv, _mExit, _mSettings, _mLang, _mJa, _mKo, _mHelp, _mAbout;
        private Label _lblHost, _lblSub, _lblBadge, _lblChecks, _lblDetails, _lblProcNote;
        private TabControl _tabs;
        private TabPage _tpOverview, _tpLive, _tpSecurity;
        private BufferedListView _lvOverview, _lvChecks, _lvSecurity, _lvProcs;
        private GraphPanel _gCpu, _gMem;
        private RadioButton _rbCpu, _rbMem;
        private Button _bRefresh, _bCopy, _bCsv;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _status;
        private ContextMenuStrip _ctx;
        private ToolStripMenuItem _ctxValue, _ctxRow;
        private readonly Timer _timer;

        public MainForm()
        {
            using (var g = CreateGraphics()) S = g.DpiX / 96f;
            AutoScaleMode = AutoScaleMode.None;   // 拡大率は自分で計算する / 배율은 직접 계산
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(Px(920), Px(700));
            MinimumSize = new Size(Px(720), Px(520));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildUi();
            ApplyLanguage();

            // 1 秒ごとに CPU・メモリを測る / 1초마다 CPU·메모리 측정
            _timer = new Timer { Interval = 1000 };
            _timer.Tick += (s, e) => OnTick();
            _timer.Start();
            OnTick();
        }

        private int Px(int v) { return (int)(v * S); }
        private static string T(string k) { return L.T(k); }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshAll();   // 画面を出してから情報を読み込む（待たされている感じを減らす）
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            base.OnFormClosed(e);
        }

        // =====================================================================
        // 画面の組み立て / 화면 구성
        // =====================================================================
        private void BuildUi()
        {
            // ----- メニュー / 메뉴 -----
            _mRefresh = new ToolStripMenuItem("", null, (s, e) => RefreshAll()) { ShortcutKeys = Keys.F5 };
            _mCopy = new ToolStripMenuItem("", null, (s, e) => CopyReport()) { ShortcutKeys = Keys.Control | Keys.Shift | Keys.C, Enabled = false };
            _mCsv = new ToolStripMenuItem("", null, (s, e) => SaveCsv()) { ShortcutKeys = Keys.Control | Keys.S, Enabled = false };
            _mExit = new ToolStripMenuItem("", null, (s, e) => Close());
            _mFile = new ToolStripMenuItem();
            _mFile.DropDownItems.AddRange(new ToolStripItem[] { _mRefresh, _mCopy, _mCsv, new ToolStripSeparator(), _mExit });

            // 言語名は切り替えても同じ表記のまま / 언어 이름은 전환해도 같은 표기 유지
            _mJa = new ToolStripMenuItem("日本語", null, (s, e) => SetLanguage("ja"));
            _mKo = new ToolStripMenuItem("한국어", null, (s, e) => SetLanguage("ko"));
            _mLang = new ToolStripMenuItem();
            _mLang.DropDownItems.AddRange(new ToolStripItem[] { _mJa, _mKo });
            _mSettings = new ToolStripMenuItem();
            _mSettings.DropDownItems.Add(_mLang);

            _mAbout = new ToolStripMenuItem("", null, (s, e) => ShowAbout());
            _mHelp = new ToolStripMenuItem();
            _mHelp.DropDownItems.Add(_mAbout);

            _menu = new MenuStrip();
            _menu.Items.AddRange(new ToolStripItem[] { _mFile, _mSettings, _mHelp });
            MainMenuStrip = _menu;

            // ----- ヘッダー（PC 名と判定バッジ） / 헤더 (PC 이름과 판정 배지) -----
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = Px(68),
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(Px(12), Px(6), Px(12), Px(6)),
                BackColor = Color.FromArgb(245, 247, 250),
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Px(200)));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

            _lblHost = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, AutoEllipsis = true };
            _lblSub = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true, ForeColor = Color.DimGray };
            _lblBadge = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(Px(4)), Cursor = Cursors.Hand };
            _lblBadge.Click += (s, e) => _tabs.SelectedTab = _tpSecurity;   // バッジを押すとセキュリティタブへ
            header.Controls.Add(_lblHost, 0, 0);
            header.Controls.Add(_lblSub, 0, 1);
            header.Controls.Add(_lblBadge, 1, 0);
            header.SetRowSpan(_lblBadge, 2);

            // ----- タブ / 탭 -----
            _tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(Px(14), Px(4)) };
            _tpOverview = new TabPage { Padding = new Padding(Px(6)) };
            _tpLive = new TabPage { Padding = new Padding(Px(6)) };
            _tpSecurity = new TabPage { Padding = new Padding(Px(6)) };

            // 概要タブ / 개요 탭
            _lvOverview = MakeList(true);
            _lvOverview.Columns.Add("", Px(220));
            _lvOverview.Columns.Add("", Px(620));
            _tpOverview.Controls.Add(_lvOverview);

            // リアルタイムタブ / 실시간 탭
            var live = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            live.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            live.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            live.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(170)));
            live.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            live.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _gCpu = new GraphPanel { Dock = DockStyle.Fill, Margin = new Padding(Px(2)), LineColor = Color.FromArgb(0, 120, 215) };
            _gMem = new GraphPanel { Dock = DockStyle.Fill, Margin = new Padding(Px(2)), LineColor = Color.FromArgb(136, 23, 152) };

            var opt = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = new Padding(0, Px(6), 0, Px(2)) };
            _rbCpu = new RadioButton { AutoSize = true, Checked = true };
            _rbMem = new RadioButton { AutoSize = true, Margin = new Padding(Px(10), Px(3), 0, 0) };
            _rbCpu.CheckedChanged += (s, e) => RenderProcs();
            _lblProcNote = new Label { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(Px(16), Px(6), 0, 0) };
            opt.Controls.AddRange(new Control[] { _rbCpu, _rbMem, _lblProcNote });

            _lvProcs = MakeList(false);
            _lvProcs.Columns.Add("", Px(300));
            _lvProcs.Columns.Add("", Px(70), HorizontalAlignment.Right);
            _lvProcs.Columns.Add("", Px(90), HorizontalAlignment.Right);
            _lvProcs.Columns.Add("", Px(120), HorizontalAlignment.Right);

            live.Controls.Add(_gCpu, 0, 0);
            live.Controls.Add(_gMem, 1, 0);
            live.Controls.Add(opt, 0, 1);
            live.SetColumnSpan(opt, 2);
            live.Controls.Add(_lvProcs, 0, 2);
            live.SetColumnSpan(_lvProcs, 2);
            _tpLive.Controls.Add(live);

            // セキュリティタブ: 上 = 判定、下 = 詳細 / 보안 탭: 위 = 판정, 아래 = 상세
            var sec = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            sec.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sec.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            sec.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sec.RowStyles.Add(new RowStyle(SizeType.Percent, 55));

            _lblChecks = new Label { AutoSize = true, Margin = new Padding(0, Px(2), 0, Px(4)) };
            _lvChecks = MakeList(false);
            _lvChecks.Columns.Add("", Px(100));
            _lvChecks.Columns.Add("", Px(220));
            _lvChecks.Columns.Add("", Px(520));
            _lblDetails = new Label { AutoSize = true, Margin = new Padding(0, Px(10), 0, Px(4)) };
            _lvSecurity = MakeList(true);
            _lvSecurity.Columns.Add("", Px(240));
            _lvSecurity.Columns.Add("", Px(600));

            sec.Controls.Add(_lblChecks, 0, 0);
            sec.Controls.Add(_lvChecks, 0, 1);
            sec.Controls.Add(_lblDetails, 0, 2);
            sec.Controls.Add(_lvSecurity, 0, 3);
            _tpSecurity.Controls.Add(sec);

            _tabs.TabPages.AddRange(new[] { _tpOverview, _tpLive, _tpSecurity });

            // ----- 下のボタン（右寄せ） / 아래 버튼 (오른쪽 정렬) -----
            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(Px(8), Px(4), Px(8), Px(4)),
            };
            _bCsv = MakeButton(SaveCsv);
            _bCopy = MakeButton(CopyReport);
            _bRefresh = MakeButton(RefreshAll);
            _bCsv.Enabled = _bCopy.Enabled = false;
            bottom.Controls.AddRange(new Control[] { _bCsv, _bCopy, _bRefresh });

            // ----- ステータスバー / 상태 표시줄 -----
            _statusStrip = new StatusStrip();
            _status = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _statusStrip.Items.Add(_status);

            // ----- 右クリックメニュー / 오른쪽 클릭 메뉴 -----
            _ctxValue = new ToolStripMenuItem("", null, (s, e) => CopyFromList(false));
            _ctxRow = new ToolStripMenuItem("", null, (s, e) => CopyFromList(true));
            _ctx = new ContextMenuStrip();
            _ctx.Items.AddRange(new ToolStripItem[] { _ctxValue, _ctxRow });
            _lvOverview.ContextMenuStrip = _ctx;
            _lvChecks.ContextMenuStrip = _ctx;
            _lvSecurity.ContextMenuStrip = _ctx;

            // Fill を最初に追加 → 上下に固定した部品の残りを埋める
            // Fill을 먼저 추가 → 위아래에 고정된 부품을 뺀 나머지를 채움
            Controls.Add(_tabs);
            Controls.Add(bottom);
            Controls.Add(_statusStrip);
            Controls.Add(header);
            Controls.Add(_menu);
        }

        private static BufferedListView MakeList(bool groups)
        {
            return new BufferedListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                ShowGroups = groups,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
            };
        }

        private Button MakeButton(Action onClick)
        {
            var b = new Button { AutoSize = true, Padding = new Padding(Px(8), Px(2), Px(8), Px(2)), Margin = new Padding(Px(4)) };
            b.Click += (s, e) => onClick();
            return b;
        }

        // =====================================================================
        // 言語の切り替え / 언어 전환
        // =====================================================================
        private void SetLanguage(string lang)
        {
            if (L.Lang == lang) return;
            L.Lang = lang;
            Settings.Save();      // 次回起動時も同じ言語で / 다음 실행 때도 같은 언어로
            ApplyLanguage();
        }

        /// <summary>画面のすべての文字を今の言語で設定し直す / 화면의 모든 문자를 현재 언어로 다시 설정</summary>
        private void ApplyLanguage()
        {
            string face = L.FontFace;
            Font = new Font(face, 9f);
            _menu.Font = _statusStrip.Font = _ctx.Font = Font;
            _lblHost.Font = new Font(face, 14f, FontStyle.Bold);
            _lblBadge.Font = new Font(face, 11f, FontStyle.Bold);
            _lblChecks.Font = _lblDetails.Font = new Font(face, 9.5f, FontStyle.Bold);

            Text = T("app.title");
            _mFile.Text = T("menu.file");
            _mRefresh.Text = T("menu.refresh");
            _mCopy.Text = T("menu.copy");
            _mCsv.Text = T("menu.csv");
            _mExit.Text = T("menu.exit");
            _mSettings.Text = T("menu.settings");
            _mLang.Text = T("menu.language");
            _mHelp.Text = T("menu.help");
            _mAbout.Text = T("menu.about");
            _mJa.Checked = L.Lang == "ja";
            _mKo.Checked = L.Lang == "ko";

            _tpOverview.Text = T("tab.overview");
            _tpLive.Text = T("tab.live");
            _tpSecurity.Text = T("tab.security");

            SetColumns(_lvOverview, "col.item", "col.value");
            SetColumns(_lvChecks, "col.status", "col.check", "col.detail");
            SetColumns(_lvSecurity, "col.item", "col.value");
            SetColumns(_lvProcs, "col.proc", "col.count", "col.cpu", "col.mem");

            _rbCpu.Text = T("live.sortCpu");
            _rbMem.Text = T("live.sortMem");
            _lblProcNote.Text = T("live.note");
            _gCpu.Title = T("live.cpuTitle");
            _gMem.Title = T("live.memTitle");
            _lblChecks.Text = T("sec.checks");
            _lblDetails.Text = T("sec.details");

            _bRefresh.Text = T("btn.refresh");
            _bCopy.Text = T("btn.copy");
            _bCsv.Text = T("btn.csv");
            _ctxValue.Text = T("ctx.value");
            _ctxRow.Text = T("ctx.row");

            RenderAll();   // データは取り直さず、表示だけ作り直す / 데이터는 다시 수집하지 않고 표시만 다시 만듦
        }

        private static void SetColumns(ListView lv, params string[] keys)
        {
            for (int i = 0; i < keys.Length && i < lv.Columns.Count; i++) lv.Columns[i].Text = L.T(keys[i]);
        }

        // =====================================================================
        // 情報の読み込み / 정보 읽기
        // =====================================================================
        private async void RefreshAll()
        {
            if (_loading) return;
            _loading = true;
            SetBusy(true);
            try
            {
                // WMI は時間がかかるので別スレッドで（画面が固まらないように）
                // WMI는 시간이 걸리므로 다른 스레드에서 (화면이 멈추지 않게)
                var tOv = Task.Run(() => Collector.CollectOverview());
                var tSec = Task.Run(() => Collector.CollectSecurity());
                await Task.WhenAll(tOv, tSec);
                _ov = tOv.Result;
                _sec = tSec.Result;
                _at = DateTime.Now;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.F("msg.error", ex.Message), T("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _loading = false;
                SetBusy(false);
                RenderAll();
            }
        }

        private void SetBusy(bool busy)
        {
            _bRefresh.Enabled = _mRefresh.Enabled = !busy;
            bool ready = !busy && _ov != null && _sec != null;
            _bCopy.Enabled = _bCsv.Enabled = _mCopy.Enabled = _mCsv.Enabled = ready;
            UseWaitCursor = busy;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (_loading) _status.Text = T("status.loading");
            else if (_ov != null) _status.Text = L.F("status.updated", _at.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture));
            else _status.Text = "";
        }

        // =====================================================================
        // 表示 / 표시
        // =====================================================================
        private void RenderAll()
        {
            List<Row> checks = (_ov != null && _sec != null) ? RowBuilder.Checks(_ov, _sec) : null;
            RenderHeader(checks);
            FillGrouped(_lvOverview, _ov == null ? null : RowBuilder.Overview(_ov));
            FillChecks(checks);
            FillGrouped(_lvSecurity, _sec == null ? null : RowBuilder.Security(_sec));
            RenderProcs();
            UpdateLiveText();
            UpdateStatus();
        }

        private void RenderHeader(List<Row> checks)
        {
            if (_ov == null || checks == null)
            {
                _lblHost.Text = Environment.MachineName;
                _lblSub.Text = T("status.loading");
                SetBadge(T("badge.loading"), Color.FromArgb(240, 240, 240), Color.DimGray);
                return;
            }

            _lblHost.Text = _ov.HostName;
            string os = _ov.OsName == null ? null : _ov.OsName.Replace("Microsoft ", "");
            _lblSub.Text = Fmt.Join("   |   ", Fmt.Join(" ", os, _ov.OsDisplayVersion), _ov.CpuName,
                                    "RAM " + Fmt.Bytes(RowBuilder.InstalledRam(_ov)));

            int alert = checks.Count(c => c.Status == Status.Alert);
            int warn = checks.Count(c => c.Status == Status.Warn);
            if (alert > 0)
                SetBadge(L.F("badge.alert", alert) + (warn > 0 ? "\n" + L.F("badge.warn", warn) : ""),
                         Color.FromArgb(253, 231, 233), Color.FromArgb(196, 43, 28));
            else if (warn > 0)
                SetBadge(L.F("badge.warn", warn), Color.FromArgb(255, 244, 206), Color.FromArgb(138, 85, 0));
            else
                SetBadge("✔ " + T("badge.ok"), Color.FromArgb(223, 246, 221), Color.FromArgb(16, 124, 16));
        }

        private void SetBadge(string text, Color back, Color fore)
        {
            _lblBadge.Text = text;
            _lblBadge.BackColor = back;
            _lblBadge.ForeColor = fore;
        }

        /// <summary>グループ付きの一覧を埋める / 그룹이 있는 목록 채우기</summary>
        private static void FillGrouped(ListView lv, List<Row> rows)
        {
            lv.BeginUpdate();
            lv.Items.Clear();
            lv.Groups.Clear();
            if (rows != null)
            {
                var groups = new Dictionary<string, ListViewGroup>();
                foreach (var r in rows)
                {
                    ListViewGroup g;
                    if (!groups.TryGetValue(r.Group, out g))
                    {
                        g = new ListViewGroup(r.Group);
                        lv.Groups.Add(g);
                        groups[r.Group] = g;
                    }
                    lv.Items.Add(new ListViewItem(new[] { r.Label, r.Value ?? "" }, g) { Tag = r });
                }
            }
            lv.EndUpdate();
        }

        /// <summary>判定一覧（要対応 = 赤、注意 = 黄）/ 판정 목록 (조치 필요 = 빨강, 주의 = 노랑)</summary>
        private void FillChecks(List<Row> rows)
        {
            _lvChecks.BeginUpdate();
            _lvChecks.Items.Clear();
            if (rows != null)
            {
                foreach (var r in rows)
                {
                    var it = new ListViewItem(new[] { RowBuilder.StatusText(r.Status), r.Label, r.Value }) { Tag = r };
                    switch (r.Status)
                    {
                        case Status.Alert:
                            it.BackColor = Color.FromArgb(253, 231, 233);
                            it.ForeColor = Color.FromArgb(164, 38, 44);
                            break;
                        case Status.Warn:
                            it.BackColor = Color.FromArgb(255, 244, 206);
                            it.ForeColor = Color.FromArgb(110, 70, 0);
                            break;
                        case Status.OK:
                            it.UseItemStyleForSubItems = false;
                            it.SubItems[0].ForeColor = Color.FromArgb(16, 124, 16);
                            break;
                    }
                    _lvChecks.Items.Add(it);
                }
            }
            _lvChecks.EndUpdate();
        }

        // =====================================================================
        // リアルタイム / 실시간
        // =====================================================================
        private void OnTick()
        {
            var c = _cpuSampler.Sample();
            if (c.HasValue) { _cpuNow = c.Value; _gCpu.Add(_cpuNow); }

            var m = new Native.MEMORYSTATUSEX();
            if (Native.GlobalMemoryStatusEx(m)) { _mem = m; _gMem.Add(m.dwMemoryLoad); }
            UpdateLiveText();

            // プロセス一覧は 2 秒ごと・別スレッドで / 프로세스 목록은 2초마다·다른 스레드에서
            if (_tick++ % 2 == 0 && !_procBusy)
            {
                _procBusy = true;
                var ui = TaskScheduler.FromCurrentSynchronizationContext();
                Task.Run(() => _procSampler.Sample()).ContinueWith(t =>
                {
                    _procBusy = false;
                    if (t.Status == TaskStatus.RanToCompletion) { _procs = t.Result; RenderProcs(); }
                }, ui);
            }
        }

        private void UpdateLiveText()
        {
            _gCpu.ValueText = _cpuNow.ToString("0", CultureInfo.InvariantCulture) + "%";
            if (_mem != null) _gMem.ValueText = RowBuilder.MemText(_mem);
        }

        private void RenderProcs()
        {
            if (_lvProcs == null) return;
            var sorted = _rbMem.Checked
                ? _procs.OrderByDescending(p => p.Memory)
                : _procs.OrderByDescending(p => p.Cpu).ThenByDescending(p => p.Memory);

            _lvProcs.BeginUpdate();
            _lvProcs.Items.Clear();
            foreach (var p in sorted.Take(15))
            {
                _lvProcs.Items.Add(new ListViewItem(new[]
                {
                    p.Name, p.Count.ToString(), p.Cpu.ToString("0.0", CultureInfo.InvariantCulture) + "%", Fmt.Bytes(p.Memory),
                }));
            }
            _lvProcs.EndUpdate();
        }

        // =====================================================================
        // 出力（レポートコピー・CSV） / 출력 (리포트 복사·CSV)
        // =====================================================================
        private List<Row> LiveRows() { return RowBuilder.Live(_cpuNow, _mem, _procs); }

        private void CopyReport()
        {
            if (_ov == null || _sec == null) return;
            try
            {
                string text = Report.Text(_at, RowBuilder.Checks(_ov, _sec), RowBuilder.Overview(_ov),
                                          RowBuilder.Security(_sec), LiveRows());
                Clipboard.SetText(text);
                _status.Text = T("status.copied");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.F("msg.error", ex.Message), T("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveCsv()
        {
            if (_ov == null || _sec == null) return;
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = T("dlg.csv");
                dlg.FileName = "PCCheck_" + _ov.HostName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string csv = Report.Csv(RowBuilder.Checks(_ov, _sec), RowBuilder.Overview(_ov),
                                            RowBuilder.Security(_sec), LiveRows());
                    // BOM 付き UTF-8 → Excel でダブルクリックしても文字化けしない
                    // BOM 포함 UTF-8 → Excel에서 더블클릭해도 글자가 깨지지 않음
                    File.WriteAllText(dlg.FileName, csv, new UTF8Encoding(true));
                    _status.Text = L.F("status.saved", dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, L.F("msg.saveFail", ex.Message), T("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        // 右クリック → 値 / 行 をコピー / 오른쪽 클릭 → 값 / 행 복사
        private void CopyFromList(bool wholeRow)
        {
            var lv = _ctx.SourceControl as ListView;
            if (lv == null || lv.SelectedItems.Count == 0) return;
            var it = lv.SelectedItems[0];
            string text = wholeRow
                ? string.Join("\t", it.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text))
                : it.SubItems[it.SubItems.Count - 1].Text;
            try { Clipboard.SetText(text); } catch { }
        }

        private void ShowAbout()
        {
            MessageBox.Show(this, L.F("about.text", Version), T("menu.about"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
