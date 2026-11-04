using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;

namespace PCCheck
{
    /// <summary>
    /// WMI（Windows の管理情報）を読むための小さな道具
    /// WMI(Windows 관리 정보)를 읽기 위한 작은 도구
    /// </summary>
    internal static class Wmi
    {
        /// <summary>取得できなかったら null を返す / 못 가져오면 null 반환</summary>
        public static List<ManagementObject> Query(string ns, string wql)
        {
            try
            {
                var list = new List<ManagementObject>();
                using (var s = new ManagementObjectSearcher(ns, wql))
                {
                    foreach (ManagementObject o in s.Get()) list.Add(o);
                }
                return list;
            }
            catch { return null; }
        }

        public static List<ManagementObject> Cim(string cls)
        {
            return Query(@"root\cimv2", "SELECT * FROM " + cls) ?? new List<ManagementObject>();
        }

        // プロパティが存在しない古い Windows でも落ちないように全部 try で囲む
        // 속성이 없는 구버전 Windows에서도 죽지 않게 전부 try로 감쌈
        public static string Str(ManagementBaseObject o, string p)
        {
            try { var v = o[p]; return v == null ? null : v.ToString().Trim(); } catch { return null; }
        }

        public static ulong U64(ManagementBaseObject o, string p)
        {
            try { var v = o[p]; return v == null ? 0 : Convert.ToUInt64(v); } catch { return 0; }
        }

        public static bool? Bool(ManagementBaseObject o, string p)
        {
            try { var v = o[p]; return v == null ? (bool?)null : Convert.ToBoolean(v); } catch { return null; }
        }

        public static DateTime? Date(ManagementBaseObject o, string p)
        {
            var s = Str(o, p);
            if (string.IsNullOrEmpty(s)) return null;
            try { return ManagementDateTimeConverter.ToDateTime(s); } catch { return null; }
        }
    }

    /// <summary>PC の情報を集める / PC 정보를 수집</summary>
    internal static class Collector
    {
        // ================= 概要 / 개요 =================
        public static OverviewData CollectOverview()
        {
            var d = new OverviewData
            {
                HostName = Environment.MachineName,
                UserName = Environment.UserName,
                Uptime = TimeSpan.FromMilliseconds(Native.GetTickCount64()),
            };

            // --- OS ---
            var os = Wmi.Cim("Win32_OperatingSystem").FirstOrDefault();
            if (os != null)
            {
                d.OsName = Wmi.Str(os, "Caption");
                d.OsBuild = Wmi.Str(os, "BuildNumber");
                d.OsArch = Wmi.Str(os, "OSArchitecture");
                d.InstallDate = Wmi.Date(os, "InstallDate");
            }
            // 「24H2」のような表示バージョンと細かいビルド番号(UBR)はレジストリにある
            // "24H2" 같은 표시 버전과 세부 빌드 번호(UBR)는 레지스트리에 있음
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var k = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (k != null)
                    {
                        d.OsDisplayVersion = k.GetValue("DisplayVersion") as string;
                        var ubr = k.GetValue("UBR");
                        if (ubr != null && d.OsBuild != null) d.OsBuild += "." + ubr;
                    }
                }
            }
            catch { }

            // --- 高速スタートアップ: 有効だと「シャットダウン」では稼働時間がリセットされない ---
            // --- 빠른 시작: 켜져 있으면 "시스템 종료"로는 가동 시간이 초기화되지 않음 ---
            // 条件: HiberbootEnabled = 1 かつ 休止状態が無効にされていない
            // 조건: HiberbootEnabled = 1 이고 최대 절전이 꺼져 있지 않음
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var sm = hklm.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power"))
                using (var pw = hklm.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power"))
                {
                    var hiber = sm == null ? null : sm.GetValue("HiberbootEnabled");
                    var hibernate = pw == null ? null : pw.GetValue("HibernateEnabled");
                    if (hiber != null)
                        d.FastStartup = Convert.ToInt32(hiber) == 1 && (hibernate == null || Convert.ToInt32(hibernate) == 1);
                }
            }
            catch { }

            // --- 本体 / 본체 ---
            var cs = Wmi.Cim("Win32_ComputerSystem").FirstOrDefault();
            if (cs != null)
            {
                d.Maker = Wmi.Str(cs, "Manufacturer");
                d.Model = Wmi.Str(cs, "Model");
                d.RamTotal = Wmi.U64(cs, "TotalPhysicalMemory");
            }

            // --- CPU ---
            var cpus = Wmi.Cim("Win32_Processor");
            if (cpus.Count > 0)
            {
                d.CpuName = Wmi.Str(cpus[0], "Name");
                d.CpuSockets = cpus.Count;
                d.CpuCores = cpus.Sum(c => (int)Wmi.U64(c, "NumberOfCores"));
                d.CpuThreads = cpus.Sum(c => (int)Wmi.U64(c, "NumberOfLogicalProcessors"));
                d.CpuMaxMHz = (int)Wmi.U64(cpus[0], "MaxClockSpeed");
                d.L2KB = (uint)Wmi.U64(cpus[0], "L2CacheSize");
                d.L3KB = (uint)Wmi.U64(cpus[0], "L3CacheSize");
            }

            // --- メモリ / 메모리 ---
            foreach (var m in Wmi.Cim("Win32_PhysicalMemory"))
            {
                uint speed = (uint)Wmi.U64(m, "ConfiguredClockSpeed");
                if (speed == 0) speed = (uint)Wmi.U64(m, "Speed");
                d.Modules.Add(new MemModule
                {
                    Capacity = Wmi.U64(m, "Capacity"),
                    Speed = speed,
                    Type = (int)Wmi.U64(m, "SMBIOSMemoryType"),
                    Maker = Wmi.Str(m, "Manufacturer"),
                    Part = Wmi.Str(m, "PartNumber"),
                    Slot = Wmi.Str(m, "DeviceLocator"),
                });
            }
            d.RamSlotsTotal = Wmi.Cim("Win32_PhysicalMemoryArray").Sum(a => (int)Wmi.U64(a, "MemoryDevices"));

            // --- GPU ---
            foreach (var g in Wmi.Cim("Win32_VideoController"))
            {
                ulong w = Wmi.U64(g, "CurrentHorizontalResolution"), h = Wmi.U64(g, "CurrentVerticalResolution");
                ulong hz = Wmi.U64(g, "CurrentRefreshRate");
                d.Gpus.Add(new GpuInfo
                {
                    Name = Wmi.Str(g, "Name"),
                    Driver = Wmi.Str(g, "DriverVersion"),
                    Mode = w > 0 ? string.Format("{0} x {1} @ {2}Hz", w, h, hz) : null,
                });
            }

            // --- マザーボード・BIOS / 메인보드·BIOS ---
            var bb = Wmi.Cim("Win32_BaseBoard").FirstOrDefault();
            if (bb != null) d.Board = Fmt.Join(" ", Wmi.Str(bb, "Manufacturer"), Wmi.Str(bb, "Product"));
            var bios = Wmi.Cim("Win32_BIOS").FirstOrDefault();
            if (bios != null)
            {
                var rel = Wmi.Date(bios, "ReleaseDate");
                d.Bios = Fmt.Join(" ", Wmi.Str(bios, "Manufacturer"), Wmi.Str(bios, "SMBIOSBIOSVersion"),
                                  rel.HasValue ? "(" + Fmt.Date(rel.Value) + ")" : null);
            }

            // --- ドライブ（C: など） / 드라이브(C: 등) ---
            foreach (var di in DriveInfo.GetDrives())
            {
                try
                {
                    if (di.DriveType != DriveType.Fixed || !di.IsReady) continue;
                    d.Volumes.Add(new Volume
                    {
                        Drive = di.Name.TrimEnd('\\'),
                        Format = di.DriveFormat,
                        Label = di.VolumeLabel,
                        Size = (ulong)di.TotalSize,
                        Free = (ulong)di.TotalFreeSpace,
                    });
                }
                catch { }
            }

            // --- 物理ディスク（SSD/HDD の区別は Storage 名前空間から） ---
            // --- 물리 디스크 (SSD/HDD 구분은 Storage 네임스페이스에서) ---
            var pds = Wmi.Query(@"root\Microsoft\Windows\Storage", "SELECT * FROM MSFT_PhysicalDisk");
            if (pds != null && pds.Count > 0)
            {
                foreach (var p in pds)
                {
                    d.Disks.Add(new PhysDisk
                    {
                        Model = Wmi.Str(p, "FriendlyName"),
                        Size = Wmi.U64(p, "Size"),
                        Media = MediaName((int)Wmi.U64(p, "MediaType")),
                        Bus = BusName((int)Wmi.U64(p, "BusType")),
                    });
                }
            }
            else
            {
                foreach (var p in Wmi.Cim("Win32_DiskDrive"))
                    d.Disks.Add(new PhysDisk { Model = Wmi.Str(p, "Model"), Size = Wmi.U64(p, "Size"), Bus = Wmi.Str(p, "InterfaceType") });
            }

            // --- ネットワーク（接続中のものだけ） / 네트워크 (연결된 것만) ---
            NetworkInterface[] nics;
            try { nics = NetworkInterface.GetAllNetworkInterfaces(); } catch { nics = new NetworkInterface[0]; }
            foreach (var ni in nics)
            {
                try   // 1 つ失敗しても他のアダプターは表示する / 하나가 실패해도 다른 어댑터는 표시
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                    var ips = ni.GetIPProperties().UnicastAddresses
                        .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(a => a.Address.ToString());
                    var mac = ni.GetPhysicalAddress().GetAddressBytes();
                    d.Adapters.Add(new NetAdapter
                    {
                        Name = ni.Name,
                        IPv4 = string.Join(", ", ips),
                        Mac = mac.Length > 0 ? string.Join("-", mac.Select(b => b.ToString("X2"))) : null,
                        Speed = ni.Speed,
                    });
                }
                catch { }
            }

            return d;
        }

        private static string MediaName(int t)
        {
            switch (t) { case 3: return "HDD"; case 4: return "SSD"; case 5: return "SCM"; default: return null; }
        }

        private static string BusName(int t)
        {
            switch (t)
            {
                case 7: return "USB"; case 8: return "RAID"; case 10: return "SAS";
                case 11: return "SATA"; case 12: return "SD"; case 17: return "NVMe";
                default: return null;
            }
        }

        // ================= セキュリティ / 보안 =================
        public static SecurityData CollectSecurity()
        {
            var s = new SecurityData();

            // --- Microsoft Defender の詳細 / 상세 ---
            var mp = Wmi.Query(@"root\Microsoft\Windows\Defender", "SELECT * FROM MSFT_MpComputerStatus");
            var st = mp == null ? null : mp.FirstOrDefault();
            if (st != null)
            {
                var def = s.Defender;
                def.Available = true;
                def.ProductVersion = Ver(Wmi.Str(st, "AMProductVersion"));
                def.EngineVersion = Ver(Wmi.Str(st, "AMEngineVersion"));
                def.SignatureVersion = Ver(Wmi.Str(st, "AntivirusSignatureVersion"));
                def.SignatureUpdated = Wmi.Date(st, "AntivirusSignatureLastUpdated");
                def.RealTime = Wmi.Bool(st, "RealTimeProtectionEnabled");
                def.AvEnabled = Wmi.Bool(st, "AntivirusEnabled");
                def.RunningMode = Wmi.Str(st, "AMRunningMode");
                if (def.SignatureUpdated.HasValue)
                    def.SignatureAgeDays = Math.Max(0, (int)(DateTime.Now - def.SignatureUpdated.Value).TotalDays);
            }

            // --- Windows セキュリティセンターに登録されているウイルス対策ソフト ---
            // --- Windows 보안 센터에 등록된 백신 소프트웨어 (Defender, 타사 백신 모두) ---
            var avs = Wmi.Query(@"root\SecurityCenter2", "SELECT * FROM AntiVirusProduct");
            if (avs != null)
            {
                s.SecurityCenterOk = true;
                foreach (var a in avs)
                {
                    // productState の中身: 0x1000 のビット = 有効、0x10 のビット = 定義ファイルが古い
                    // productState 내용: 0x1000 비트 = 사용 중, 0x10 비트 = 정의 파일이 오래됨
                    ulong state = Wmi.U64(a, "productState");
                    string path = Wmi.Str(a, "pathToSignedProductExe");
                    var p = new AvProduct
                    {
                        Name = Wmi.Str(a, "displayName"),
                        Enabled = (state & 0x1000) != 0,
                        UpToDate = (state & 0x10) == 0,
                    };

                    if (path != null && path.StartsWith("windowsdefender", StringComparison.OrdinalIgnoreCase))
                    {
                        p.IsDefender = true;
                        p.Version = s.Defender.ProductVersion;    // Defender は上で取った版を使う / Defender는 위에서 가져온 버전 사용
                        // セキュリティセンターが「有効」と報告しても、Defender 本体が停止中なら無効として扱う
                        // 보안 센터가 "사용 중"이라고 해도, Defender 본체가 정지 중이면 사용 안 함으로 취급
                        if (s.Defender.Available && !s.Defender.IsActive) p.Enabled = false;
                    }
                    else
                        p.Version = FileVersion(path);            // 他社製は実行ファイルのバージョン / 타사 백신은 실행 파일 버전

                    s.Products.Add(p);
                }
            }

            s.Update = GetLastUpdate();
            return s;
        }

        // Defender 停止中は "0.0.0.0" が返るので「不明」扱いにする / Defender 정지 중엔 "0.0.0.0"이 오므로 "알 수 없음" 취급
        private static string Ver(string v)
        {
            return string.IsNullOrWhiteSpace(v) || v == "0.0.0.0" ? null : v;
        }

        private static string FileVersion(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return null;
                path = Environment.ExpandEnvironmentVariables(path.Trim('"'));
                if (!File.Exists(path)) return null;
                var v = FileVersionInfo.GetVersionInfo(path);
                return string.IsNullOrWhiteSpace(v.ProductVersion) ? v.FileVersion : v.ProductVersion;
            }
            catch { return null; }
        }

        // --- Windows Update：最後に成功したインストール / 마지막으로 성공한 설치 ---
        private static UpdateInfo GetLastUpdate()
        {
            // 1) Windows Update の履歴（COM）。毎日来る Defender 定義ファイルの更新は除外
            // 1) Windows Update 기록(COM). 매일 오는 Defender 정의 파일 업데이트는 제외
            try
            {
                var t = Type.GetTypeFromProgID("Microsoft.Update.Session");
                if (t != null)
                {
                    dynamic session = Activator.CreateInstance(t);
                    dynamic searcher = session.CreateUpdateSearcher();
                    int count = searcher.GetTotalHistoryCount();
                    if (count > 0)
                    {
                        dynamic history = searcher.QueryHistory(0, Math.Min(count, 200));
                        DateTime? best = null;
                        string title = null;
                        foreach (dynamic e in history)   // COM のコレクションは foreach で回すのが確実 / COM 컬렉션은 foreach가 확실
                        {
                            int op = (int)e.Operation;      // 1 = インストール / 설치
                            int rc = (int)e.ResultCode;     // 2 = 成功 / 성공
                            string tt = (string)e.Title ?? "";
                            if (op != 1 || rc != 2 || IsDefinitionUpdate(tt)) continue;
                            DateTime dt = ((DateTime)e.Date).ToLocalTime();
                            if (best == null || dt > best.Value) { best = dt; title = tt; }
                        }
                        if (best.HasValue) return new UpdateInfo { LastInstall = best, Title = title };
                    }
                }
            }
            catch { }

            // 2) 取れなければ Win32_QuickFixEngineering（インストール済み更新プログラム一覧）
            // 2) 안 되면 Win32_QuickFixEngineering (설치된 업데이트 목록)
            var info = new UpdateInfo();
            foreach (var q in Wmi.Cim("Win32_QuickFixEngineering"))
            {
                var dt = ParseQfeDate(Wmi.Str(q, "InstalledOn"));
                if (dt.HasValue && (info.LastInstall == null || dt.Value > info.LastInstall.Value))
                {
                    info.LastInstall = dt;
                    info.Title = Fmt.Join(" ", Wmi.Str(q, "HotFixID"), Wmi.Str(q, "Description"));
                }
            }
            return info;
        }

        private static bool IsDefinitionUpdate(string title)
        {
            // KB2267602 = Defender 定義ファイル、KB915597 = 旧 Defender 定義、KB4052623 = Defender プラットフォーム
            string[] kbs = { "KB2267602", "KB915597", "KB4052623", "KB2461484" };
            return kbs.Any(k => title.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static DateTime? ParseQfeDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            DateTime d;
            string[] fmts = { "M/d/yyyy", "MM/dd/yyyy", "yyyy/MM/dd", "yyyyMMdd", "yyyy-MM-dd" };
            if (DateTime.TryParseExact(s.Trim(), fmts, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
            if (DateTime.TryParse(s, out d)) return d;
            long ft;   // 古い Windows では 16 進数の FILETIME の場合がある / 구버전은 16진수 FILETIME인 경우가 있음
            if (long.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ft))
            {
                try { return DateTime.FromFileTime(ft); } catch { }
            }
            return null;
        }
    }

    // ================= リアルタイム / 실시간 =================

    /// <summary>
    /// CPU 使用率 = 1 - (アイドル時間の増加分 ÷ 全体時間の増加分)
    /// CPU 사용률 = 1 - (유휴 시간 증가분 ÷ 전체 시간 증가분)
    /// </summary>
    internal class CpuSampler
    {
        private ulong _lastIdle, _lastTotal;
        private bool _has;

        public double? Sample()
        {
            Native.FILETIME idle, kernel, user;
            if (!Native.GetSystemTimes(out idle, out kernel, out user)) return null;
            ulong i = idle.Value, total = kernel.Value + user.Value;   // kernel にはアイドルも含まれる / kernel에는 유휴도 포함됨
            if (!_has) { _lastIdle = i; _lastTotal = total; _has = true; return null; }

            ulong di = i - _lastIdle, dt = total - _lastTotal;
            _lastIdle = i; _lastTotal = total;
            if (dt == 0) return 0;
            return Math.Max(0, Math.Min(100, (1.0 - (double)di / dt) * 100));
        }
    }

    /// <summary>
    /// プロセスごとの CPU・メモリ使用量。同じ名前（chrome など）はまとめる
    /// 프로세스별 CPU·메모리 사용량. 같은 이름(chrome 등)은 묶어서 계산
    /// </summary>
    internal class ProcSampler
    {
        private Dictionary<int, TimeSpan> _last = new Dictionary<int, TimeSpan>();
        private DateTime _lastTime = DateTime.MinValue;

        public List<ProcInfo> Sample()
        {
            var now = DateTime.UtcNow;
            bool first = _lastTime == DateTime.MinValue;
            double elapsed = (now - _lastTime).TotalMilliseconds;
            int cores = Environment.ProcessorCount;

            var next = new Dictionary<int, TimeSpan>();
            var groups = new Dictionary<string, ProcInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    try
                    {
                        if (p.Id == 0) continue;   // System Idle Process
                        string name = p.ProcessName;
                        long mem = 0;
                        try { mem = p.WorkingSet64; } catch { }

                        // CPU% = (この間に使った CPU 時間) ÷ (経過時間 × コア数)
                        // CPU% = (그 사이에 사용한 CPU 시간) ÷ (경과 시간 × 코어 수)
                        double cpu = 0;
                        try
                        {
                            var t = p.TotalProcessorTime;   // 権限がないプロセスは例外になる / 권한 없는 프로세스는 예외 발생
                            next[p.Id] = t;
                            TimeSpan prev;
                            if (!first && elapsed > 0 && _last.TryGetValue(p.Id, out prev) && t >= prev)
                                cpu = (t - prev).TotalMilliseconds / (elapsed * cores) * 100;
                        }
                        catch { }

                        ProcInfo g;
                        if (!groups.TryGetValue(name, out g)) { g = new ProcInfo { Name = name }; groups[name] = g; }
                        g.Count++;
                        g.Cpu += cpu;
                        g.Memory += mem;
                    }
                    catch { }
                }
            }

            _last = next;
            _lastTime = now;
            return groups.Values.ToList();
        }
    }
}
