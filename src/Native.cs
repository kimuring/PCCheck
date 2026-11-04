using System;
using System.Runtime.InteropServices;

namespace PCCheck
{
    /// <summary>
    /// Windows API の宣言 / Windows API 선언
    /// C# の標準機能だけでは取れない情報（CPU 使用率・メモリ使用率・稼働時間）をここから取得する
    /// C# 기본 기능만으로는 못 가져오는 정보(CPU 사용률·메모리 사용률·가동 시간)를 여기서 가져옴
    /// </summary>
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public uint Low;
            public uint High;
            public ulong Value { get { return ((ulong)High << 32) | Low; } }
        }

        // CPU の「アイドル時間・カーネル時間・ユーザー時間」の累計を返す
        // CPU의 "유휴 시간·커널 시간·사용자 시간" 누적값을 반환
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

        [StructLayout(LayoutKind.Sequential)]
        public class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;          // メモリ使用率(%) / 메모리 사용률(%)
            public ulong ullTotalPhys;         // 物理メモリ合計 / 물리 메모리 합계
            public ulong ullAvailPhys;         // 空きメモリ / 여유 메모리
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX buffer);

        // 起動してからの経過ミリ秒 / 부팅 후 경과 밀리초
        [DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();

        // 高DPI（画面の拡大率125%など）で文字がぼやけないようにする
        // 고DPI(화면 배율 125% 등)에서 글자가 흐릿해지지 않게
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
    }
}
