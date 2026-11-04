using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PCCheck
{
    /// <summary>
    /// 表示言語（日本語 / 韓国語）の切り替え
    /// 표시 언어(일본어 / 한국어) 전환
    /// 画面の文字はすべてここにまとめ、キーで呼び出す / 화면 문자열은 전부 여기 모아두고 키로 불러옴
    /// </summary>
    internal static class L
    {
        public static string Lang = "ja";

        /// <summary>言語ごとのフォント（日本語: Meiryo UI / 韓国語: Malgun Gothic）</summary>
        public static string FontFace { get { return Lang == "ko" ? "Malgun Gothic" : "Meiryo UI"; } }

        public static string T(string key)
        {
            string[] v;
            if (M.TryGetValue(key, out v)) return Lang == "ko" ? v[1] : v[0];
            return key;   // 見つからないときはキーをそのまま表示 / 못 찾으면 키를 그대로 표시
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        //                                       { 日本語,  한국어 }
        private static readonly Dictionary<string, string[]> M = new Dictionary<string, string[]>
        {
            // ----- アプリ・メニュー / 앱·메뉴 -----
            { "app.title",      new[] { "PC Check - PC状態チェックツール", "PC Check - PC 상태 점검 도구" } },
            { "app.name",       new[] { "PC Check", "PC Check" } },
            { "menu.file",      new[] { "ファイル(&F)", "파일(&F)" } },
            { "menu.refresh",   new[] { "再読み込み", "다시 읽기" } },
            { "menu.copy",      new[] { "レポートをコピー", "리포트 복사" } },
            { "menu.csv",       new[] { "CSVで保存...", "CSV로 저장..." } },
            { "menu.exit",      new[] { "終了", "종료" } },
            { "menu.settings",  new[] { "設定(&S)", "설정(&S)" } },
            { "menu.language",  new[] { "表示言語", "표시 언어" } },
            { "menu.help",      new[] { "ヘルプ(&H)", "도움말(&H)" } },
            { "menu.about",     new[] { "バージョン情報", "버전 정보" } },

            // ----- タブ・ボタン・列 / 탭·버튼·열 -----
            { "tab.overview",   new[] { "概要", "개요" } },
            { "tab.live",       new[] { "リアルタイム", "실시간" } },
            { "tab.security",   new[] { "セキュリティ", "보안" } },
            { "btn.refresh",    new[] { "再読み込み (F5)", "다시 읽기 (F5)" } },
            { "btn.copy",       new[] { "レポートをコピー", "리포트 복사" } },
            { "btn.csv",        new[] { "CSVで保存", "CSV로 저장" } },
            { "col.item",       new[] { "項目", "항목" } },
            { "col.value",      new[] { "値", "값" } },
            { "col.status",     new[] { "判定", "판정" } },
            { "col.check",      new[] { "チェック項目", "점검 항목" } },
            { "col.detail",     new[] { "内容", "내용" } },
            { "col.proc",       new[] { "プロセス名", "프로세스명" } },
            { "col.count",      new[] { "数", "개수" } },
            { "col.cpu",        new[] { "CPU", "CPU" } },
            { "col.mem",        new[] { "メモリ", "메모리" } },
            { "ctx.value",      new[] { "値をコピー", "값 복사" } },
            { "ctx.row",        new[] { "行をコピー", "행 복사" } },

            // ----- リアルタイム / 실시간 -----
            { "live.sortCpu",   new[] { "CPU使用率順", "CPU 사용률 순" } },
            { "live.sortMem",   new[] { "メモリ使用量順", "메모리 사용량 순" } },
            { "live.note",      new[] { "同じ名前のプロセスはまとめて表示 ・ 2秒ごとに更新", "같은 이름의 프로세스는 묶어서 표시 · 2초마다 갱신" } },
            { "live.cpuTitle",  new[] { "CPU使用率", "CPU 사용률" } },
            { "live.memTitle",  new[] { "メモリ使用率", "메모리 사용률" } },

            // ----- 状態表示 / 상태 표시 -----
            { "sec.checks",     new[] { "チェック結果", "점검 결과" } },
            { "sec.details",    new[] { "詳細", "상세" } },
            { "status.loading", new[] { "情報を読み込み中...", "정보를 불러오는 중..." } },
            { "status.updated", new[] { "取得日時: {0}", "취득 일시: {0}" } },
            { "status.copied",  new[] { "レポートをクリップボードにコピーしました", "리포트를 클립보드에 복사했어요" } },
            { "status.saved",   new[] { "保存しました: {0}", "저장했어요: {0}" } },
            { "badge.alert",    new[] { "要対応 {0}件", "조치 필요 {0}건" } },
            { "badge.warn",     new[] { "注意 {0}件", "주의 {0}건" } },
            { "badge.ok",       new[] { "問題なし", "문제 없음" } },
            { "badge.loading",  new[] { "確認中...", "확인 중..." } },
            { "st.warn",        new[] { "注意", "주의" } },
            { "st.alert",       new[] { "要対応", "조치 필요" } },
            { "unknown",        new[] { "不明", "알 수 없음" } },
            { "na",             new[] { "取得できません", "가져올 수 없음" } },

            // ----- 概要 / 개요 -----
            { "g.system",       new[] { "システム", "시스템" } },
            { "g.memory",       new[] { "メモリ", "메모리" } },
            { "g.gpu",          new[] { "グラフィックス", "그래픽" } },
            { "g.board",        new[] { "マザーボード・BIOS", "메인보드·BIOS" } },
            { "g.storage",      new[] { "ストレージ", "저장 장치" } },
            { "g.network",      new[] { "ネットワーク", "네트워크" } },
            { "o.host",         new[] { "コンピューター名", "컴퓨터 이름" } },
            { "o.user",         new[] { "ユーザー名", "사용자 이름" } },
            { "o.model",        new[] { "メーカー・モデル", "제조사·모델" } },
            { "o.os",           new[] { "OS", "OS" } },
            { "o.osver",        new[] { "バージョン", "버전" } },
            { "o.build",        new[] { "ビルド", "빌드" } },
            { "o.install",      new[] { "インストール日", "설치일" } },
            { "o.boot",         new[] { "最終起動", "마지막 부팅" } },
            { "o.uptime",       new[] { "稼働時間", "가동 시간" } },
            { "o.name",         new[] { "名前", "이름" } },
            { "o.cores",        new[] { "コア / スレッド", "코어 / 스레드" } },
            { "o.sockets",      new[] { "ソケット", "소켓" } },
            { "o.clock",        new[] { "基本クロック", "기본 클럭" } },
            { "o.fast",         new[] { "高速スタートアップ", "빠른 시작" } },
            { "o.cache",        new[] { "キャッシュ", "캐시" } },
            { "o.memtotal",     new[] { "搭載容量", "탑재 용량" } },
            { "o.slots",        new[] { "スロット使用", "슬롯 사용" } },
            { "o.module",       new[] { "メモリ", "메모리" } },
            { "o.driver",       new[] { "ドライバー", "드라이버" } },
            { "o.board",        new[] { "マザーボード", "메인보드" } },
            { "o.disk",         new[] { "ディスク {0}", "디스크 {0}" } },
            { "o.nonet",        new[] { "接続中のネットワークがありません", "연결된 네트워크가 없어요" } },
            { "fmt.uptime",     new[] { "{0}日 {1}時間 {2}分", "{0}일 {1}시간 {2}분" } },
            { "fmt.cores",      new[] { "{0} コア / {1} スレッド", "{0}코어 / {1}스레드" } },
            { "fmt.free",       new[] { "空き {0} / {1} ({2}%)", "여유 {0} / {1} ({2}%)" } },
            { "fmt.withAgo",    new[] { "{0}（{1}）", "{0} ({1})" } },
            { "ago.today",      new[] { "今日", "오늘" } },
            { "ago.days",       new[] { "{0}日前", "{0}일 전" } },

            // ----- セキュリティ詳細 / 보안 상세 -----
            { "g.av",           new[] { "セキュリティソフト（Windows セキュリティセンター）", "보안 소프트웨어 (Windows 보안 센터)" } },
            { "s.state",        new[] { "状態", "상태" } },
            { "s.enabled",      new[] { "有効", "사용 중" } },
            { "s.disabled",     new[] { "無効", "사용 안 함" } },
            { "s.uptodate",     new[] { "定義ファイル最新", "정의 파일 최신" } },
            { "s.outdated",     new[] { "定義ファイルが古い", "정의 파일 오래됨" } },
            { "s.version",      new[] { "バージョン", "버전" } },
            { "s.noav",         new[] { "登録されているセキュリティソフトがありません", "등록된 보안 소프트웨어가 없어요" } },
            { "s.scna",         new[] { "セキュリティセンターの情報を取得できません（Windows Server など）", "보안 센터 정보를 가져올 수 없어요 (Windows Server 등)" } },
            { "s.on",           new[] { "有効", "켜짐" } },
            { "s.off",          new[] { "無効", "꺼짐" } },
            { "d.na",           new[] { "Defender の情報を取得できません", "Defender 정보를 가져올 수 없어요" } },
            { "d.passive",      new[] { "停止中（{0} が有効なため。正常な状態です）", "꺼져 있음 ({0}이(가) 켜져 있어서예요. 정상 상태예요)" } },
            { "d.stopped",      new[] { "停止しています", "꺼져 있어요" } },
            { "d.av",           new[] { "ウイルス対策", "바이러스 백신" } },
            { "d.realtime",     new[] { "リアルタイム保護", "실시간 보호" } },
            { "d.mode",         new[] { "動作モード", "동작 모드" } },
            { "d.product",      new[] { "製品バージョン", "제품 버전" } },
            { "d.engine",       new[] { "エンジンバージョン", "엔진 버전" } },
            { "d.sig",          new[] { "定義ファイルのバージョン", "정의 파일 버전" } },
            { "d.sigdate",      new[] { "定義ファイルの更新日時", "정의 파일 업데이트 일시" } },
            { "u.last",         new[] { "最後にインストールした更新", "마지막으로 설치한 업데이트" } },
            { "u.title",        new[] { "更新プログラム名", "업데이트 이름" } },

            // ----- チェック（判定） / 점검(판정) -----
            { "c.av",           new[] { "セキュリティソフト", "보안 소프트웨어" } },
            { "c.av.ok",        new[] { "{0} が有効です", "{0} 사용 중이에요" } },
            { "c.av.none",      new[] { "有効なセキュリティソフトがありません", "사용 중인 보안 소프트웨어가 없어요" } },
            { "c.unknown",      new[] { "状態を確認できません", "상태를 확인할 수 없어요" } },
            { "c.def",          new[] { "ウイルス定義ファイル", "바이러스 정의 파일" } },
            { "c.def.ok",       new[] { "最新です（{0}更新）", "최신이에요 ({0} 업데이트)" } },
            { "c.def.okSc",     new[] { "最新です（セキュリティセンターの報告）", "최신이에요 (보안 센터 보고 기준)" } },
            { "c.def.old",      new[] { "{0}日間更新されていません", "{0}일 동안 업데이트되지 않았어요" } },
            { "c.def.outdated", new[] { "{0} の定義ファイルが古い状態です", "{0}의 정의 파일이 오래됐어요" } },
            { "c.rt",           new[] { "リアルタイム保護", "실시간 보호" } },
            { "c.rt.ok",        new[] { "有効です", "켜져 있어요" } },
            { "c.rt.off",       new[] { "無効になっています", "꺼져 있어요" } },
            { "c.wu",           new[] { "Windows Update", "Windows Update" } },
            { "c.wu.ok",        new[] { "最終インストール: {0}", "마지막 설치: {0}" } },
            { "c.wu.old",       new[] { "{0}日間インストールされていません", "{0}일 동안 설치되지 않았어요" } },
            { "c.wu.unknown",   new[] { "更新履歴を確認できません", "업데이트 기록을 확인할 수 없어요" } },
            { "c.disk",         new[] { "ディスク空き容量 ({0})", "디스크 여유 공간 ({0})" } },
            { "c.disk.detail",  new[] { "空き {0}%（{1}）", "여유 {0}% ({1})" } },
            { "c.uptime",       new[] { "再起動", "재부팅" } },
            { "c.uptime.ok",    new[] { "最終起動から {0}日", "마지막 부팅 후 {0}일" } },
            { "c.uptime.long",  new[] { "{0}日間再起動されていません（再起動を推奨）", "{0}일 동안 재부팅하지 않았어요 (재부팅 권장)" } },
            { "c.uptime.longFast", new[] { "{0}日間再起動されていません。高速スタートアップが有効なため、シャットダウンではリセットされません（「再起動」を推奨）",
                                           "{0}일 동안 재부팅하지 않았어요. 빠른 시작이 켜져 있어서 시스템 종료로는 초기화되지 않아요 (「다시 시작」 권장)" } },

            // ----- レポート・CSV / 리포트·CSV -----
            { "r.title",        new[] { "PC Check レポート", "PC Check 리포트" } },
            { "r.time",         new[] { "取得日時", "취득 일시" } },
            { "r.live",         new[] { "現在の使用状況", "현재 사용 현황" } },
            { "r.usage",        new[] { "CPU・メモリ", "CPU·메모리" } },
            { "r.topmem",       new[] { "メモリ使用量 TOP5", "메모리 사용량 TOP5" } },
            { "r.topcpu",       new[] { "CPU使用率 TOP5", "CPU 사용률 TOP5" } },
            { "csv.section",    new[] { "区分", "구분" } },
            { "csv.group",      new[] { "グループ", "그룹" } },
            { "dlg.csv",        new[] { "CSVファイル (*.csv)|*.csv", "CSV 파일 (*.csv)|*.csv" } },

            // ----- メッセージ / 메시지 -----
            { "msg.error",      new[] { "エラーが発生しました:\n{0}", "오류가 발생했어요:\n{0}" } },
            { "msg.saveFail",   new[] { "保存できませんでした。\nExcel などで開いている場合は、閉じてからもう一度お試しください。\n\n{0}",
                                        "저장하지 못했어요.\nExcel 등에서 열려 있다면 닫은 뒤 다시 시도해 주세요.\n\n{0}" } },
            { "about.text",     new[] { "PC Check v{0}\n\nPCのハードウェア・セキュリティ・リソース使用状況を確認するツールです。\n管理者権限なしで動作します。",
                                        "PC Check v{0}\n\nPC의 하드웨어·보안·리소스 사용 현황을 점검하는 도구예요.\n관리자 권한 없이 동작해요." } },
        };
    }

    /// <summary>
    /// 設定の保存（%APPDATA%\PCCheck\settings.ini）/ 설정 저장
    /// 選んだ言語を次回起動時も覚えておく / 고른 언어를 다음 실행 때도 기억
    /// </summary>
    internal static class Settings
    {
        private static string FilePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "PCCheck", "settings.ini");
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (var line in File.ReadAllLines(FilePath))
                    {
                        var t = line.Trim();
                        if (!t.StartsWith("lang=")) continue;
                        var v = t.Substring(5);
                        if (v == "ja" || v == "ko") { L.Lang = v; return; }
                    }
                }
            }
            catch { }

            // 初回起動: Windows の表示言語が韓国語なら韓国語、それ以外は日本語
            // 첫 실행: Windows 표시 언어가 한국어면 한국어, 그 외에는 일본어
            L.Lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko" ? "ko" : "ja";
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, "lang=" + L.Lang + "\r\n");
            }
            catch { }
        }
    }
}
