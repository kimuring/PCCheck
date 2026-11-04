using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PCCheck
{
    /// <summary>
    /// 集めたデータ → 画面に出す行（現在の言語で）に変換し、判定もここで行う
    /// 수집한 데이터 → 화면에 표시할 행(현재 언어로)으로 변환하고, 판정도 여기서 함
    /// データと表示を分けておくと、言語を切り替えたときに再取得せず表示だけ作り直せる
    /// 데이터와 표시를 분리해두면, 언어를 바꿀 때 다시 수집하지 않고 표시만 새로 만들 수 있음
    /// </summary>
    internal static class RowBuilder
    {
        // ===== 判定の基準 / 판정 기준 =====
        public const int DefWarnDays = 3, DefAlertDays = 7;        // 定義ファイル / 정의 파일
        public const int WuWarnDays = 35, WuAlertDays = 60;        // Windows Update（月1回の更新が基本）
        public const int DiskWarnPct = 20, DiskAlertPct = 10;      // ディスク空き容量 / 디스크 여유 공간
        public const int UptimeWarnDays = 14;                      // 再起動していない日数 / 재부팅 안 한 일수

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static string T(string k) { return L.T(k); }

        // ================= 概要 / 개요 =================
        public static List<Row> Overview(OverviewData d)
        {
            var r = new List<Row>();

            string g = T("g.system");
            r.Add(new Row(g, T("o.host"), d.HostName));
            r.Add(new Row(g, T("o.user"), d.UserName));
            r.Add(new Row(g, T("o.model"), Or(Fmt.Join(" ", d.Maker, d.Model))));
            r.Add(new Row(g, T("o.os"), Or(d.OsName)));
            r.Add(new Row(g, T("o.osver"), Or(Fmt.Join(" / ", d.OsDisplayVersion,
                d.OsBuild != null ? T("o.build") + " " + d.OsBuild : null, d.OsArch))));
            if (d.InstallDate.HasValue) r.Add(new Row(g, T("o.install"), Fmt.Date(d.InstallDate.Value)));
            r.Add(new Row(g, T("o.boot"), Fmt.DateTime(DateTime.Now - d.Uptime)));
            r.Add(new Row(g, T("o.uptime"), L.F("fmt.uptime", (int)d.Uptime.TotalDays, d.Uptime.Hours, d.Uptime.Minutes)));
            if (d.FastStartup.HasValue) r.Add(new Row(g, T("o.fast"), OnOff(d.FastStartup)));

            g = "CPU";
            r.Add(new Row(g, T("o.name"), Or(d.CpuName)));
            if (d.CpuCores > 0) r.Add(new Row(g, T("o.cores"), L.F("fmt.cores", d.CpuCores, d.CpuThreads)));
            if (d.CpuSockets > 1) r.Add(new Row(g, T("o.sockets"), d.CpuSockets.ToString()));
            if (d.CpuMaxMHz > 0) r.Add(new Row(g, T("o.clock"), (d.CpuMaxMHz / 1000.0).ToString("0.00", Inv) + " GHz"));
            if (d.L2KB > 0 || d.L3KB > 0)
                r.Add(new Row(g, T("o.cache"), Fmt.Join(" / ",
                    d.L2KB > 0 ? "L2 " + Fmt.Bytes(d.L2KB * 1024.0) : null,
                    d.L3KB > 0 ? "L3 " + Fmt.Bytes(d.L3KB * 1024.0) : null)));

            g = T("g.memory");
            string type = d.Modules.Select(m => MemType(m.Type)).FirstOrDefault(x => x != null);
            r.Add(new Row(g, T("o.memtotal"), Fmt.Join(" ", Fmt.Bytes(InstalledRam(d)), type != null ? "(" + type + ")" : null)));
            if (d.RamSlotsTotal > 0) r.Add(new Row(g, T("o.slots"), d.Modules.Count + " / " + d.RamSlotsTotal));
            foreach (var m in d.Modules)
            {
                string t = MemType(m.Type);
                string spec = Fmt.Join(" ", Fmt.Bytes(m.Capacity),
                    t != null ? t + (m.Speed > 0 ? "-" + m.Speed : "") : (m.Speed > 0 ? m.Speed + " MT/s" : null));
                r.Add(new Row(g, string.IsNullOrWhiteSpace(m.Slot) ? T("o.module") : m.Slot,
                              Fmt.Join(" / ", spec, m.Maker, m.Part)));
            }

            g = T("g.gpu");
            for (int i = 0; i < d.Gpus.Count; i++)
            {
                var gpu = d.Gpus[i];
                r.Add(new Row(g, d.Gpus.Count > 1 ? "GPU " + (i + 1) : "GPU", Fmt.Join(" / ", gpu.Name,
                    gpu.Driver != null ? T("o.driver") + " " + gpu.Driver : null, gpu.Mode)));
            }

            g = T("g.board");
            r.Add(new Row(g, T("o.board"), Or(d.Board)));
            r.Add(new Row(g, "BIOS", Or(d.Bios)));

            g = T("g.storage");
            foreach (var v in d.Volumes)
            {
                int pct = FreePct(v);
                string label = v.Drive + (string.IsNullOrWhiteSpace(v.Label) ? "" : " (" + v.Label + ")");
                r.Add(new Row(g, label, L.F("fmt.free", Fmt.Bytes(v.Free), Fmt.Bytes(v.Size), pct) + " - " + v.Format));
            }
            for (int i = 0; i < d.Disks.Count; i++)
            {
                var p = d.Disks[i];
                r.Add(new Row(g, L.F("o.disk", i + 1), Fmt.Join(" / ", p.Model, Fmt.Join(" ", p.Media, p.Bus),
                    p.Size > 0 ? Fmt.Bytes(p.Size) : null)));
            }

            g = T("g.network");
            if (d.Adapters.Count == 0) r.Add(new Row(g, "-", T("o.nonet")));
            foreach (var a in d.Adapters)
            {
                r.Add(new Row(g, a.Name, Fmt.Join(" / ",
                    string.IsNullOrEmpty(a.IPv4) ? null : "IPv4 " + a.IPv4,
                    a.Mac != null ? "MAC " + a.Mac : null,
                    a.Speed > 0 ? LinkSpeed(a.Speed) : null)));
            }
            return r;
        }

        // ================= セキュリティ詳細 / 보안 상세 =================
        public static List<Row> Security(SecurityData s)
        {
            var r = new List<Row>();

            string g = T("g.av");
            if (!s.SecurityCenterOk) r.Add(new Row(g, T("s.state"), T("s.scna")));
            else if (s.Products.Count == 0) r.Add(new Row(g, T("s.state"), T("s.noav")));
            foreach (var p in s.Products)
            {
                r.Add(new Row(g, p.Name, Fmt.Join(" / ",
                    p.Enabled ? T("s.enabled") : T("s.disabled"),
                    p.UpToDate ? T("s.uptodate") : T("s.outdated"),
                    p.Version != null ? T("s.version") + " " + p.Version : null)));
            }

            g = "Microsoft Defender";
            var df = s.Defender;
            var others = s.Products.Where(p => p.Enabled && !p.IsDefender).Select(p => p.Name).ToList();
            if (!df.Available) r.Add(new Row(g, T("s.state"), T("d.na")));
            else if (!df.IsActive)
            {
                // 他社製ソフトが有効なら Defender が止まっているのは正常
                // 타사 백신이 켜져 있으면 Defender가 꺼져 있는 건 정상
                r.Add(new Row(g, T("s.state"), others.Count > 0 ? L.F("d.passive", string.Join(", ", others)) : T("d.stopped")));
                if (df.RunningMode != null) r.Add(new Row(g, T("d.mode"), df.RunningMode));
                if (df.ProductVersion != null) r.Add(new Row(g, T("d.product"), df.ProductVersion));
            }
            else
            {
                r.Add(new Row(g, T("d.av"), OnOff(df.AvEnabled)));
                r.Add(new Row(g, T("d.realtime"), OnOff(df.RealTime)));
                if (df.RunningMode != null) r.Add(new Row(g, T("d.mode"), df.RunningMode));
                r.Add(new Row(g, T("d.product"), Or(df.ProductVersion)));
                r.Add(new Row(g, T("d.engine"), Or(df.EngineVersion)));
                r.Add(new Row(g, T("d.sig"), Or(df.SignatureVersion)));
                if (df.SignatureUpdated.HasValue)
                    r.Add(new Row(g, T("d.sigdate"), L.F("fmt.withAgo", Fmt.DateTime(df.SignatureUpdated.Value), Ago(df.SignatureAgeDays ?? 0))));
            }

            g = "Windows Update";
            var u = s.Update;
            if (u.LastInstall.HasValue)
            {
                r.Add(new Row(g, T("u.last"), L.F("fmt.withAgo", Fmt.DateTime(u.LastInstall.Value), Ago(DaysSince(u.LastInstall.Value)))));
                if (!string.IsNullOrWhiteSpace(u.Title)) r.Add(new Row(g, T("u.title"), u.Title));
            }
            else r.Add(new Row(g, T("u.last"), T("na")));

            return r;
        }

        // ================= チェック（判定） / 점검(판정) =================
        public static List<Row> Checks(OverviewData d, SecurityData s)
        {
            var r = new List<Row>();
            string g = T("sec.checks");
            var df = s.Defender;
            var active = s.Products.Where(p => p.Enabled).ToList();

            // 1) セキュリティソフトが有効か / 보안 소프트웨어가 켜져 있는지
            string cAv = T("c.av");
            if (active.Count > 0)
                r.Add(new Row(g, cAv, L.F("c.av.ok", string.Join(", ", active.Select(p => p.Name))), Status.OK));
            else if (!s.SecurityCenterOk && df.IsActive)
                r.Add(new Row(g, cAv, L.F("c.av.ok", "Microsoft Defender"), Status.OK));
            else if (!s.SecurityCenterOk)
                r.Add(new Row(g, cAv, T("c.unknown"), Status.Warn));
            else
                r.Add(new Row(g, cAv, T("c.av.none"), Status.Alert));

            // 2) 定義ファイルが新しいか / 정의 파일이 최신인지
            string cDef = T("c.def");
            var outdated = active.Where(p => !p.UpToDate).ToList();
            if (outdated.Count > 0)
                r.Add(new Row(g, cDef, L.F("c.def.outdated", string.Join(", ", outdated.Select(p => p.Name))), Status.Alert));
            else if (df.IsActive && df.SignatureAgeDays.HasValue)
            {
                int age = df.SignatureAgeDays.Value;
                if (age > DefAlertDays) r.Add(new Row(g, cDef, L.F("c.def.old", age), Status.Alert));
                else if (age > DefWarnDays) r.Add(new Row(g, cDef, L.F("c.def.old", age), Status.Warn));
                else r.Add(new Row(g, cDef, L.F("c.def.ok", Ago(age)), Status.OK));
            }
            else if (active.Count > 0)
                r.Add(new Row(g, cDef, T("c.def.okSc"), Status.OK));

            // 3) リアルタイム保護（Defender がメインのときだけ判定できる）
            // 3) 실시간 보호 (Defender가 메인일 때만 판정 가능)
            if (df.IsActive)
            {
                bool on = df.RealTime == true;
                r.Add(new Row(g, T("c.rt"), on ? T("c.rt.ok") : T("c.rt.off"), on ? Status.OK : Status.Alert));
            }

            // 4) Windows Update
            string cWu = T("c.wu");
            if (s.Update.LastInstall.HasValue)
            {
                int days = DaysSince(s.Update.LastInstall.Value);
                if (days > WuAlertDays) r.Add(new Row(g, cWu, L.F("c.wu.old", days), Status.Alert));
                else if (days > WuWarnDays) r.Add(new Row(g, cWu, L.F("c.wu.old", days), Status.Warn));
                else r.Add(new Row(g, cWu, L.F("c.wu.ok", Ago(days)), Status.OK));
            }
            else r.Add(new Row(g, cWu, T("c.wu.unknown"), Status.Warn));

            // 5) ディスク空き容量 / 디스크 여유 공간
            foreach (var v in d.Volumes)
            {
                int pct = FreePct(v);
                var st = pct < DiskAlertPct ? Status.Alert : pct < DiskWarnPct ? Status.Warn : Status.OK;
                r.Add(new Row(g, L.F("c.disk", v.Drive), L.F("c.disk.detail", pct, Fmt.Bytes(v.Free)), st));
            }

            // 6) 長期間再起動していないか / 오랫동안 재부팅하지 않았는지
            int up = (int)d.Uptime.TotalDays;
            if (up >= UptimeWarnDays)
                r.Add(new Row(g, T("c.uptime"), L.F(d.FastStartup == true ? "c.uptime.longFast" : "c.uptime.long", up), Status.Warn));
            else r.Add(new Row(g, T("c.uptime"), L.F("c.uptime.ok", up), Status.OK));

            // 深刻なものを上に（同じ深刻度なら元の順番のまま） / 심각한 것을 위로 (같은 심각도면 원래 순서 유지)
            return r.OrderByDescending(x => (int)x.Status).ToList();
        }

        // ================= 現在の使用状況（レポート用） / 현재 사용 현황 (리포트용) =================
        public static List<Row> Live(double cpu, Native.MEMORYSTATUSEX mem, List<ProcInfo> procs)
        {
            var r = new List<Row>();
            string g = T("r.usage");
            r.Add(new Row(g, T("live.cpuTitle"), cpu.ToString("0", Inv) + "%"));
            if (mem != null && mem.ullTotalPhys > 0) r.Add(new Row(g, T("live.memTitle"), MemText(mem)));

            g = T("r.topmem");
            int i = 1;
            foreach (var p in procs.OrderByDescending(p => p.Memory).Take(5))
                r.Add(new Row(g, (i++) + ". " + ProcLabel(p), Fmt.Bytes(p.Memory)));

            g = T("r.topcpu");
            i = 1;
            foreach (var p in procs.OrderByDescending(p => p.Cpu).Take(5))
                r.Add(new Row(g, (i++) + ". " + ProcLabel(p), p.Cpu.ToString("0.0", Inv) + "%"));
            return r;
        }

        // ================= 小さな道具 / 작은 도구 =================
        public static string StatusText(Status s)
        {
            switch (s)
            {
                case Status.OK: return "OK";
                case Status.Warn: return T("st.warn");
                case Status.Alert: return T("st.alert");
                default: return "";
            }
        }

        public static string MemText(Native.MEMORYSTATUSEX m)
        {
            return Fmt.Bytes(m.ullTotalPhys - m.ullAvailPhys) + " / " + Fmt.Bytes(m.ullTotalPhys) + " (" + m.dwMemoryLoad + "%)";
        }

        public static string ProcLabel(ProcInfo p)
        {
            return p.Count > 1 ? p.Name + " (×" + p.Count + ")" : p.Name;
        }

        /// <summary>実際に挿さっているメモリの合計（取れなければ OS が認識している量）</summary>
        public static ulong InstalledRam(OverviewData d)
        {
            ulong sum = 0;
            foreach (var m in d.Modules) sum += m.Capacity;
            return sum > 0 ? sum : d.RamTotal;
        }

        private static int FreePct(Volume v) { return v.Size == 0 ? 0 : (int)(v.Free * 100 / v.Size); }
        private static int DaysSince(DateTime t) { return Math.Max(0, (int)(DateTime.Now - t).TotalDays); }
        private static string Ago(int days) { return days <= 0 ? T("ago.today") : L.F("ago.days", days); }
        private static string Or(string s) { return string.IsNullOrWhiteSpace(s) ? T("unknown") : s; }
        private static string OnOff(bool? b) { return b == null ? T("unknown") : (b.Value ? T("s.on") : T("s.off")); }

        private static string LinkSpeed(long bps)
        {
            return bps >= 1000000000 ? (bps / 1e9).ToString("0.#", Inv) + " Gbps" : (bps / 1e6).ToString("0", Inv) + " Mbps";
        }

        // SMBIOS のメモリ種類番号 → 名前 / SMBIOS 메모리 종류 번호 → 이름
        private static string MemType(int t)
        {
            switch (t)
            {
                case 20: return "DDR"; case 21: return "DDR2"; case 24: return "DDR3";
                case 26: return "DDR4"; case 34: return "DDR5";
                case 28: return "LPDDR2"; case 29: return "LPDDR3"; case 30: return "LPDDR4"; case 35: return "LPDDR5";
                default: return null;
            }
        }
    }
}
