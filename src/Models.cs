using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PCCheck
{
    /// <summary>判定結果 / 판정 결과（数字が大きいほど深刻 / 숫자가 클수록 심각）</summary>
    public enum Status { None = 0, OK = 1, Warn = 2, Alert = 3 }

    /// <summary>画面・レポート・CSV に出す 1 行 / 화면·리포트·CSV에 표시할 1행</summary>
    public class Row
    {
        public string Group;
        public string Label;
        public string Value;
        public Status Status;

        public Row(string group, string label, string value, Status status = Status.None)
        {
            Group = group; Label = label; Value = value; Status = status;
        }
    }

    // ===== 概要（ハードウェア・OS） / 개요(하드웨어·OS) =====
    public class OverviewData
    {
        public string HostName, UserName, Maker, Model;
        public string OsName, OsDisplayVersion, OsBuild, OsArch;
        public DateTime? InstallDate;
        public TimeSpan Uptime;
        public bool? FastStartup;   // 高速スタートアップ（null = 不明） / 빠른 시작 (null = 알 수 없음)

        public string CpuName;
        public int CpuSockets, CpuCores, CpuThreads, CpuMaxMHz;
        public uint L2KB, L3KB;

        public ulong RamTotal;
        public int RamSlotsTotal;
        public List<MemModule> Modules = new List<MemModule>();

        public List<GpuInfo> Gpus = new List<GpuInfo>();
        public string Board, Bios;

        public List<Volume> Volumes = new List<Volume>();
        public List<PhysDisk> Disks = new List<PhysDisk>();
        public List<NetAdapter> Adapters = new List<NetAdapter>();
    }

    public class MemModule { public ulong Capacity; public uint Speed; public int Type; public string Maker, Part, Slot; }
    public class GpuInfo { public string Name, Driver, Mode; }
    public class Volume { public string Drive, Format, Label; public ulong Size, Free; }
    public class PhysDisk { public string Model, Media, Bus; public ulong Size; }
    public class NetAdapter { public string Name, IPv4, Mac; public long Speed; }

    // ===== セキュリティ / 보안 =====
    public class SecurityData
    {
        public bool SecurityCenterOk;   // セキュリティセンターから取得できたか / 보안 센터에서 가져왔는지
        public List<AvProduct> Products = new List<AvProduct>();
        public DefenderInfo Defender = new DefenderInfo();
        public UpdateInfo Update = new UpdateInfo();
    }

    public class AvProduct { public string Name, Version; public bool Enabled, UpToDate, IsDefender; }

    public class DefenderInfo
    {
        public bool Available;
        public string ProductVersion, EngineVersion, SignatureVersion, RunningMode;
        public DateTime? SignatureUpdated;
        public int? SignatureAgeDays;
        public bool? RealTime, AvEnabled;

        /// <summary>Defender がメインのウイルス対策として動いているか / Defender가 메인 백신으로 동작 중인지</summary>
        public bool IsActive
        {
            get
            {
                // 他社製ソフトが有効だと Defender は "Passive Mode" や "Not running" になる
                // 타사 백신이 켜져 있으면 Defender는 "Passive Mode"나 "Not running" 상태가 됨
                return Available && AvEnabled == true &&
                       (RunningMode == null ||
                        (RunningMode.IndexOf("Passive", StringComparison.OrdinalIgnoreCase) < 0 &&
                         RunningMode.IndexOf("Not running", StringComparison.OrdinalIgnoreCase) < 0));
            }
        }
    }

    public class UpdateInfo { public DateTime? LastInstall; public string Title; }

    // ===== リアルタイム / 실시간 =====
    public class ProcInfo { public string Name; public int Count; public double Cpu; public long Memory; }

    /// <summary>表示用の書式 / 표시용 서식</summary>
    internal static class Fmt
    {
        public static string Bytes(double b)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
            string num = i == 0 ? b.ToString("0", CultureInfo.InvariantCulture)
                                : b.ToString(b >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture);
            return num + " " + u[i];
        }

        public static string DateTime(DateTime d) { return d.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture); }
        public static string Date(DateTime d) { return d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture); }

        /// <summary>空の部分を除いてつなげる / 빈 부분은 빼고 연결</summary>
        public static string Join(string sep, params string[] parts)
        {
            return string.Join(sep, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        }
    }
}
