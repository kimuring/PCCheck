using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PCCheck
{
    /// <summary>
    /// レポート（テキスト）と CSV の作成 / 리포트(텍스트)와 CSV 생성
    /// テキストはヘルプデスクへの問い合わせにそのまま貼り付けられる形
    /// 텍스트는 헬프데스크 문의에 그대로 붙여넣을 수 있는 형태
    /// </summary>
    internal static class Report
    {
        public static string Text(DateTime at, List<Row> checks, List<Row> overview, List<Row> security, List<Row> live)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==== " + L.T("r.title") + " ====");
            sb.AppendLine(L.T("r.time") + ": " + at.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();

            sb.AppendLine("[" + L.T("sec.checks") + "]");
            foreach (var r in checks)
                sb.AppendLine("  [" + RowBuilder.StatusText(r.Status) + "] " + r.Label + ": " + r.Value);
            sb.AppendLine();

            Section(sb, L.T("tab.overview"), overview);
            Section(sb, L.T("tab.security"), security);
            Section(sb, L.T("r.live"), live);
            return sb.ToString();
        }

        private static void Section(StringBuilder sb, string title, List<Row> rows)
        {
            sb.AppendLine("[" + title + "]");
            string last = null;
            foreach (var r in rows)
            {
                if (r.Group != last) { sb.AppendLine("  ■ " + r.Group); last = r.Group; }
                sb.AppendLine("    " + r.Label + ": " + r.Value);
            }
            sb.AppendLine();
        }

        public static string Csv(List<Row> checks, List<Row> overview, List<Row> security, List<Row> live)
        {
            var sb = new StringBuilder();
            Line(sb, L.T("csv.section"), L.T("csv.group"), L.T("col.item"), L.T("col.value"), L.T("col.status"));
            Add(sb, L.T("sec.checks"), checks);
            Add(sb, L.T("tab.overview"), overview);
            Add(sb, L.T("tab.security"), security);
            Add(sb, L.T("r.live"), live);
            return sb.ToString();
        }

        private static void Add(StringBuilder sb, string section, List<Row> rows)
        {
            foreach (var r in rows) Line(sb, section, r.Group, r.Label, r.Value, RowBuilder.StatusText(r.Status));
        }

        private static void Line(StringBuilder sb, params string[] cols)
        {
            sb.Append(string.Join(",", cols.Select(Esc))).Append("\r\n");
        }

        // カンマ・改行・ダブルクォートを含む値は "..." で囲む（Excel で列がずれないように）
        // 쉼표·줄바꿈·큰따옴표가 들어간 값은 "..."로 감쌈 (Excel에서 열이 밀리지 않게)
        private static string Esc(string s)
        {
            s = s ?? "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
